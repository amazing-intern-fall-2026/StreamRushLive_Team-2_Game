using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Chướng ngại vật Đá / Thùng Lăn Bập Bênh (Bouncing Boulder - GDD v1.2 Mục 2):
    /// Lăn ngược chiều cộng hưởng cùng tốc độ thế giới, đồng thời nảy hình sin linh hoạt.
    /// Hỗ trợ quỹ đạo ngẫu nhiên (Random Trajectory):
    /// - Ngẫu nhiên độ cao nảy (min/max): lúc nảy thấp (1.2m - nhảy qua), lúc nảy vọt xà (3.8m - trượt dưới).
    /// - Ngẫu nhiên chu kỳ nảy (min/max): lúc nảy nhanh, lúc nảy bổng chậm rãi.
    /// - Ngẫu nhiên tốc độ lăn (min/max).
    /// - Tự động đổi ngẫu nhiên thông số sau mỗi lần tiếp đất nếu bật randomizeEachBounce.
    /// - Hiệu ứng lăn xoay tròn theo trục Z.
    /// Hậu quả va chạm: Trừ 35% Energy và trừ 15m cự ly chặng.
    /// </summary>
    public class BouncingBoulderObstacle : ObstacleBase
    {
        [Header("Fixed Fallback Settings")]
        [SerializeField] private float bounceHeight = 1.8f;
        [SerializeField] private float bouncePeriod = 0.8f;
        [SerializeField] private float rollSpeed = 3.5f;

        [Header("Random Trajectory Settings")]
        [Tooltip("Bật tính năng ngẫu nhiên hóa quỹ đạo")]
        [SerializeField] private bool useRandomTrajectory = true;

        [Tooltip("Khoảng độ cao nảy ngẫu nhiên (min, max tính bằng mét)")]
        [SerializeField] private Vector2 bounceHeightRange = new Vector2(1.2f, 3.8f);

        [Tooltip("Khoảng chu kỳ nảy ngẫu nhiên (min, max tính bằng giây cho mỗi nhịp nảy)")]
        [SerializeField] private Vector2 bouncePeriodRange = new Vector2(0.8f, 1.5f);

        [Tooltip("Khoảng tốc độ tự lăn về phía Runner (min, max tính bằng m/s)")]
        [SerializeField] private Vector2 rollSpeedRange = new Vector2(1.5f, 4.0f);

        [Tooltip("Đổi ngẫu nhiên độ cao và chu kỳ sau mỗi lần tiếp đất")]
        [SerializeField] private bool randomizeEachBounce = true;

        [Header("Visual Rolling Effect")]
        [Tooltip("Tự động xoay tròn vật thể theo trục Z khi lăn về phía Runner")]
        [SerializeField] private bool enableVisualRoll = true;
        [SerializeField] private float rollRotationMultiplier = 120f;

        private float _baseY;
        private float _bounceTimer;
        private float _currentBounceHeight;
        private float _currentBouncePeriod;
        private float _currentRollSpeed;
        private MovingWorldObject _movingWorldObject;
        private WorldSpeedManager _speedManager;

        private void Awake()
        {
            obstacleType = ObstacleType.BouncingBoulder;
            _baseY = transform.position.y;
            _movingWorldObject = GetComponent<MovingWorldObject>();
        }

        private void Start()
        {
            _baseY = transform.position.y;

            if (_speedManager == null)
            {
                _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            }

            if (useRandomTrajectory)
            {
                PickRandomBounceParameters();
                float minR = Mathf.Min(rollSpeedRange.x, rollSpeedRange.y);
                float maxR = Mathf.Max(rollSpeedRange.x, rollSpeedRange.y);
                _currentRollSpeed = Random.Range(minR, maxR);

                // Bắt đầu tại một thời điểm ngẫu nhiên trong chu kỳ nảy để các tảng đá không nảy rập khuôn cùng lúc
                _bounceTimer = Random.Range(0f, _currentBouncePeriod);
            }
            else
            {
                _currentBounceHeight = bounceHeight;
                _currentBouncePeriod = bouncePeriod;
                _currentRollSpeed = rollSpeed;
                _bounceTimer = 0f;
            }
        }

        private void Update()
        {
            if (_speedManager == null)
            {
                _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            }

            float dt = Time.deltaTime;
            _bounceTimer += dt;

            // Khi chạm đất hoàn thành 1 nhịp nảy:
            if (_bounceTimer >= _currentBouncePeriod)
            {
                _bounceTimer -= _currentBouncePeriod;
                if (useRandomTrajectory && randomizeEachBounce)
                {
                    PickRandomBounceParameters();
                }
            }

            Vector3 position = transform.position;

            // Nếu không có MovingWorldObject thì phải tự tính tốc độ di chuyển tiếp cận Runner
            float approachSpeed = 0f;
            if (_movingWorldObject == null)
            {
                if (_speedManager != null)
                {
                    if (_speedManager.CurrentSpeed < -0.05f)
                    {
                        approachSpeed = _speedManager.CurrentSpeed;
                    }
                    else if (_speedManager.CurrentSpeed > 0.05f && !_speedManager.IsRecovering)
                    {
                        approachSpeed = _speedManager.CurrentSpeed;
                    }
                    else
                    {
                        approachSpeed = _speedManager.BaseSpeed > 0f ? _speedManager.BaseSpeed : 10f;
                    }
                }
                else
                {
                    approachSpeed = 10f;
                }
            }

            float effectiveRollSpeed = useRandomTrajectory ? _currentRollSpeed : rollSpeed;

            // rollSpeed luôn cuộn về phía Runner (-X)
            position.x -= (effectiveRollSpeed + approachSpeed) * dt;

            // Quỹ đạo nảy hình sin: đạt đỉnh ở giữa chu kỳ và tiếp đất ở cuối chu kỳ
            float progress = Mathf.Clamp01(_bounceTimer / Mathf.Max(0.01f, _currentBouncePeriod));
            float effectiveHeight = useRandomTrajectory ? _currentBounceHeight : bounceHeight;
            position.y = _baseY + effectiveHeight * Mathf.Sin(progress * Mathf.PI);

            transform.position = position;

            // Hiệu ứng xoay tròn thị giác khi lăn
            if (enableVisualRoll)
            {
                float totalMoveSpeed = effectiveRollSpeed + (_movingWorldObject != null ? 10f : approachSpeed);
                transform.Rotate(Vector3.forward, totalMoveSpeed * rollRotationMultiplier * dt, Space.World);
            }
        }

        private void PickRandomBounceParameters()
        {
            float minH = Mathf.Min(bounceHeightRange.x, bounceHeightRange.y);
            float maxH = Mathf.Max(bounceHeightRange.x, bounceHeightRange.y);
            if (minH <= 0.1f) minH = 1.0f;
            if (maxH < minH) maxH = minH + 1.0f;

            float minP = Mathf.Min(bouncePeriodRange.x, bouncePeriodRange.y);
            float maxP = Mathf.Max(bouncePeriodRange.x, bouncePeriodRange.y);
            if (minP <= 0.1f) minP = 0.6f;
            if (maxP < minP) maxP = minP + 0.5f;

            _currentBounceHeight = Random.Range(minH, maxH);
            _currentBouncePeriod = Random.Range(minP, maxP);
        }

        public override void OnHitPlayer(GameObject player)
        {
            // Đã được xử lý trong ObstacleBase (trừ 15m quãng đường) và RunnerCollisionHandler
        }
    }
}
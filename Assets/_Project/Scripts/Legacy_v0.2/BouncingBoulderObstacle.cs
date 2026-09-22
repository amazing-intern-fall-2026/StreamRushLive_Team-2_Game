using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Chướng ngại vật Đá / Thùng Lăn Bập Bênh (Bouncing Boulder - GDD v1.2 Mục 2):
    /// Lăn ngược chiều (3.5 m/s) cộng hưởng cùng tốc độ cuộn của thế giới, đồng thời
    /// nảy hình sin với chu kỳ 0.8s, độ cao 1.8m.
    /// Hậu quả va chạm: Trừ 35% Energy và trừ 15m cự ly chặng.
    /// </summary>
    public class BouncingBoulderObstacle : ObstacleBase
    {
        [Header("Bounce & Roll Settings")]
        [SerializeField] private float bounceHeight = 1.8f;
        [SerializeField] private float bouncePeriod = 0.8f;
        [SerializeField] private float rollSpeed = 3.5f;

        private float _baseY;
        private float _elapsedTime;
        private MovingWorldObject _movingWorldObject;
        private WorldSpeedManager _speedManager;

        private void Awake()
        {
            obstacleType = ObstacleType.BouncingBoulder;
            energyPenaltyPercent = 35f;
            distancePenaltyMeters = 15f;
            _baseY = transform.position.y;
            _movingWorldObject = GetComponent<MovingWorldObject>();
        }

        private void Start()
        {
            if (_speedManager == null)
            {
                _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
        }

        private void Update()
        {
            _elapsedTime += Time.deltaTime;

            Vector3 position = transform.position;

            // Nếu không có MovingWorldObject thì phải tự cộng dồn cả tốc độ cuộn thế giới
            float worldSpeed = (_movingWorldObject == null && _speedManager != null) ? _speedManager.CurrentSpeed : 0f;
            position.x -= (rollSpeed + worldSpeed) * Time.deltaTime;

            // Quỹ đạo nảy hình sin
            position.y = _baseY + bounceHeight * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * _elapsedTime / bouncePeriod));

            transform.position = position;
        }

        public override void OnHitPlayer(GameObject player)
        {
            // Đã được xử lý trong ObstacleBase (trừ 15m quãng đường) và RunnerCollisionHandler
        }
    }
}
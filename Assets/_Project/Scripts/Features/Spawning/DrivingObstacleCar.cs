using System.Collections.Generic;
using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Điều khiển xe cản đường có tốc độ tự chạy thực tế (Driving Speed)
    /// thay vì chỉ trôi thụ động theo tốc độ thế giới.
    /// Kế thừa ObstacleBase để tương thích hoàn toàn với hệ thống va chạm và khiên.
    /// </summary>
    public class DrivingObstacleCar : ObstacleBase
    {
        [Header("Speed Settings")]
        [Tooltip("Tốc độ xe tự chạy trên mặt đường (m/s) bổ sung vào tốc độ cuộn của thế giới.")]
        [SerializeField] private float _drivingSpeed = 7.0f;

        [Tooltip("Tọa độ X khi xe vượt qua phía sau người chơi để tự hủy.")]
        [SerializeField] private float _despawnXThreshold = -15f;

        [Header("Visual Effects")]
        [Tooltip("Tự động tìm và quay các bánh xe theo tốc độ lái.")]
        [SerializeField] private bool _enableWheelSpin = true;

        [Tooltip("Bán kính bánh xe để tính tốc độ góc quay (m).")]
        [SerializeField] private float _wheelRadius = 0.36f;

        [Tooltip("Tạo độ rung nhún động cơ nhẹ khi xe đang chạy.")]
        [SerializeField] private bool _enableEngineRumble = true;
        [SerializeField] private float _rumbleFrequency = 30f;
        [SerializeField] private float _rumbleAmplitude = 0.008f;

        private WorldSpeedManager _speedManager;
        private readonly List<Transform> _wheelTransforms = new List<Transform>();
        private float _baseY;
        private float _rumbleSeed;

        public float DrivingSpeed
        {
            get => _drivingSpeed;
            set => _drivingSpeed = Mathf.Max(0f, value);
        }

        public void Initialize(WorldSpeedManager speedManager, float drivingSpeed = 7.0f, float despawnXThreshold = -15f)
        {
            _speedManager = speedManager;
            _drivingSpeed = drivingSpeed;
            _despawnXThreshold = despawnXThreshold;
        }

        private void Awake()
        {
            obstacleName = "ObstacleCar";
            obstacleType = ObstacleType.LowBarrier;
            energyPenaltyPercent = 25f;
            hitStopDuration = 0.05f;
            distancePenaltyMeters = 15f;

            _baseY = transform.position.y;
            _rumbleSeed = Random.Range(0f, 100f);

            // Tìm toàn bộ các cụm bánh xe con (bỏ qua vô lăng Steering_Wheel)
            foreach (Transform child in transform)
            {
                string lowerName = child.name.ToLowerInvariant();
                if (lowerName.Contains("wheel") && !lowerName.Contains("steering"))
                {
                    _wheelTransforms.Add(child);
                }
            }
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
            float worldSpeed = _speedManager != null ? _speedManager.CurrentSpeed : 0f;
            float totalSpeed = worldSpeed + _drivingSpeed;
            float dt = Time.deltaTime;

            // 1. Di chuyển xe lao về phía trước (-X) theo tổng vận tốc (vận tốc đường + vận tốc tự lái)
            transform.position += Vector3.left * (totalSpeed * dt);

            // 2. Quay các bánh xe đồng bộ theo tốc độ thực tế của xe trên mặt đường
            if (_enableWheelSpin && _wheelTransforms.Count > 0 && _drivingSpeed > 0.01f)
            {
                // Tốc độ góc (độ/giây) = (v / r) * (180 / PI)
                float angularSpeedDeg = (_drivingSpeed / Mathf.Max(0.1f, _wheelRadius)) * Mathf.Rad2Deg;
                float angleStep = angularSpeedDeg * dt;

                for (int i = 0; i < _wheelTransforms.Count; i++)
                {
                    if (_wheelTransforms[i] != null)
                    {
                        // Quay quanh trục X cục bộ của bánh xe
                        _wheelTransforms[i].Rotate(Vector3.right, angleStep, Space.Self);
                    }
                }
            }

            // 3. Rung nhún động cơ nhẹ tạo cảm giác xe sống động đang nổ máy
            if (_enableEngineRumble)
            {
                float rumbleOffset = Mathf.Sin((Time.time * _rumbleFrequency) + _rumbleSeed) * _rumbleAmplitude;
                Vector3 currentPos = transform.position;
                currentPos.y = _baseY + rumbleOffset;
                transform.position = currentPos;
            }

            // 4. Tự hủy khi xe đã vượt qua người chơi về phía sau
            if (transform.position.x <= _despawnXThreshold)
            {
                Destroy(gameObject);
            }
        }

        public override void OnHitPlayer(GameObject player)
        {
            // 1. Tắt toàn bộ collider để không kích hoạt va chạm trùng lặp
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = false;
            }

            // 2. Dừng tốc độ tự lái để xe bị thế giới và runner kéo lùi lại cùng lúc trong 0.5s xung cuộn ngược
            _drivingSpeed = 0f;

            // 3. Tồn tại găm phía trước Runner trong thời gian xung giật lùi (0.55s) rồi biến mất
            Destroy(gameObject, 0.55f);
        }
    }
}

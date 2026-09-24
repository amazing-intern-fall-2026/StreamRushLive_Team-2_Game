using System.Collections.Generic;
using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Phân cấp xe cản phá theo GDD v1.4 Mục 4.3:
    /// - SedanCar (Xe Con Húc): Giảm 20% năng lượng, đẩy lùi 100m.
    /// - PickupTruck (Xe Bán Tải): Giảm 40% năng lượng, đẩy lùi 200m (kèm choáng).
    /// - HeavyTruck (Xe Tải Hạng Nặng / Xe Bus): Giảm 60% năng lượng, đẩy lùi 400m.
    /// </summary>
    public enum VehicleTier
    {
        SedanCar,     // Xe Con Húc: -20% NL, -100m cự ly
        PickupTruck,  // Xe Bán Tải: -40% NL, -200m cự ly
        HeavyTruck    // Xe Tải Hạng Nặng: -60% NL, -400m cự ly
    }

    /// <summary>
    /// Điều khiển xe cản đường có tốc độ tự chạy thực tế (Driving Speed)
    /// thay vì chỉ trôi thụ động theo tốc độ thế giới.
    /// Kế thừa ObstacleBase để tương thích hoàn toàn với hệ thống va chạm và khiên.
    /// </summary>
    public class DrivingObstacleCar : ObstacleBase
    {
        [Header("Vehicle Tier & Penalties (GDD v1.4)")]
        [Tooltip("Cấp bậc của xe chướng ngại vật.")]
        [SerializeField] private VehicleTier _vehicleTier = VehicleTier.SedanCar;

        [Tooltip("Cự ly knockback đẩy giật lùi Runner khi va chạm (m).")]
        [SerializeField] private float _knockbackDistance = 2.0f;

        [Tooltip("Thời gian Runner hồi phục sau cú knockback (giây).")]
        [SerializeField] private float _knockbackDuration = 0.45f;

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

        [Header("World Reverse Knockback (Hiệu ứng cuộn ngược thế giới GDD v1.2/v1.4)")]
        [Tooltip("Vận tốc đỉnh khi thế giới cuộn ngược lại (m/s, giá trị âm).")]
        [SerializeField] private float _reverseWorldPeakSpeed = -15f;
        [Tooltip("Thời lượng thế giới cuộn ngược lại (giây).")]
        [SerializeField] private float _reverseWorldDuration = 0.65f;

        private WorldSpeedManager _speedManager;
        private readonly List<Transform> _wheelTransforms = new List<Transform>();
        private float _baseY;
        private float _rumbleSeed;

        public VehicleTier Tier => _vehicleTier;
        public float KnockbackDistance => _knockbackDistance;
        public float KnockbackDuration => _knockbackDuration;
        public float ReverseWorldPeakSpeed => _reverseWorldPeakSpeed;
        public float ReverseWorldDuration => _reverseWorldDuration;

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

        /// <summary>
        /// Cấu hình thông số hình phạt của xe theo GDD v1.4 Mục 4.3
        /// </summary>
        public void ConfigureTier(VehicleTier tier)
        {
            _vehicleTier = tier;
            switch (tier)
            {
                case VehicleTier.SedanCar:
                    obstacleName = "Xe Con Húc";
                    obstacleType = ObstacleType.LowBarrier;
                    energyPenaltyPercent = 20f;       // -20% năng lượng
                    distancePenaltyMeters = 100f;     // -100m cự ly
                    hitStopDuration = 0.10f;          // 0.10s khựng nhẹ
                    _knockbackDistance = 2.0f;
                    _knockbackDuration = 0.45f;
                    _drivingSpeed = 7.0f;
                    _reverseWorldPeakSpeed = -15.0f;  // Cuộn ngược thế giới -15 m/s (~6.2m trôi lùi)
                    _reverseWorldDuration = 0.65f;
                    break;

                case VehicleTier.PickupTruck:
                    obstacleName = "Xe Bán Tải";
                    obstacleType = ObstacleType.LowBarrier;
                    energyPenaltyPercent = 40f;       // -40% năng lượng
                    distancePenaltyMeters = 200f;     // -200m cự ly
                    hitStopDuration = 0.18f;          // 0.18s khựng/choáng
                    _knockbackDistance = 3.2f;
                    _knockbackDuration = 0.60f;
                    _drivingSpeed = 6.2f;
                    _reverseWorldPeakSpeed = -24.0f;  // Cuộn ngược thế giới -24 m/s (~13.0m trôi lùi)
                    _reverseWorldDuration = 0.85f;
                    break;

                case VehicleTier.HeavyTruck:
                    obstacleName = "Xe Tải Hạng Nặng";
                    obstacleType = ObstacleType.HighBarrier;
                    energyPenaltyPercent = 60f;       // -60% năng lượng
                    distancePenaltyMeters = 400f;     // -400m cự ly
                    hitStopDuration = 0.25f;          // 0.25s cú tông cực mạnh
                    _knockbackDistance = 4.5f;
                    _knockbackDuration = 0.75f;
                    _drivingSpeed = 5.2f;
                    _reverseWorldPeakSpeed = -36.0f;  // Cuộn ngược thế giới -36 m/s (~26.4m trôi lùi)
                    _reverseWorldDuration = 1.15f;
                    break;
            }
        }

        private void Awake()
        {
            ConfigureTier(_vehicleTier);

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

        private Rigidbody _rb;

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
            if (_speedManager == null)
            {
                _speedManager = WorldSpeedManager.Instance ?? FindFirstObjectByType<WorldSpeedManager>();
            }
        }

        private void Update()
        {
            if (_speedManager == null)
            {
                _speedManager = WorldSpeedManager.Instance ?? FindFirstObjectByType<WorldSpeedManager>();
            }

            float worldSpeed = _speedManager != null ? _speedManager.CurrentSpeed : 0f;
            float totalSpeed = worldSpeed + _drivingSpeed;
            float dt = Time.deltaTime;

            // 1. Di chuyển xe lao về phía trước (-X) theo tổng vận tốc (vận tốc đường + vận tốc tự lái)
            if (_rb != null && _rb.isKinematic)
            {
                _rb.position += Vector3.left * (totalSpeed * dt);
                transform.position = _rb.position;
            }
            else
            {
                transform.position += Vector3.left * (totalSpeed * dt);
            }

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

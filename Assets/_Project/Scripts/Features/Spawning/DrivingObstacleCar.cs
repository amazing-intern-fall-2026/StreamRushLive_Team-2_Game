using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Vehicle tiers according to design specs (Sedan, Pickup, Heavy Truck).
    /// </summary>
    public enum VehicleTier
    {
        SedanCar,
        PickupTruck,
        HeavyTruck
    }

    /// <summary>
    /// Controls dynamic obstacle vehicles with independent forward driving speed.
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
        private bool _isPinnedToPlayer;
        private Transform _pinnedPlayerTransform;
        private float _pinnedOffsetX;
        private bool _isDeflectedByShield = false;

        public bool IsDeflectedByShield => _isDeflectedByShield;
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
        /// Xe và Runner đứng yên trong camera, chỉ thế giới cuộn ngược tạo cảm giác bị đẩy lùi 100m / 200m / 400m
        /// </summary>
        public void ConfigureTier(VehicleTier tier)
        {
            _vehicleTier = tier;

            // Đọc thông số trực tiếp từ GiftManager trên Hierarchy nếu có
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                var cfg = StreamRushLive.Features.Gifts.GiftManager.Instance.GetCarConfig(tier);
                if (cfg != null)
                {
                    obstacleName = cfg.vehicleName;
                    obstacleType = tier == VehicleTier.HeavyTruck ? ObstacleType.HighBarrier : ObstacleType.LowBarrier;
                    energyPenaltyPercent = cfg.energyPenaltyPercent;
                    distancePenaltyMeters = cfg.distancePenaltyMeters;
                    hitStopDuration = cfg.hitStopDuration;
                    _knockbackDistance = cfg.knockbackDistance;
                    _knockbackDuration = cfg.knockbackDuration;
                    _drivingSpeed = cfg.drivingSpeed;
                    _reverseWorldPeakSpeed = cfg.reverseWorldPeakSpeed;
                    _reverseWorldDuration = cfg.reverseWorldDuration;
                    return;
                }
            }

            switch (tier)
            {
                case VehicleTier.SedanCar:
                    obstacleName = "Xe Con Húc";
                    obstacleType = ObstacleType.LowBarrier;
                    energyPenaltyPercent = 20f;       // -20% năng lượng
                    distancePenaltyMeters = 100f;     // -100m cự ly
                    hitStopDuration = 0.10f;          // 0.10s khựng nhẹ
                    _knockbackDistance = 2.0f;        // Runner bị húc đẩy lùi 2.0m
                    _knockbackDuration = 0.5f;
                    _drivingSpeed = 7.0f;
                    _reverseWorldPeakSpeed = -87.0f;  // Thế giới cuộn ngược lùi đúng ~100m
                    _reverseWorldDuration = 1.8f;
                    break;

                case VehicleTier.PickupTruck:
                    obstacleName = "Xe Bán Tải";
                    obstacleType = ObstacleType.LowBarrier;
                    energyPenaltyPercent = 40f;       // -40% năng lượng
                    distancePenaltyMeters = 200f;     // -200m cự ly
                    hitStopDuration = 0.18f;          // 0.18s khựng/choáng
                    _knockbackDistance = 3.0f;        // Runner bị húc đẩy lùi 3.0m
                    _knockbackDuration = 0.6f;
                    _drivingSpeed = 6.2f;
                    _reverseWorldPeakSpeed = -130.0f; // Thế giới cuộn ngược lùi đúng ~200m
                    _reverseWorldDuration = 2.4f;
                    break;

                case VehicleTier.HeavyTruck:
                    obstacleName = "Xe Tải Hạng Nặng";
                    obstacleType = ObstacleType.HighBarrier;
                    energyPenaltyPercent = 60f;       // -60% năng lượng
                    distancePenaltyMeters = 400f;     // -400m cự ly
                    hitStopDuration = 0.25f;          // 0.25s cú tông cực mạnh
                    _knockbackDistance = 4.2f;        // Runner bị húc đẩy lùi 4.2m
                    _knockbackDuration = 0.7f;
                    _drivingSpeed = 5.2f;
                    _reverseWorldPeakSpeed = -196.0f; // Thế giới cuộn ngược lùi đúng ~400m
                    _reverseWorldDuration = 3.2f;
                    break;
            }
        }

        private void Awake()
        {
            ConfigureTier(_vehicleTier);

            _baseY = transform.position.y;
            _rumbleSeed = Random.Range(0f, 100f);

            // Tìm toàn bộ các cụm bánh xe con (PolygonCity chứa 'wheel', MegaCity chứa '_fl', '_fr', '_rl', '_rr')
            foreach (Transform child in transform)
            {
                string lowerName = child.name.ToLowerInvariant();
                bool isWheel = (lowerName.Contains("wheel") || 
                                lowerName.Contains("_fl") || lowerName.Contains("_fr") || 
                                lowerName.Contains("_rl") || lowerName.Contains("_rr")) && 
                               !lowerName.Contains("steering");
                if (isWheel)
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
            if (_isDeflectedByShield) return;

            if (_speedManager == null)
            {
                _speedManager = WorldSpeedManager.Instance ?? FindFirstObjectByType<WorldSpeedManager>();
            }

            // Nếu đã va chạm với Runner: Xe găm cố định ngay trước mặt Runner, không di chuyển theo thế giới
            if (_isPinnedToPlayer)
            {
                if (_pinnedPlayerTransform != null)
                {
                    Vector3 pinnedPos = transform.position;
                    pinnedPos.x = _pinnedPlayerTransform.position.x + _pinnedOffsetX;
                    if (_enableEngineRumble)
                    {
                        float rumbleOffset = Mathf.Sin((Time.time * _rumbleFrequency) + _rumbleSeed) * _rumbleAmplitude;
                        pinnedPos.y = _baseY + rumbleOffset;
                    }
                    transform.position = pinnedPos;
                    if (_rb != null) _rb.position = pinnedPos;
                }
                return;
            }

            // Nếu thế giới đang cuộn ngược (do va chạm đẩy lùi), các xe khác không bị kéo ngược lại
            // mà vẫn tiếp tục di chuyển bình thường theo tốc độ đường chuẩn
            float effectiveWorldSpeed = 10f;
            if (_speedManager != null)
            {
                effectiveWorldSpeed = _speedManager.CurrentSpeed < 0f ? _speedManager.BaseSpeed : _speedManager.CurrentSpeed;
            }

            float totalSpeed = effectiveWorldSpeed + _drivingSpeed;
            float dt = Time.deltaTime;

            if (_rb != null && _rb.isKinematic)
            {
                _rb.position += Vector3.left * (totalSpeed * dt);
                transform.position = _rb.position;
            }
            else
            {
                transform.position += Vector3.left * (totalSpeed * dt);
            }

            if (_enableWheelSpin && _wheelTransforms.Count > 0 && _drivingSpeed > 0.01f)
            {
                float angularSpeedDeg = (_drivingSpeed / Mathf.Max(0.1f, _wheelRadius)) * Mathf.Rad2Deg;
                float angleStep = angularSpeedDeg * dt;

                for (int i = 0; i < _wheelTransforms.Count; i++)
                {
                    if (_wheelTransforms[i] != null)
                    {
                        _wheelTransforms[i].Rotate(Vector3.right, angleStep, Space.Self);
                    }
                }
            }

            if (_enableEngineRumble)
            {
                float rumbleOffset = Mathf.Sin((Time.time * _rumbleFrequency) + _rumbleSeed) * _rumbleAmplitude;
                Vector3 currentPos = transform.position;
                currentPos.y = _baseY + rumbleOffset;
                transform.position = currentPos;
            }

            if (transform.position.x <= _despawnXThreshold)
            {
                Destroy(gameObject);
            }
        }

        public override void OnHitPlayer(GameObject player)
        {
            if (_isDeflectedByShield || IsShieldDeflected) return;

            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = false;
            }

            _drivingSpeed = 0f;
            _isPinnedToPlayer = true;
            _pinnedPlayerTransform = player != null ? player.transform : null;
            if (_pinnedPlayerTransform != null)
            {
                _pinnedOffsetX = Mathf.Clamp(transform.position.x - _pinnedPlayerTransform.position.x, 1.6f, 2.8f);
            }

            Destroy(gameObject, _reverseWorldDuration);
        }

        /// <summary>
        /// Được gọi khi xe đâm phải Khiên bảo vệ của Runner:
        /// Xe bị hất văng bốc lên không trung và dạt mạnh sang 2 bên lề đường,
        /// hoàn toàn không gây sát thương hay trừ điểm cho Runner.
        /// </summary>
        public void DeflectByShield(Vector3 runnerPosition)
        {
            if (_isDeflectedByShield) return;
            _isDeflectedByShield = true;
            IsShieldDeflected = true;
            _isPinnedToPlayer = false;
            _drivingSpeed = 0f;

            // Vô hiệu hóa script di chuyển thế giới nếu có để xe tự do bay văng
            var mover = GetComponent<MovingWorldObject>() ?? GetComponentInParent<MovingWorldObject>();
            if (mover != null) mover.enabled = false;

            // Vô hiệu hóa toàn bộ colliders để không cản trở Runner
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = false;
            }

            if (transform.root != null && transform.root != transform)
            {
                Collider[] rootColliders = transform.root.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < rootColliders.Length; i++)
                {
                    if (rootColliders[i] != null) rootColliders[i].enabled = false;
                }
            }

            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.detectCollisions = false;
            }

            StartCoroutine(DeflectFlyRoutine(runnerPosition));
        }

        private IEnumerator DeflectFlyRoutine(Vector3 runnerPosition)
        {
            // Xác định hướng văng sang trái (+Z) hoặc phải (-Z)
            float sideZ = (transform.position.z >= runnerPosition.z) ? 1f : -1f;
            if (Mathf.Abs(transform.position.z - runnerPosition.z) < 0.2f)
            {
                sideZ = Random.value > 0.5f ? 1f : -1f;
            }

            float vx = 8f;            // Hất mạnh theo chiều phía trước
            float vy = 15f;           // Bốc cao lên không trung
            float vz = sideZ * 18f;   // Hất văng dạt mạnh sang 2 bên lề đường
            float gravity = -28f;
            Vector3 rotAxis = new Vector3(Random.Range(240f, 420f), Random.Range(100f, 250f), sideZ * Random.Range(300f, 520f));

            Debug.Log($"[DrivingObstacleCar] Xe {gameObject.name} bị khiên hất văng sang {(sideZ > 0 ? "TRÁI" : "PHẢI")}!");

            float elapsed = 0f;
            float duration = 1.8f;

            while (elapsed < duration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                vy += gravity * dt;
                Vector3 moveDelta = new Vector3(vx, vy, vz) * dt;
                transform.position += moveDelta;
                if (_rb != null) _rb.position = transform.position;

                transform.Rotate(rotAxis * dt, Space.World);

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}

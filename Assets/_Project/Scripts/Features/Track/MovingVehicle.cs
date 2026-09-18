using System.Collections.Generic;
using UnityEngine;

namespace SteamRush.Track
{
    /// <summary>
    /// Điều khiển phương tiện giao thông di chuyển tự nhiên trên làn đường hậu cảnh.
    /// Tích hợp cơ chế Bám Đuôi An Toàn (Car-Following & Collision Avoidance):
    /// - Tự động phát hiện xe phía trước trên cùng làn để hãm tốc giữ khoảng cách.
    /// - Tuyệt đối không để xe đi sau đâm xuyên / chồng chéo lên đuôi xe đi trước.
    /// - Vận tốc tổng = (Hướng lái * Tốc độ xe) + (Tốc độ thế giới ngược chiều do runner chạy).
    /// </summary>
    public class MovingVehicle : MonoBehaviour
    {
        private static readonly List<MovingVehicle> s_activeVehicles = new List<MovingVehicle>();
        public static IReadOnlyList<MovingVehicle> ActiveVehicles => s_activeVehicles;

        [Header("Vehicle Dimension & Safety")]
        [SerializeField] private float _vehicleLength = 5.4f;      // Chiều dài thân xe trung bình ~5.2m - 5.5m
        [SerializeField] private float _minSafeGap = 3.5f;          // Khoảng cách an toàn tối thiểu (bumper-to-bumper)
        [SerializeField] private float _targetFollowGap = 7.0f;     // Khoảng cách bám đuôi lý tưởng khi chạy nối đuôi
        [SerializeField] private float _slowDownGap = 15.0f;        // Khoảng cách bắt đầu giảm tốc để nhường đường
        [SerializeField] private float _brakeDecelRate = 14.0f;     // Gia tốc phanh (m/s²)
        [SerializeField] private float _accelRate = 5.0f;           // Gia tốc tăng tốc (m/s²)

        private WorldSpeedManager _speedManager;
        private float _desiredSpeed = 14f;
        private float _currentDriveSpeed = 14f;
        private float _direction = 1f; // +1 = chạy sang phải (+X), -1 = chạy sang trái (-X)
        private float _minDespawnX = -35f;
        private float _maxDespawnX = 95f;
        private bool _isInitialized = false;

        public float Direction => _direction;
        public float CurrentDriveSpeed => _currentDriveSpeed;
        public float DesiredSpeed => _desiredSpeed;

        public void Initialize(WorldSpeedManager speedManager, float driveSpeed, float direction, float minDespawnX = -35f, float maxDespawnX = 95f)
        {
            _speedManager = speedManager;
            _desiredSpeed = Mathf.Max(1f, driveSpeed);
            _currentDriveSpeed = _desiredSpeed;
            _direction = direction >= 0f ? 1f : -1f;
            _minDespawnX = minDespawnX;
            _maxDespawnX = maxDespawnX;
            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (!s_activeVehicles.Contains(this))
            {
                s_activeVehicles.Add(this);
            }
        }

        private void OnDisable()
        {
            s_activeVehicles.Remove(this);
        }

        private void Awake()
        {
            if (_speedManager == null)
            {
                _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
        }

        [Header("Relative Speed Control")]
        [Tooltip("Độ chênh tốc độ tối thiểu so với Runner khi xe chạy cùng chiều (+X) để không bao giờ bị đi lùi.")]
        [SerializeField] private float _minSpeedAboveRunner = 3.5f;

        private void Update()
        {
            if (!_isInitialized) return;

            float worldSpeed = _speedManager != null ? _speedManager.CurrentSpeed : 0f;

            // Đảm bảo xe chạy cùng chiều (+X) luôn có tốc độ sàn lớn hơn tốc độ Runner
            float speedFloor = (_direction > 0f) ? (worldSpeed + _minSpeedAboveRunner) : 1f;
            float effectiveDesiredSpeed = Mathf.Max(_desiredSpeed, speedFloor);

            // 1. Tìm xe ngay phía trước trên cùng làn đường
            MovingVehicle leadCar = FindLeadVehicle();

            // 2. Tính toán tốc độ lái mục tiêu theo khoảng cách an toàn
            float targetSpeed = effectiveDesiredSpeed;
            if (leadCar != null)
            {
                float myX = transform.position.x;
                float leadX = leadCar.transform.position.x;
                float distanceAhead = (leadX - myX) * _direction;
                float gap = distanceAhead - _vehicleLength;

                if (gap <= _minSafeGap)
                {
                    // Khoảng cách sát nút: Đồng bộ tốc độ với xe trước để chạy song hành bám đuôi,
                    // tuyệt đối không giảm tốc về 0 hay 2m/s khiến xe bị trôi lùi!
                    targetSpeed = Mathf.Max(leadCar.CurrentDriveSpeed, speedFloor);

                    // Khóa chặn vị trí để triệt tiêu hoàn toàn khả năng đâm xuyên thân xe
                    float clampedX = leadX - (_direction * (_vehicleLength + _minSafeGap));
                    if ((_direction > 0f && transform.position.x > clampedX) ||
                        (_direction < 0f && transform.position.x < clampedX))
                    {
                        transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);
                    }
                }
                else if (gap < _targetFollowGap)
                {
                    // Trong vùng bám đuôi -> Giảm tốc mượt mà bám theo tốc độ xe đi trước
                    float ratio = Mathf.Clamp01((gap - _minSafeGap) / (_targetFollowGap - _minSafeGap));
                    float matchedSpeed = Mathf.Lerp(leadCar.CurrentDriveSpeed, effectiveDesiredSpeed, ratio);
                    targetSpeed = Mathf.Max(matchedSpeed, speedFloor);
                }
                else if (gap < _slowDownGap)
                {
                    // Bắt đầu tiếp cận xe trước -> Giới hạn tốc độ không vượt quá xe trước quá nhiều
                    targetSpeed = Mathf.Min(effectiveDesiredSpeed, Mathf.Max(leadCar.CurrentDriveSpeed + 2.0f, speedFloor));
                }
            }

            // 3. Nội suy tốc độ lái mượt mà (Phanh nhạy hơn tăng tốc)
            float stepRate = targetSpeed < _currentDriveSpeed ? _brakeDecelRate : _accelRate;
            _currentDriveSpeed = Mathf.MoveTowards(_currentDriveSpeed, targetSpeed, stepRate * Time.deltaTime);

            // Giới hạn sàn tuyệt đối: Khi chạy cùng chiều runner, luôn giữ _currentDriveSpeed >= speedFloor
            if (_direction > 0f)
            {
                _currentDriveSpeed = Mathf.Max(_currentDriveSpeed, speedFloor);
            }

            // 4. Di chuyển theo hệ quy chiếu thế giới cuộn:
            // Với _direction = +1: netVelocityX = _currentDriveSpeed - worldSpeed >= _minSpeedAboveRunner > 0 (Luôn tiến về trước!)
            float netVelocityX = (_direction * _currentDriveSpeed) - worldSpeed;
            transform.position += Vector3.right * (netVelocityX * Time.deltaTime);

            // 5. Tự hủy khi vượt quá ranh giới màn hình
            if (_direction > 0f && transform.position.x > _maxDespawnX)
            {
                Destroy(gameObject);
            }
            else if (_direction < 0f && transform.position.x < _minDespawnX)
            {
                Destroy(gameObject);
            }
        }

        private MovingVehicle FindLeadVehicle()
        {
            MovingVehicle closestLead = null;
            float minDistance = float.MaxValue;
            float myX = transform.position.x;
            float myZ = transform.position.z;

            for (int i = 0; i < s_activeVehicles.Count; i++)
            {
                MovingVehicle other = s_activeVehicles[i];
                if (other == null || other == this) continue;

                // Kiểm tra cùng hướng di chuyển
                if (Mathf.Sign(other._direction) != Mathf.Sign(_direction)) continue;

                // Kiểm tra cùng làn đường (sai lệch trục Z < 1.8m)
                if (Mathf.Abs(other.transform.position.z - myZ) > 1.8f) continue;

                // Kiểm tra có ở phía trước mũi xe theo chiều chuyển động không
                float distanceAhead = (other.transform.position.x - myX) * _direction;
                if (distanceAhead > 0.1f && distanceAhead < minDistance)
                {
                    minDistance = distanceAhead;
                    closestLead = other;
                }
            }

            return closestLead;
        }
    }
}

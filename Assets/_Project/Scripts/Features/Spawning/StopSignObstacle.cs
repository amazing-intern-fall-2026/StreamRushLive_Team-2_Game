using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Chướng ngại vật Bảng Dừng (Stop Sign - GDD v1.2 Mục 2):
    /// Chắn ngang đường chạy. Khi va chạm, ép vận tốc cuộn thế giới
    /// về 0 trong stopDuration (mặc định 1.0s, có thể tinh chỉnh tự do trong Inspector),
    /// sau đó thế giới tự tăng tốc mượt mà trở lại.
    /// Toàn bộ curve dừng và hồi tốc độ được phối hợp trực tiếp qua WorldSpeedManager.
    /// - Khi Runner đang dừng yên (CurrentSpeed ~ 0 và IsRecovering), TẤT CẢ biển STOP trên đường đều đứng yên tại chỗ.
    /// - Khi Runner/thế giới bị đẩy lùi (Knockback: CurrentSpeed < -0.05f), biển STOP vẫn cuộn lùi cùng thế giới.
    /// </summary>
    public class StopSignObstacle : ObstacleBase
    {
        [Header("Stop Sign Settings")]
        [Tooltip("Thời gian thế giới ngừng trôi khi va phải biển STOP (giây).")]
        [SerializeField] private float stopDuration = 1.0f;

        private MovingWorldObject _movingWorldObject;
        private WorldSpeedManager _speedManager;

        public float StopDuration
        {
            get => stopDuration;
            set => stopDuration = Mathf.Max(0.1f, value);
        }

        public override float RecoveryDuration => stopDuration > 0f ? stopDuration : recoveryDuration;

        /// <summary>
        /// Chỉ đứng yên khi thế giới đang ở pha dừng hẳn (CurrentSpeed ~ 0 và IsRecovering).
        /// Khi đang bị đẩy lùi (CurrentSpeed < -0.05f), trả về false để biển STOP cuộn lùi bình thường.
        /// </summary>
        public bool IsFrozenAtPlayer
        {
            get
            {
                if (_speedManager == null) _speedManager = WorldSpeedManager.Instance != null ? WorldSpeedManager.Instance : FindFirstObjectByType<WorldSpeedManager>();
                return _speedManager != null && _speedManager.IsRecovering && Mathf.Abs(_speedManager.CurrentSpeed) <= 0.05f;
            }
        }

        private void Awake()
        {
            obstacleType = ObstacleType.StopSign;
            _movingWorldObject = GetComponent<MovingWorldObject>();
        }

        private void Start()
        {
            if (_speedManager == null)
            {
                _speedManager = WorldSpeedManager.Instance != null ? WorldSpeedManager.Instance : FindFirstObjectByType<WorldSpeedManager>();
            }

            if (_movingWorldObject == null)
            {
                _movingWorldObject = GetComponent<MovingWorldObject>();
            }
        }

        private void Update()
        {
            // Khi Runner đang dừng yên do biển STOP, tất cả các biển STOP đều đứng yên
            if (IsFrozenAtPlayer) return;

            if (_movingWorldObject == null)
            {
                _movingWorldObject = GetComponent<MovingWorldObject>();
            }

            // Chỉ tự dịch chuyển nếu chưa có MovingWorldObject quản lý để tránh di chuyển x2 tốc độ
            if (_movingWorldObject == null)
            {
                float speed;
                if (_speedManager != null)
                {
                    if (_speedManager.CurrentSpeed < -0.05f)
                    {
                        // Đang bị đẩy lùi (Knockback), cuộn ngược cùng thế giới
                        speed = _speedManager.CurrentSpeed;
                    }
                    else if (_speedManager.CurrentSpeed > 0.05f && !_speedManager.IsRecovering)
                    {
                        speed = _speedManager.CurrentSpeed;
                    }
                    else
                    {
                        speed = _speedManager.BaseSpeed > 0f ? _speedManager.BaseSpeed : 10f;
                    }
                }
                else
                {
                    speed = 10f;
                }

                transform.position += Vector3.left * (speed * Time.deltaTime);
            }
        }

        public override void OnHitPlayer(GameObject player)
        {
        }
    }
}
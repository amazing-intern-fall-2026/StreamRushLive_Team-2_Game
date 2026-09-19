using UnityEngine;
using StreamRushLive.Features.Spawning;

namespace SteamRush.Track
{
    /// <summary>
    /// Gắn vào bất kỳ vật thể nào di chuyển cùng thế giới (chướng ngại vật, buff item...)
    /// để trôi ngược về phía Player theo trục -X với tốc độ từ WorldSpeedManager.
    /// Xử lý đặc thù cho biển STOP:
    /// - Khi bị đẩy lùi (Knockback: CurrentSpeed < -0.05f): Biển STOP cuộn lùi (+X) cùng thế giới.
    /// - Chỉ khi thế giới thực sự dừng hẳn (CurrentSpeed ~ 0 và IsRecovering): TẤT CẢ biển STOP mới đứng yên tại chỗ.
    /// - Các chướng ngại vật khác (rào chắn, bao rác, xe, đá lăn...) vẫn tiếp tục lao tới Runner khi Runner đứng yên.
    /// </summary>
    public class MovingWorldObject : MonoBehaviour
    {
        [SerializeField] private float _despawnXThreshold = -15f;

        private WorldSpeedManager _speedManager;
        private StopSignObstacle _stopSign;

        public void Initialize(WorldSpeedManager speedManager, float despawnXThreshold = -15f)
        {
            _speedManager = speedManager;
            _despawnXThreshold = despawnXThreshold;
        }

        private void Awake()
        {
            if (_stopSign == null)
            {
                _stopSign = GetComponent<StopSignObstacle>();
            }
        }

        private void Start()
        {
            if (_speedManager == null)
            {
                _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
            if (_stopSign == null)
            {
                _stopSign = GetComponent<StopSignObstacle>();
            }
        }

        private void Update()
        {
            // Nếu là biển STOP:
            // - Khi đang bị đẩy lùi (CurrentSpeed < -0.05f): Không đóng băng, để biển STOP cuộn lùi cùng thế giới.
            // - Chỉ khi Runner đang dừng yên (CurrentSpeed ~ 0 và IsRecovering): Biển STOP mới đứng yên tại chỗ.
            if (_stopSign != null)
            {
                if (_speedManager != null && _speedManager.IsRecovering && Mathf.Abs(_speedManager.CurrentSpeed) <= 0.05f)
                {
                    return;
                }
            }

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
                    // Tốc độ bình thường (chạy, sprint, slide)
                    speed = _speedManager.CurrentSpeed;
                }
                else
                {
                    // Khi runner đang dừng yên (CurrentSpeed ~ 0 hoặc đang trong thời gian dừng của biển STOP),
                    // các chướng ngại vật khác (rào chắn, bao rác, xe, đá lăn...) vẫn tiếp tục lao tới vị trí Runner theo BaseSpeed.
                    speed = _speedManager.BaseSpeed > 0f ? _speedManager.BaseSpeed : 10f;
                }
            }
            else
            {
                speed = 10f;
            }

            float step = speed * Time.deltaTime;
            transform.position += Vector3.left * step;

            if (transform.position.x <= _despawnXThreshold)
            {
                Destroy(gameObject);
            }
        }
    }
}

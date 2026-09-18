using System.Collections;
using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Chướng ngại vật Bảng Dừng (Stop Sign - GDD v1.2 Mục 2):
    /// Chắn ngang đường chạy. Khi va chạm (hoặc chạm Trigger), ép vận tốc cuộn thế giới
    /// về 0 trong stopDuration (mặc định 1.0s), sau đó thế giới tự tăng tốc mượt mà trở lại.
    /// </summary>
    public class StopSignObstacle : ObstacleBase
    {
        [Header("Stop Sign Settings")]
        [Tooltip("Thời gian thế giới ngừng trôi khi va phải biển STOP.")]
        [SerializeField] private float stopDuration = 1.0f;

        private WorldSpeedManager _speedManager;
        private MovingWorldObject _movingWorldObject;

        private void Awake()
        {
            obstacleType = ObstacleType.StopSign;
            if (distancePenaltyMeters <= 0f)
            {
                distancePenaltyMeters = 8f;
            }
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
            // Chỉ tự dịch chuyển nếu chưa có MovingWorldObject quản lý để tránh di chuyển x2 tốc độ
            if (_movingWorldObject == null)
            {
                float speed = _speedManager != null ? _speedManager.CurrentSpeed : 0f;
                transform.position += Vector3.left * (speed * Time.deltaTime);
            }
        }

        public override void OnHitPlayer(GameObject player)
        {
            if (_speedManager != null)
            {
                StartCoroutine(HandleStopAndResume());
            }
        }

        private IEnumerator HandleStopAndResume()
        {
            float normalSpeed = _speedManager.CurrentSpeed > 0f ? _speedManager.CurrentSpeed : 10f;
            _speedManager.CurrentSpeed = 0f;

            yield return new WaitForSeconds(stopDuration);

            // Hồi phục gia tốc thế giới cuộn từ 0 lên lại tốc độ chuẩn trong 1.0s
            float elapsed = 0f;
            float recoveryDuration = 1.0f;
            while (elapsed < recoveryDuration)
            {
                elapsed += Time.deltaTime;
                if (_speedManager != null)
                {
                    _speedManager.CurrentSpeed = Mathf.Lerp(0f, normalSpeed, elapsed / recoveryDuration);
                }
                yield return null;
            }

            if (_speedManager != null)
            {
                _speedManager.CurrentSpeed = normalSpeed;
            }
        }
    }
}
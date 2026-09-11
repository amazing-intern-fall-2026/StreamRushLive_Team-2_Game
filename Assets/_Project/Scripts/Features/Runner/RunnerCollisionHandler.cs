namespace SteamRush.Features.Runner
{
    using System.Collections;
    using UnityEngine;
    using SteamRush.Core;

    /// Chịu trách nhiệm DUY NHẤT: phát hiện va chạm vật cản và điều phối phản ứng — đóng băng
    /// khung hình ngắn (hit-stop), đẩy lùi nhân vật, rồi để tốc độ thế giới hồi phục dần từ 0.
    /// KHÔNG còn Game Over/reload scene — cơ chế mới cho phép người chơi "vấp rồi đứng dậy chạy
    /// tiếp" thay vì kết thúc lượt chơi ngay lập tức.

    [RequireComponent(typeof(RunnerController))]
    public class RunnerCollisionHandler : MonoBehaviour
    {
        [SerializeField] private string _obstacleTag = "Obstacle";

        [Header("Đóng băng khung hình (Hit-stop)")]
        [Tooltip("Thời gian (giây thực, không bị ảnh hưởng bởi Time.timeScale) đóng băng toàn màn hình khi va chạm.")]
        [SerializeField] private float _hitStopDuration = 0.15f;

        private RunnerController _controller;
        private bool _isHandlingHit;

        private void Awake()
        {
            _controller = GetComponent<RunnerController>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_isHandlingHit || !collision.gameObject.CompareTag(_obstacleTag)) return;

            StartCoroutine(HandleObstacleHit());
        }

        private IEnumerator HandleObstacleHit()
        {
            _isHandlingHit = true;

            // TODO (Animation/Audio): người phụ trách Animation/Audio gọi hiệu ứng "vấp ngã" +
            // âm thanh va chạm tại đây.
            Debug.Log("[RunnerCollisionHandler] Va chạm vật cản! Đóng băng khung hình, đẩy lùi, hồi phục tốc độ...");

            _controller.ApplyKnockback();

            // Time.timeScale = 0 khiến Time.deltaTime = 0 toàn game, nên GameSpeedController và
            // mọi hệ thống dựa trên Time.deltaTime tự động "đứng hình" theo — không cần thêm
            // logic đóng băng riêng ở nơi khác. WaitForSecondsRealtime không bị ảnh hưởng bởi
            // timeScale nên coroutine này vẫn đếm giờ đúng trong lúc đóng băng.
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(_hitStopDuration);
            Time.timeScale = 1f;

            if (GameSpeedController.Instance != null)
            {
                GameSpeedController.Instance.TriggerRecovery();
            }

            // Đợi nhân vật đẩy lùi xong (đứng dậy) rồi mới cho phép va chạm tiếp theo được xử lý.
            yield return new WaitUntil(() => !_controller.IsKnockingBack);

            _isHandlingHit = false;
        }
    }
}
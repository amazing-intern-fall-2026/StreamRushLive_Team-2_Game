namespace SteamRush.Features.Runner
{
    using System.Collections;
    using UnityEngine;
    using SteamRush.Core;
    using SteamRush.Track;
    using StreamRushLive.Features.Spawning;
    using SteamRush.Features.UI;

    /// <summary>
    /// Chịu trách nhiệm phát hiện va chạm TRIGGER giữa Runner với vật cản và vật phẩm:
    /// - Sử dụng cơ chế Trigger (OnTriggerEnter) giúp Runner không bị kẹt hay khựng vật lý cứng.
    /// - Đóng băng khung hình ngắn (hit-stop 0.15s).
    /// - Đẩy lùi nhân vật (knockback easing).
    /// - Phạt trừ năng lượng (-25%) và hiển thị Status Popup "Vấp ngã!".
    /// - Tự động hồi phục tốc độ thế giới (WorldSpeedManager/GameSpeedController).
    /// - Nhặt Buff Item dạng Trigger hồi +20% năng lượng.
    /// </summary>
    [RequireComponent(typeof(RunnerController))]
    public class RunnerCollisionHandler : MonoBehaviour
    {
        [SerializeField] private string _obstacleTag = "Obstacle";

        [Header("Hit-Stop Settings")]
        [Tooltip("Real-time hit-stop freeze duration on collision.")]
        [SerializeField] private float _hitStopDuration = 0.15f;

        [Header("Energy Penalty Settings")]
        [SerializeField] private float _energyPenaltyPercent = 25f;

        [Header("Invulnerability Settings")]
        [Tooltip("Duration player remains as trigger to pass through obstacle.")]
        [SerializeField] private float _invulnerabilityDuration = 0.8f;

        private RunnerController _controller;
        private Renderer[] _renderers;
        private bool _isHandlingHit;

        public bool IsHandlingHit => _isHandlingHit;

        private void Awake()
        {
            _controller = GetComponent<RunnerController>();
            _renderers = GetComponentsInChildren<Renderer>();
        }

        /// <summary>
        /// Điểm nhận va chạm: Hỗ trợ cả Trigger lẫn Collision vật lý.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            HandleInteraction(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleInteraction(collision.gameObject);
        }

        private void HandleInteraction(GameObject obj)
        {
            // 1. Kiểm tra nếu là Item (kế thừa ItemBase)
            ItemBase item = obj.GetComponentInParent<ItemBase>();
            if (item != null)
            {
                item.Collect(gameObject);
                return;
            }

            // Fallback nhặt Buff Item qua Tag nếu chưa gắn component ItemBase
            if (obj.CompareTag("Buff") || obj.name.Contains("Buff"))
            {
                EnergySystem energy = FindFirstObjectByType<EnergySystem>();
                if (energy != null)
                {
                    energy.AddEnergy(20f);
                }
                HUDManager hud = FindFirstObjectByType<HUDManager>();
                hud?.ShowStatusPopup("Năng lượng +20%", true);
                Destroy(obj);
                return;
            }

            // 2. Xử lý va chạm vật cản: Nhận diện ObstacleBase hoặc theo Tag
            if (_isHandlingHit) return;

            ObstacleBase obstacle = obj.GetComponentInParent<ObstacleBase>();
            if (obstacle != null)
            {
                StartCoroutine(HandleObstacleHit(obstacle));
                return;
            }

            if (obj.CompareTag(_obstacleTag) || obj.name.Contains("Barrier") || obj.name.Contains("Obstacle"))
            {
                StartCoroutine(HandleObstacleHit(null));
            }
        }

        public void HandleObstacleHitFromSource(ObstacleBase obstacle)
        {
            if (_isHandlingHit) return;
            StartCoroutine(HandleObstacleHit(obstacle));
        }

        private IEnumerator HandleObstacleHit(ObstacleBase obstacle)
        {
            _isHandlingHit = true;

            // Lấy thông số phạt cụ thể từ chính Obstacle nếu có, ngược lại dùng mặc định
            float penalty = obstacle != null ? obstacle.EnergyPenaltyPercent : _energyPenaltyPercent;
            float hitStop = obstacle != null ? obstacle.HitStopDuration : _hitStopDuration;
            string obstacleTitle = obstacle != null ? obstacle.ObstacleName : "Vật cản";

            // Chuyển chính Player thành Trigger để vật cản xuyên qua mà không xô đẩy
            _controller.SetTriggerMode(true);

            // Hiển thị Status Popup debuff
            HUDManager hud = FindFirstObjectByType<HUDManager>();
            if (obstacle != null && obstacle.DistancePenaltyMeters > 0f)
            {
                hud?.ShowStatusPopup($"Vấp ngã! -{penalty:F0}% NL (-{obstacle.DistancePenaltyMeters:F0}m)", false);
            }
            else if (penalty > 0f)
            {
                hud?.ShowStatusPopup($"Vấp ngã! -{penalty:F0}% Năng lượng", false);
            }
            else
            {
                hud?.ShowStatusPopup("Vấp ngã!", false);
            }

            // Trừ năng lượng
            EnergySystem energySystem = FindFirstObjectByType<EnergySystem>();
            if (energySystem != null)
            {
                energySystem.AddEnergy(-penalty);
            }

            // Đẩy lùi nhân vật
            _controller.ApplyKnockback();

            // Đóng băng khung hình (Hit-stop)
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitStop);
            Time.timeScale = 1f;

            // Kích hoạt hồi phục tốc độ trên GameSpeedController (nếu có)
            if (GameSpeedController.Instance != null)
            {
                GameSpeedController.Instance.TriggerRecovery();
            }

            // Kích hoạt hồi phục tốc độ trên WorldSpeedManager (nếu có)
            WorldSpeedManager speedManager = FindFirstObjectByType<WorldSpeedManager>();
            if (speedManager != null)
            {
                float originalSpeed = speedManager.CurrentSpeed;
                speedManager.CurrentSpeed = originalSpeed * 0.3f;
                StartCoroutine(RecoverWorldSpeed(speedManager, originalSpeed, 1.5f));
            }

            // Hiệu ứng nhấp nháy miễn nhiễm và giữ Player ở trạng thái Trigger trong khi vật cản trôi qua
            float elapsed = 0f;
            while (elapsed < _invulnerabilityDuration || _controller.IsKnockingBack)
            {
                elapsed += Time.deltaTime;
                SetRenderersVisible(elapsed % 0.15f < 0.075f);
                yield return null;
            }

            SetRenderersVisible(true);

            // Chuyển Player trở lại Collider vật lý bình thường
            _controller.SetTriggerMode(false);
            _isHandlingHit = false;
        }

        private void SetRenderersVisible(bool visible)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = visible;
                }
            }
        }

        private IEnumerator RecoverWorldSpeed(WorldSpeedManager speedManager, float targetSpeed, float duration)
        {
            float elapsed = 0f;
            float startSpeed = speedManager.CurrentSpeed;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (speedManager != null)
                {
                    speedManager.CurrentSpeed = Mathf.Lerp(startSpeed, targetSpeed, elapsed / duration);
                }
                yield return null;
            }
            if (speedManager != null)
            {
                speedManager.CurrentSpeed = targetSpeed;
            }
        }
    }
}
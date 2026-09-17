namespace SteamRush.Features.Runner
{
    using System.Collections;
    using UnityEngine;
    using SteamRush.Track;
    using StreamRushLive.Features.Spawning;
    using SteamRush.Features.UI;

    /// <summary>
    /// Chịu trách nhiệm phát hiện va chạm TRIGGER giữa Runner với vật cản và vật phẩm:
    /// - Sử dụng cơ chế Trigger (OnTriggerEnter) giúp Runner không bị kẹt hay khựng vật lý cứng.
    /// - Đóng băng khung hình ngắn (hit-stop 0.15s).
    /// - Phạt trừ năng lượng và hiển thị Status Popup "Vấp ngã!".
    /// - Tự động hồi phục tốc độ thế giới qua WorldSpeedManager.TriggerRecovery().
    /// - Nhặt Buff Item dạng Trigger hồi +20% năng lượng.
    ///
    /// LƯU Ý: Không còn dùng GameSpeedController (đã xoá khỏi project) và không còn tự Lerp
    /// CurrentSpeed bằng coroutine riêng — toàn bộ curve hồi tốc độ (giảm về 0 -> giữ 0 -> tăng
    /// lại) nằm gọn trong WorldSpeedManager, chỉ cần gọi TriggerRecovery().
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

        [Header("World Recovery Settings")]
        [Tooltip("Thời gian hồi phục World Speed mặc định nếu obstacle không chỉ định riêng. GDD dao động 1.2s-1.8s tuỳ loại.")]
        [SerializeField] private float _defaultWorldRecoveryDuration = 1.2f;

        [Header("Invulnerability Settings")]
        [Tooltip("Duration player remains as trigger to pass through obstacle.")]
        [SerializeField] private float _invulnerabilityDuration = 0.8f;

        private RunnerController _controller;
        private Renderer[] _renderers;
        private bool _isHandlingHit;
        private bool _isHyperDashActive;

        public bool IsHandlingHit => _isHandlingHit;
        public bool IsHyperDashActive => _isHyperDashActive;

        private void Awake()
        {
            _controller = GetComponent<RunnerController>();
            _renderers = GetComponentsInChildren<Renderer>();

            if (!TryGetComponent<RunnerItemEffects>(out _))
            {
                gameObject.AddComponent<RunnerItemEffects>();
            }
        }

        /// <summary>
        /// Bật hoặc tắt trạng thái Hyper Dash.
        /// Khi Hyper Dash hoạt động:
        /// - Tắt va chạm layer giữa Layer Player và Layer Obstacle (Physics.IgnoreLayerCollision).
        /// - Đổi collider sang Trigger mode.
        /// - Runner đi xuyên và quét phá hủy vật cản.
        /// </summary>
        public void SetHyperDashState(bool isActive)
        {
            _isHyperDashActive = isActive;

            int playerLayer = LayerMask.NameToLayer("Player");
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");

            if (playerLayer != -1 && obstacleLayer != -1)
            {
                Physics.IgnoreLayerCollision(playerLayer, obstacleLayer, isActive);
            }
        }

        private void Update()
        {
            if (_isHyperDashActive)
            {
                ClearHyperDashObstacles();
            }
        }

        private void ClearHyperDashObstacles()
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer == -1) return;

            Vector3 center = transform.position + Vector3.up * 1f;
            Vector3 halfExtents = new Vector3(1.2f, 1.2f, 1.2f);

            Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation, 1 << obstacleLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                ObstacleBase obstacle = hits[i].GetComponentInParent<ObstacleBase>();
                if (obstacle != null)
                {
                    Destroy(obstacle.gameObject);
                }
                else if (hits[i].CompareTag(_obstacleTag) || hits[i].name.Contains("Barrier") || hits[i].name.Contains("Obstacle"))
                {
                    Destroy(hits[i].gameObject);
                }
            }
        }

        private void OnDisable()
        {
            ResetHyperDashLayerCollision();
        }

        private void OnDestroy()
        {
            ResetHyperDashLayerCollision();
        }

        private void ResetHyperDashLayerCollision()
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (playerLayer != -1 && obstacleLayer != -1)
            {
                Physics.IgnoreLayerCollision(playerLayer, obstacleLayer, false);
            }
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
            if (_isHyperDashActive)
            {
                ObstacleBase hyperDashObstacle = obj.GetComponentInParent<ObstacleBase>();

                if (hyperDashObstacle != null)
                {
                    Destroy(hyperDashObstacle.gameObject);
                    return;
                }

                // Fallback cho vật cản chưa có ObstacleBase nhưng vẫn mang Tag/tên vật cản.
                if (obj.CompareTag(_obstacleTag)
                    || obj.name.Contains("Barrier")
                    || obj.name.Contains("Obstacle"))
                {
                    Destroy(obj);
                    return;
                }
            }
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
                if (obstacle.HasCollided) return;

                if (TryConsumeShield())
                {
                    Destroy(obstacle.gameObject);
                    return;
                }

                obstacle.TriggerHit(gameObject);
                return;
            }

            if (obj.CompareTag(_obstacleTag) || obj.name.Contains("Barrier") || obj.name.Contains("Obstacle"))
            {
                if (TryConsumeShield())
                {
                    Destroy(obj);
                    return;
                }
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

            // Bỏ qua va chạm vật lý giữa Player và vật cản này để không xô đẩy hoặc kích hoạt lại
            if (obstacle != null)
            {
                Collider[] obsColliders = obstacle.GetComponentsInChildren<Collider>();
                Collider[] playerColliders = GetComponentsInChildren<Collider>();
                for (int i = 0; i < obsColliders.Length; i++)
                {
                    for (int j = 0; j < playerColliders.Length; j++)
                    {
                        if (obsColliders[i] != null && playerColliders[j] != null)
                        {
                            Physics.IgnoreCollision(playerColliders[j], obsColliders[i], true);
                        }
                    }
                }
            }

            // Lấy thông số phạt cụ thể từ chính Obstacle nếu có, ngược lại dùng mặc định
            float penalty = obstacle != null ? obstacle.EnergyPenaltyPercent : _energyPenaltyPercent;
            float hitStop = obstacle != null ? obstacle.HitStopDuration : _hitStopDuration;

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

            // Đóng băng khung hình (Hit-stop)
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitStop);
            Time.timeScale = 1f;

            // Kích hoạt hồi phục tốc độ thế giới — curve 3 pha (giảm về 0 -> giữ 0 -> tăng lại)
            // nằm gọn trong WorldSpeedManager, không cần tự Lerp thủ công ở đây nữa.
            WorldSpeedManager speedManager = FindFirstObjectByType<WorldSpeedManager>();
            speedManager?.TriggerRecovery(_defaultWorldRecoveryDuration);

            // Hiệu ứng nhấp nháy miễn nhiễm và giữ Player ở trạng thái Trigger trong khi vật cản trôi qua
            float elapsed = 0f;
            while (elapsed < _invulnerabilityDuration)
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

        /// <summary>
        /// Kiểm tra Runner có Shield hay không và tiêu hao Shield nếu có.
        /// </summary>
        private bool TryConsumeShield()
        {
            RunnerItemEffects itemEffects = GetComponent<RunnerItemEffects>();

            if (itemEffects == null)
            {
                itemEffects = GetComponentInParent<RunnerItemEffects>();
            }

            if (itemEffects == null)
            {
                return false;
            }

            if (!itemEffects.IsShieldActive)
            {
                return false;
            }

            bool blocked = itemEffects.ConsumeShield();

            if (blocked)
            {
                Debug.Log("[RunnerCollisionHandler] Shield đã chặn va chạm.");

                HUDManager hud = FindFirstObjectByType<HUDManager>();
                hud?.ShowStatusPopup("Shield chặn va chạm!", true);
            }

            return blocked;
        }
    }
}
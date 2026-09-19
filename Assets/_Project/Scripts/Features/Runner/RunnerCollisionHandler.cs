namespace SteamRush.Features.Runner
{
    using System.Collections;
    using UnityEngine;
    using SteamRush.Track;
    using StreamRushLive.Features.Spawning;
    using SteamRush.Features.UI;

    /// <summary>
    /// Chịu trách nhiệm xử lý tương tác giữa Runner với vật cản và vật phẩm.
    ///
    /// Gameplay hiện tại:
    /// - Nếu có Shield: Shield chặn đúng 1 lần va chạm và không mất máu.
    /// - Nếu không có Shield: Runner mất 1 tim thông qua RunnerHealthSystem.
    /// - RunnerHealthSystem chịu trách nhiệm quản lý 2 giây bất tử sau damage.
    /// - Có hit-stop ngắn khi va chạm.
    /// - World Speed Recovery vẫn được xử lý thông qua WorldSpeedManager.
    /// </summary>
    [RequireComponent(typeof(RunnerController))]
    public class RunnerCollisionHandler : MonoBehaviour
    {
        [SerializeField] private string _obstacleTag = "Obstacle";

        [Header("Hit-Stop Settings")]
        [Tooltip("Real-time hit-stop freeze duration on collision.")]
        [SerializeField] private float _hitStopDuration = 0.15f;

        [Header("World Recovery Settings")]
        [Tooltip("Thời gian hồi phục World Speed mặc định.")]
        [SerializeField] private float _defaultWorldRecoveryDuration = 1.2f;

        private RunnerController _controller;
        private RunnerHealthSystem _healthSystem;

        private bool _isHandlingHit;
        private bool _isHyperDashActive;

        public bool IsHandlingHit => _isHandlingHit;
        public bool IsHyperDashActive => _isHyperDashActive;

        private void Awake()
        {
            _controller = GetComponent<RunnerController>();
            _healthSystem = GetComponent<RunnerHealthSystem>();

            if (_healthSystem == null)
            {
                _healthSystem = GetComponentInParent<RunnerHealthSystem>();
            }
        }

        /// <summary>
        /// Bật hoặc tắt trạng thái Hyper Dash.
        /// Khi Hyper Dash hoạt động, Runner trở thành Trigger
        /// để có thể đi xuyên qua vật cản.
        /// </summary>
        public void SetHyperDashState(bool isActive)
        {
            _isHyperDashActive = isActive;

            if (_controller != null)
            {
                _controller.SetHyperDashTriggerMode(isActive);
            }
        }

        /// <summary>
        /// Điểm nhận tương tác Trigger.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            HandleInteraction(other.gameObject);
        }

        /// <summary>
        /// Điểm nhận tương tác Collision.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            HandleInteraction(collision.gameObject);
        }

        private void HandleInteraction(GameObject obj)
        {
            // =========================================================
            // 1. HYPER DASH
            // =========================================================

            if (_isHyperDashActive)
            {
                ObstacleBase hyperDashObstacle =
                    obj.GetComponentInParent<ObstacleBase>();

                if (hyperDashObstacle != null)
                {
                    Destroy(hyperDashObstacle.gameObject);
                    return;
                }

                // Fallback cho vật cản chưa có ObstacleBase.
                if (obj.CompareTag(_obstacleTag)
                    || obj.name.Contains("Barrier")
                    || obj.name.Contains("Obstacle"))
                {
                    Destroy(obj);
                    return;
                }
            }

            // =========================================================
            // 2. ITEM
            // =========================================================

            ItemBase item = obj.GetComponentInParent<ItemBase>();

            if (item != null)
            {
                item.Collect(gameObject);
                return;
            }

            // Fallback Buff Item cũ.
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

            // =========================================================
            // 3. OBSTACLE
            // =========================================================

            if (_isHandlingHit)
            {
                return;
            }

            ObstacleBase obstacle =
                obj.GetComponentInParent<ObstacleBase>();

            if (obstacle != null)
            {
                // Shield được ưu tiên kiểm tra trước Health.
                if (TryConsumeShield())
                {
                    Destroy(obstacle.gameObject);
                    return;
                }

                // ObstacleBase đã xử lý va chạm này rồi.
                if (!obstacle.HasCollided)
                {
                    obstacle.TriggerHit(gameObject);
                }
                else
                {
                    StartCoroutine(HandleObstacleHit(obstacle));
                }

                return;
            }

            // Fallback cho obstacle chưa có ObstacleBase.
            if (obj.CompareTag(_obstacleTag)
                || obj.name.Contains("Barrier")
                || obj.name.Contains("Obstacle"))
            {
                if (TryConsumeShield())
                {
                    Destroy(obj);
                    return;
                }

                StartCoroutine(HandleObstacleHit(null));
            }
        }

        /// <summary>
        /// Được ObstacleBase gọi sau khi obstacle xác nhận Runner bị hit.
        /// </summary>
        public void HandleObstacleHitFromSource(ObstacleBase obstacle)
        {
            if (_isHandlingHit)
            {
                return;
            }

            StartCoroutine(HandleObstacleHit(obstacle));
        }

        /// <summary>
        /// Xử lý một lần Runner bị vật cản đánh trúng.
        ///
        /// Damage thực tế được chuyển sang RunnerHealthSystem.
        /// Không còn trừ Energy khi va chạm.
        /// </summary>
        private IEnumerator HandleObstacleHit(ObstacleBase obstacle)
        {
            _isHandlingHit = true;

            float hitStop =
                obstacle != null
                    ? obstacle.HitStopDuration
                    : _hitStopDuration;

            // =========================================================
            // 1. Kiểm tra Health System
            // =========================================================

            if (_healthSystem == null)
            {
                _healthSystem =
                    GetComponent<RunnerHealthSystem>();

                if (_healthSystem == null)
                {
                    _healthSystem =
                        GetComponentInParent<RunnerHealthSystem>();
                }
            }

            // =========================================================
            // 2. Nếu Health đang bất tử thì không nhận damage
            // =========================================================

            if (_healthSystem != null &&
                _healthSystem.IsInvulnerable)
            {
                _isHandlingHit = false;
                yield break;
            }

            // =========================================================
            // 3. Chuyển Runner thành Trigger
            // =========================================================

            _controller.SetTriggerMode(true);

            // =========================================================
            // 4. Hiển thị thông báo bị va chạm
            // =========================================================

            HUDManager hud = FindFirstObjectByType<HUDManager>();

            hud?.ShowStatusPopup("Vấp ngã! -1 tim", false);

            // =========================================================
            // 5. Trừ 1 tim
            // =========================================================

            if (_healthSystem != null)
            {
                _healthSystem.TakeDamage(1);
            }
            else
            {
                Debug.LogWarning(
                    "[RunnerCollisionHandler] Không tìm thấy RunnerHealthSystem."
                );
            }

            // =========================================================
            // 6. Hit-stop
            // =========================================================

            Time.timeScale = 0f;

            yield return new WaitForSecondsRealtime(hitStop);

            Time.timeScale = 1f;

            // =========================================================
            // 7. World Speed Recovery
            // =========================================================

            WorldSpeedManager speedManager =
                FindFirstObjectByType<WorldSpeedManager>();

            speedManager?.TriggerRecovery(
                _defaultWorldRecoveryDuration
            );

            // =========================================================
            // 8. Chờ Health xử lý trạng thái bất tử 2 giây
            //
            // RunnerHealthSystem chịu trách nhiệm:
            // - IsInvulnerable
            // - Blink
            // - thời gian 2 giây
            //
            // Ở đây chỉ cần giữ Runner ở Trigger trong thời gian
            // xử lý va chạm hiện tại để obstacle đi xuyên qua.
            // =========================================================

            float triggerDuration = 0.2f;
            float elapsed = 0f;

            while (elapsed < triggerDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // =========================================================
            // 9. Trả Collider về trạng thái bình thường
            // =========================================================

            _controller.SetTriggerMode(false);

            _isHandlingHit = false;
        }

        /// <summary>
        /// Kiểm tra Runner có Shield hay không.
        /// Nếu có, Shield sẽ bị tiêu hao và va chạm không gây mất máu.
        /// </summary>
        private bool TryConsumeShield()
        {
            RunnerItemEffects itemEffects =
                GetComponent<RunnerItemEffects>();

            if (itemEffects == null)
            {
                itemEffects =
                    GetComponentInParent<RunnerItemEffects>();
            }

            if (itemEffects == null)
            {
                return false;
            }

            if (!itemEffects.IsShieldActive)
            {
                return false;
            }

            bool blocked =
                itemEffects.ConsumeShield();

            if (blocked)
            {
                Debug.Log(
                    "[RunnerCollisionHandler] Shield đã chặn va chạm."
                );

                HUDManager hud =
                    FindFirstObjectByType<HUDManager>();

                hud?.ShowStatusPopup(
                    "Shield chặn va chạm!",
                    true
                );
            }

            return blocked;
        }
    }
}
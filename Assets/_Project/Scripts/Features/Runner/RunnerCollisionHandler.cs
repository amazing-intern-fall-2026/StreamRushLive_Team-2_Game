namespace SteamRush.Features.Runner
{
    using System.Collections;
    using UnityEngine;
    using SteamRush.Track;
    using StreamRushLive.Features.Spawning;
    using SteamRush.Features.UI;

    /// <summary>
    /// Chịu trách nhiệm xử lý tương tác giữa Runner với vật cản và vật phẩm (GDD v1.2):
    /// - Không còn cơ chế máu/tim (Runner không chết vì va chạm).
    /// - Nếu có Shield: Shield chặn đúng 1 lần va chạm và huỷ vật cản.
    /// - Nếu không có Shield:
    ///   + Bật Trigger Mode để vật cản xuyên qua mà không xô lệch vật lý.
    ///   + Knockback đẩy lùi Runner về sau theo đường cong Ease Out Quad.
    ///   + Phạt trừ quãng đường trên thanh tiến trình (-10m).
    ///   + Phạt trừ năng lượng (-25%).
    ///   + Đóng băng khung hình ngắn (hit-stop 0.15s).
    ///   + Hồi phục tốc độ thế giới (WorldSpeedManager.TriggerRecovery).
    ///   + Miễn nhiễm và nhấp nháy i-Frames 0.8s.
    /// </summary>
    [RequireComponent(typeof(RunnerController))]
    public class RunnerCollisionHandler : MonoBehaviour
    {
        [SerializeField] private string _obstacleTag = "Obstacle";

        [Header("Hit-Stop Settings")]
        [Tooltip("Real-time hit-stop freeze duration on collision.")]
        [SerializeField] private float _hitStopDuration = 0.15f;

        [Header("Penalty Settings (GDD v1.2)")]
        [Tooltip("Phần trăm năng lượng bị trừ khi va chạm.")]
        [SerializeField] private float _energyPenaltyPercent = 25f;

        [Tooltip("Quãng đường phạt bị đẩy lùi (mét).")]
        [SerializeField] private float _defaultDistancePenalty = 10f;

        [Header("Invulnerability Settings")]
        [Tooltip("Thời gian miễn nhiễm (nhấp nháy + trigger mode) để vật cản đi qua.")]
        [SerializeField] private float _invulnerabilityDuration = 0.8f;

        private RunnerController _controller;
        private ChatLaneRunnerController _chatLaneRunner;
        private Renderer[] _renderers;

        private bool _isHandlingHit;
        private bool _isHyperDashActive;

        public bool IsHandlingHit => _isHandlingHit;
        public bool IsHyperDashActive => _isHyperDashActive;

        private void Awake()
        {
            _controller = GetComponent<RunnerController>();
            _chatLaneRunner = GetComponent<ChatLaneRunnerController>();
            _renderers = GetComponentsInChildren<Renderer>();
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
            // 1. Hyper Dash
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

            // 2. Item
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

            // 3. Obstacle
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

            // Fallback cho obstacle chưa có ObstacleBase hoặc nhận diện xe DrivingObstacleCar
            StreamRushLive.Features.Spawning.DrivingObstacleCar drivingCar = obj.GetComponentInParent<StreamRushLive.Features.Spawning.DrivingObstacleCar>();
            if (drivingCar != null || obj.CompareTag(_obstacleTag)
                || obj.transform.root.CompareTag(_obstacleTag)
                || obj.name.Contains("Barrier")
                || obj.name.Contains("Obstacle")
                || obj.name.Contains("Car"))
            {
                if (TryConsumeShield())
                {
                    if (drivingCar != null) Destroy(drivingCar.gameObject);
                    else Destroy(obj.transform.root.gameObject);
                    return;
                }

                if (drivingCar != null)
                {
                    drivingCar.OnHitPlayer(gameObject);
                }

                StartCoroutine(HandleObstacleHit(drivingCar));
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
        /// Xử lý một lần Runner bị vật cản đánh trúng (GDD v1.2 / Prototype 3-Lane):
        /// - Phạt trừ cự ly tiến trình (-15m) & năng lượng (-25%).
        /// - Reverse World Knockback (-8.5 m/s) & đẩy lùi Runner về sau.
        /// - i-Frames 2.0s nhấp nháy bất tử bảo vệ Runner.
        /// </summary>
        private IEnumerator HandleObstacleHit(ObstacleBase obstacle)
        {
            _isHandlingHit = true;

            float penalty = obstacle != null ? obstacle.EnergyPenaltyPercent : _energyPenaltyPercent;
            float hitStop = obstacle != null ? obstacle.HitStopDuration : _hitStopDuration;
            float distancePenalty = (obstacle != null && obstacle.DistancePenaltyMeters > 0f)
                ? obstacle.DistancePenaltyMeters
                : _defaultDistancePenalty;

            // 1. Chuyển Runner thành Trigger để vật cản trôi xuyên qua an toàn
            if (_controller != null)
            {
                _controller.SetTriggerMode(true);
            }
            else
            {
                var col = GetComponent<Collider>();
                if (col != null) col.isTrigger = true;
            }

            // 2. Trừ năng lượng (hỗ trợ cả EnergySystem lẫn Faction Fan Energy)
            EnergySystem energySystem = FindFirstObjectByType<EnergySystem>();
            if (energySystem != null && penalty > 0f)
            {
                energySystem.AddEnergy(-penalty);
            }
            else
            {
                SteamRush.Features.StreamIntegration.FactionTugOfWarManager faction =
                    FindFirstObjectByType<SteamRush.Features.StreamIntegration.FactionTugOfWarManager>();
                if (faction != null && penalty > 0f)
                {
                    faction.TryConsumeFanEnergy(Mathf.RoundToInt(penalty));
                }
            }

            // 3. Phạt trừ quãng đường trên thanh tiến trình (-15m)
            TrackProgressTracker tracker = FindFirstObjectByType<TrackProgressTracker>();
            float finalDistancePenalty = (obstacle != null && obstacle.DistancePenaltyMeters > 0f)
                ? obstacle.DistancePenaltyMeters
                : (distancePenalty > 0f ? distancePenalty : 15f);

            if (obstacle == null || obstacle.DistancePenaltyMeters <= 0f)
            {
                if (tracker != null && finalDistancePenalty > 0f)
                {
                    tracker.ReduceDistance(finalDistancePenalty);
                }
            }

            // 4. Hiển thị thông báo trạng thái theo phân cấp xe GDD v1.4
            HUDManager hud = FindFirstObjectByType<HUDManager>();
            if (obstacle is StreamRushLive.Features.Spawning.DrivingObstacleCar drivingCar)
            {
                string tierTitle = drivingCar.Tier switch
                {
                    StreamRushLive.Features.Spawning.VehicleTier.HeavyTruck => "Xe Tải Hạng Nặng Tông!",
                    StreamRushLive.Features.Spawning.VehicleTier.PickupTruck => "Xe Bán Tải Húc!",
                    _ => "Xe Con Húc!"
                };
                hud?.ShowStatusPopup($"{tierTitle} (-{finalDistancePenalty:F0}m Cự ly, -{penalty:F0}% NL)", false);
            }
            else
            {
                hud?.ShowStatusPopup($"Va chạm xe! (-{finalDistancePenalty:F0}m Cự ly, -{penalty:F0}% NL)", false);
            }

            if (_chatLaneRunner == null)
            {
                _chatLaneRunner = GetComponent<ChatLaneRunnerController>() ?? GetComponentInParent<ChatLaneRunnerController>();
            }

            // 5. Knockback: Đẩy lùi Runner về sau theo trục -X (GDD v1.2 & v1.4)
            float kbDistance = obstacle is StreamRushLive.Features.Spawning.DrivingObstacleCar carObj ? carObj.KnockbackDistance : 2.2f;
            float kbDuration = obstacle is StreamRushLive.Features.Spawning.DrivingObstacleCar carObj2 ? carObj2.KnockbackDuration : 0.5f;

            if (_chatLaneRunner != null)
            {
                _chatLaneRunner.ApplyKnockback(kbDistance, kbDuration);
            }
            else if (_controller != null)
            {
                _controller.ApplyKnockback(kbDistance, kbDuration);
            }

            // 6. Hit-stop: Đóng băng khung hình ngắn nếu có cấu hình
            if (hitStop > 0.01f)
            {
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(hitStop);
                Time.timeScale = 1f;
            }

            // 7. World Reverse Knockback (GDD v1.2 Mục 4):
            // Kích hoạt xung cuộn ngược thế giới (-8.5 m/s trong 0.5s) tạo cảm giác thế giới trôi lùi về vị trí cũ
            WorldSpeedManager speedManager = FindFirstObjectByType<WorldSpeedManager>() ?? WorldSpeedManager.Instance;
            if (speedManager != null)
            {
                speedManager.TriggerReverseWorldKnockback(-8.5f, 0.5f);
            }

            // 8. i-Frames: Nhấp nháy model và giữ Trigger mode trong lúc trôi qua vật cản (2.0s theo GDD)
            float invulDuration = _chatLaneRunner != null ? 2.0f : _invulnerabilityDuration;
            float elapsed = 0f;
            bool isKnocking = true;
            while (elapsed < invulDuration || isKnocking)
            {
                elapsed += Time.deltaTime;
                isKnocking = (_chatLaneRunner != null && _chatLaneRunner.IsKnockingBack) ||
                             (_controller != null && _controller.IsKnockingBack);
                SetRenderersVisible(elapsed % 0.15f < 0.075f);
                yield return null;
            }

            SetRenderersVisible(true);

            // 9. Trả Collider về trạng thái bình thường
            if (_controller != null)
            {
                _controller.SetTriggerMode(false);
            }
            else
            {
                var col = GetComponent<Collider>();
                if (col != null) col.isTrigger = false;
            }
            _isHandlingHit = false;
        }

        private void SetRenderersVisible(bool visible)
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>();
            }

            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = visible;
                }
            }
        }

        /// <summary>
        /// Kiểm tra Runner có Shield hay không.
        /// Nếu có, Shield sẽ bị tiêu hao và va chạm không gây trừ cự ly hay năng lượng.
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
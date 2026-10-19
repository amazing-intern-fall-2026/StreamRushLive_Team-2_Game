namespace SteamRush.Features.Runner
{
    using System.Collections;
    using UnityEngine;
    using SteamRush.Track;
    using StreamRushLive.Features.Spawning;
    using SteamRush.Features.UI;

    /// <summary>
    /// Handles Runner collision and interactions with obstacles and pickups.
    /// Manages shield defense, knockback penalty, i-frames, and world recovery.
    /// </summary>
    [RequireComponent(typeof(RunnerMovement))]
    public class RunnerCollisionHandler : MonoBehaviour
    {
        [SerializeField] private string _obstacleTag = "Obstacle";

        [Header("Hit-Stop Settings")]
        [Tooltip("Real-time hit-stop freeze duration on collision.")]
        [SerializeField] private float _hitStopDuration = 0.15f;

        [Header("Penalty Settings (GDD v1.2)")]
        [Tooltip("Percentage energy deducted on obstacle collision.")]
        [SerializeField] private float _energyPenaltyPercent = 25f;

        [Tooltip("Progress penalty distance pushed backward (meters).")]
        [SerializeField] private float _defaultDistancePenalty = 10f;

        [Header("Invulnerability Settings")]
        [Tooltip("Invulnerability duration (blinking + trigger mode) for obstacles to pass through.")]
        [SerializeField] private float _invulnerabilityDuration = 0.8f;

        private RunnerMovement _movement;
        private ChatLaneRunnerController _chatLaneRunner;
        private Renderer[] _renderers;

        private bool _isHandlingHit;
        private bool _isHyperDashActive;
        private float _shieldDeflectImmunityTimer = 0f;
        // BUGFIX: miễn nhiễm sau khi Khiên đẩy văng xe chỉ áp dụng cho ĐÚNG xe đó (các collider
        // phụ của cùng 1 obstacle, ví dụ nhiều mesh con), KHÔNG được áp dụng cho mọi va chạm khác.
        // Trước đây dùng timer toàn cục (_shieldDeflectImmunityTimer > 0 chặn HandleInteraction()
        // cho TẤT CẢ), nên nếu đổi làn tông trúng xe thứ 2 (hoàn toàn khác xe 1) trong 0.8s đó,
        // HandleInteraction() bị return sớm ở đầu hàm — bỏ qua luôn TriggerHit/TryConsumeShield
        // của xe thứ 2, nên không có phạt/i-frame, chỉ còn va chạm vật lý (Collider) làm chậm tốc độ.
        private GameObject _shieldDeflectedObstacleRoot;

        public bool IsHandlingHit => _isHandlingHit;
        public bool IsHyperDashActive => _isHyperDashActive;

        private void Update()
        {
            if (_shieldDeflectImmunityTimer > 0f)
            {
                _shieldDeflectImmunityTimer -= Time.deltaTime;
                if (_shieldDeflectImmunityTimer <= 0f)
                {
                    _shieldDeflectedObstacleRoot = null;
                }
            }
        }

        private void Awake()
        {
            _movement = GetComponent<RunnerMovement>();
            _chatLaneRunner = GetComponent<ChatLaneRunnerController>();
            _renderers = GetComponentsInChildren<Renderer>();
            _isHandlingHit = false;
            SetRenderersVisible(true);
            if (_movement != null)
            {
                _movement.SetTriggerMode(false);
            }
            else
            {
                var col = GetComponent<Collider>();
                if (col != null) col.isTrigger = false;
            }
        }

        /// <summary>
        /// Toggles Hyper Dash state. Runner turns into Trigger to phase through obstacles.
        /// </summary>
        public void SetHyperDashState(bool isActive)
        {
            _isHyperDashActive = isActive;

            if (_movement != null)
            {
                _movement.SetHyperDashTriggerMode(isActive);
            }
        }

        /// <summary>
        /// Trigger interaction handler.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            HandleInteraction(other.gameObject);
        }

        /// <summary>
        /// Collision interaction handler.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            HandleInteraction(collision.gameObject);
        }

        private void HandleInteraction(GameObject obj)
        {
            // Chỉ bỏ qua nếu đây đúng là obstacle (hoặc 1 collider con của nó) vừa bị Khiên đẩy
            // văng — không còn chặn mọi va chạm khác trong lúc timer còn chạy.
            if (_shieldDeflectImmunityTimer > 0f
                && _shieldDeflectedObstacleRoot != null
                && obj.transform.IsChildOf(_shieldDeflectedObstacleRoot.transform))
            {
                return;
            }

            if (_isHyperDashActive)
            {
                ObstacleBase hyperDashObstacle =
                    obj.GetComponentInParent<ObstacleBase>();

                if (hyperDashObstacle != null)
                {
                    Destroy(hyperDashObstacle.gameObject);
                    return;
                }

                if (obj.CompareTag(_obstacleTag)
                    || obj.name.Contains("Barrier")
                    || obj.name.Contains("Obstacle"))
                {
                    Destroy(obj);
                    return;
                }
            }

            ItemBase item = obj.GetComponentInParent<ItemBase>();
            if (item != null)
            {
                item.Collect(gameObject);
                return;
            }

            if (obj.CompareTag("Buff") || obj.name.Contains("Buff"))
            {
                RunnerEnergySystem energy = FindFirstObjectByType<RunnerEnergySystem>();
                if (energy != null)
                {
                    energy.AddEnergy(20f);
                }
                else
                {
                    var faction = FindFirstObjectByType<SteamRush.Features.StreamIntegration.FactionTugOfWarManager>();
                    if (faction != null)
                    {
                        int bonus = Mathf.RoundToInt(0.2f * faction.GetFanEnergyMax());
                        faction.AddLikes(SteamRush.Features.StreamIntegration.FactionType.Blue, bonus);
                    }
                }

                HUDManager hud = FindFirstObjectByType<HUDManager>();
                hud?.ShowStatusPopup("+20% Energy", true);

                Destroy(obj);
                return;
            }

            if (_isHandlingHit)
            {
                return;
            }

            ObstacleBase obstacle =
                obj.GetComponentInParent<ObstacleBase>();

            if (obstacle != null)
            {
                if (obstacle.IsShieldDeflected)
                {
                    return;
                }

                if (TryConsumeShield(obstacle.gameObject))
                {
                    return;
                }

                // ObstacleBase already handled this collision
                if (!obstacle.HasCollided)
                {
                    obstacle.TriggerHit(gameObject);
                }
                else if (!obstacle.IsShieldDeflected)
                {
                    StartCoroutine(HandleObstacleHit(obstacle));
                }

                return;
            }

            // Fallback for obstacles without ObstacleBase or DynamicObstacle
            StreamRushLive.Features.Spawning.DynamicObstacle dynamicObs = obj.GetComponentInParent<StreamRushLive.Features.Spawning.DynamicObstacle>();
            if (dynamicObs != null && dynamicObs.IsShieldDeflected)
            {
                return;
            }

            if (dynamicObs != null || obj.CompareTag(_obstacleTag)
                || obj.transform.root.CompareTag(_obstacleTag)
                || obj.name.Contains("Barrier")
                || obj.name.Contains("Obstacle")
                || obj.name.Contains("Car"))
            {
                GameObject targetObj = dynamicObs != null ? dynamicObs.gameObject : obj.transform.root.gameObject;
                if (TryConsumeShield(targetObj))
                {
                    return;
                }

                if (dynamicObs != null)
                {
                    dynamicObs.OnHitPlayer(gameObject);
                }

                StartCoroutine(HandleObstacleHit(dynamicObs));
            }
        }

        /// <summary>
        /// Called by ObstacleBase when a collision with runner is confirmed.
        /// </summary>
        public void HandleObstacleHitFromSource(ObstacleBase obstacle)
        {
            bool isDeflectedObstacle = _shieldDeflectImmunityTimer > 0f
                && _shieldDeflectedObstacleRoot != null
                && obstacle != null
                && obstacle.transform.IsChildOf(_shieldDeflectedObstacleRoot.transform);

            if (_isHandlingHit || isDeflectedObstacle)
            {
                return;
            }

            if (obstacle != null)
            {
                if (obstacle.IsShieldDeflected) return;
                if (TryConsumeShield(obstacle.gameObject)) return;
            }

            StartCoroutine(HandleObstacleHit(obstacle));
        }

        /// <summary>
        /// Executes collision resolution on runner:
        /// - Applies progress and energy penalties
        /// - Triggers reverse world knockback impulse
        /// - Activates invulnerability i-frames
        /// </summary>
        private IEnumerator HandleObstacleHit(ObstacleBase obstacle)
        {
            _isHandlingHit = true;

            float penalty = obstacle != null ? obstacle.EnergyPenaltyPercent : _energyPenaltyPercent;
            float hitStop = obstacle != null ? obstacle.HitStopDuration : _hitStopDuration;
            float distancePenalty = (obstacle != null && obstacle.DistancePenaltyMeters > 0f)
                ? obstacle.DistancePenaltyMeters
                : _defaultDistancePenalty;

            if (_movement != null)
            {
                _movement.SetTriggerMode(true);
            }
            else
            {
                var col = GetComponent<Collider>();
                if (col != null) col.isTrigger = true;
            }

            RunnerEnergySystem energySystem = FindFirstObjectByType<RunnerEnergySystem>();
            if (energySystem != null && penalty > 0f)
            {
                float deduction = (penalty / 100f) * energySystem.MaxEnergy;
                energySystem.AddEnergy(-deduction);
            }

            SteamRush.Features.StreamIntegration.FactionTugOfWarManager faction =
                FindFirstObjectByType<SteamRush.Features.StreamIntegration.FactionTugOfWarManager>();
            if (faction != null && penalty > 0f)
            {
                faction.TryConsumeFanEnergyPercent(penalty);
            }

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

            HUDManager hud = FindFirstObjectByType<HUDManager>();
            if (obstacle is StreamRushLive.Features.Spawning.DynamicObstacle dynamicObs2)
            {
                string tierTitle = dynamicObs2.ObstacleClassification switch
                {
                    StreamRushLive.Features.Spawning.ObstacleType.Trains => "Train Hit!",
                    StreamRushLive.Features.Spawning.ObstacleType.Animals => "Animals Hit!",
                    _ => "Car Hit!"
                };
                hud?.ShowStatusPopup($"{tierTitle} (-{finalDistancePenalty:F0}m, -{penalty:F0}% Energy)", false);
            }
            else
            {
                hud?.ShowStatusPopup($"Vehicle Hit! (-{finalDistancePenalty:F0}m, -{penalty:F0}% Energy)", false);
            }

            if (_chatLaneRunner == null)
            {
                _chatLaneRunner = GetComponent<ChatLaneRunnerController>() ?? GetComponentInParent<ChatLaneRunnerController>();
            }

            float kbDistance = obstacle is StreamRushLive.Features.Spawning.DynamicObstacle obsObj ? obsObj.KnockbackDistance : 2.2f;
            float kbDuration = obstacle is StreamRushLive.Features.Spawning.DynamicObstacle obsObj2
                ? Mathf.Max(obsObj2.KnockbackDuration, obsObj2.ReverseWorldDuration)
                : 1.8f;

            if (_chatLaneRunner != null)
            {
                _chatLaneRunner.ApplyKnockback(kbDistance, kbDuration);
            }
            else if (_movement != null)
            {
                _movement.ApplyKnockback(kbDistance, kbDuration);
            }

            if (hitStop > 0.01f)
            {
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(hitStop);
                Time.timeScale = 1f;
            }

            WorldSpeedManager speedManager = FindFirstObjectByType<WorldSpeedManager>() ?? WorldSpeedManager.Instance;
            if (speedManager != null)
            {
                float peakSpeed = obstacle is StreamRushLive.Features.Spawning.DynamicObstacle obsObj3 ? obsObj3.ReverseWorldPeakSpeed : -15f;
                float duration = obstacle is StreamRushLive.Features.Spawning.DynamicObstacle obsObj4 ? obsObj4.ReverseWorldDuration : 0.65f;
                speedManager.TriggerReverseWorldKnockback(peakSpeed, duration);
            }

            float invulDuration = _chatLaneRunner != null ? 2.0f : _invulnerabilityDuration;
            float elapsed = 0f;
            bool isKnocking = true;
            while (elapsed < invulDuration || isKnocking)
            {
                elapsed += Time.deltaTime;
                isKnocking = (_chatLaneRunner != null && _chatLaneRunner.IsKnockingBack) ||
                             (_movement != null && _movement.IsKnockingBack);
                SetRenderersVisible(elapsed % 0.15f < 0.075f);
                yield return null;
            }

            SetRenderersVisible(true);

            if (_movement != null)
            {
                _movement.SetTriggerMode(false);
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
        /// Checks if the runner is currently shielded.
        /// </summary>
        public bool HasShieldActive()
        {
            RunnerItemEffects itemEffects = GetComponent<RunnerItemEffects>() ?? GetComponentInParent<RunnerItemEffects>();
            return itemEffects != null && itemEffects.IsShieldActive;
        }

        /// <summary>
        /// Checks and consumes shield to block an obstacle collision,
        /// deflecting colliding vehicles without penalty.
        /// </summary>
        public bool TryConsumeShield(GameObject obstacleObj = null)
        {
            RunnerItemEffects itemEffects = GetComponent<RunnerItemEffects>() ?? GetComponentInParent<RunnerItemEffects>();

            if (itemEffects == null || !itemEffects.IsShieldActive)
            {
                return false;
            }

            bool blocked = itemEffects.ConsumeShield();
            if (blocked)
            {
                // Short 0.8s immunity against secondary colliders of THIS SPECIFIC deflected
                // obstacle only (e.g. multiple child meshes/colliders on the same car) — scoped
                // via _shieldDeflectedObstacleRoot, checked against in HandleInteraction() /
                // HandleObstacleHitFromSource() above. Does NOT affect any other obstacle.
                _shieldDeflectImmunityTimer = 0.8f;
                _shieldDeflectedObstacleRoot = obstacleObj;

                if (obstacleObj != null)
                {
                    DeflectObstacle(obstacleObj);
                }

                HUDManager hud = FindFirstObjectByType<HUDManager>();
                hud?.ShowStatusPopup("Shield Deflected Car!", true);
                Debug.Log("[RunnerCollisionHandler] Shield blocked obstacle and deflected car away!");
            }

            return blocked;
        }

        /// <summary>
        /// Launches deflected obstacle away from the track.
        /// </summary>
        private void DeflectObstacle(GameObject obstacleObj)
        {
            if (obstacleObj == null) return;

            StreamRushLive.Features.Spawning.DynamicObstacle dynamicObs3 =
                obstacleObj.GetComponentInParent<StreamRushLive.Features.Spawning.DynamicObstacle>()
                ?? obstacleObj.GetComponent<StreamRushLive.Features.Spawning.DynamicObstacle>()
                ?? obstacleObj.GetComponentInChildren<StreamRushLive.Features.Spawning.DynamicObstacle>();

            if (dynamicObs3 != null)
            {
                dynamicObs3.DeflectByShield(transform.position);
                return;
            }

            StartCoroutine(DeflectFlyFallbackRoutine(obstacleObj.transform, transform.position));
        }

        private IEnumerator DeflectFlyFallbackRoutine(Transform target, Vector3 runnerPos)
        {
            if (target == null) yield break;

            var mover = target.GetComponent<MovingWorldObject>() ?? target.GetComponentInParent<MovingWorldObject>();
            if (mover != null) mover.enabled = false;

            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = false;
            }

            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            float sideZ = (target.position.z >= runnerPos.z) ? 1f : -1f;
            if (Mathf.Abs(target.position.z - runnerPos.z) < 0.2f)
            {
                sideZ = Random.value > 0.5f ? 1f : -1f;
            }

            float vx = 6f;
            float vy = 13.5f;
            float vz = sideZ * 16.5f;
            float gravity = -26f;
            Vector3 rotAxis = new Vector3(Random.Range(220f, 380f), Random.Range(90f, 200f), sideZ * Random.Range(280f, 480f));

            float elapsed = 0f;
            float duration = 1.6f;

            while (elapsed < duration && target != null)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                vy += gravity * dt;
                target.position += new Vector3(vx, vy, vz) * dt;
                target.Rotate(rotAxis * dt, Space.World);

                yield return null;
            }

            if (target != null)
            {
                Destroy(target.gameObject);
            }
        }
    }
}
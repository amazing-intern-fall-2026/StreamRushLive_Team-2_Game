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
    [RequireComponent(typeof(RunnerController))]
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

        private RunnerController _controller;
        private ChatLaneRunnerController _chatLaneRunner;
        private Renderer[] _renderers;

        private bool _isHandlingHit;
        private bool _isHyperDashActive;
        private float _shieldDeflectImmunityTimer = 0f;

        public bool IsHandlingHit => _isHandlingHit;
        public bool IsHyperDashActive => _isHyperDashActive;

        private void Update()
        {
            if (_shieldDeflectImmunityTimer > 0f)
            {
                _shieldDeflectImmunityTimer -= Time.deltaTime;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<RunnerController>();
            _chatLaneRunner = GetComponent<ChatLaneRunnerController>();
            _renderers = GetComponentsInChildren<Renderer>();
        }

        /// <summary>
        /// Toggles Hyper Dash state. Runner turns into Trigger to phase through obstacles.
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
            if (_shieldDeflectImmunityTimer > 0f)
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
                EnergySystem energy = FindFirstObjectByType<EnergySystem>();
                if (energy != null)
                {
                    energy.AddEnergy(20f);
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

            // Fallback for obstacles without ObstacleBase or DrivingObstacleCar
            StreamRushLive.Features.Spawning.DrivingObstacleCar drivingCar = obj.GetComponentInParent<StreamRushLive.Features.Spawning.DrivingObstacleCar>();
            if (drivingCar != null && drivingCar.IsShieldDeflected)
            {
                return;
            }

            if (drivingCar != null || obj.CompareTag(_obstacleTag)
                || obj.transform.root.CompareTag(_obstacleTag)
                || obj.name.Contains("Barrier")
                || obj.name.Contains("Obstacle")
                || obj.name.Contains("Car"))
            {
                GameObject targetObj = drivingCar != null ? drivingCar.gameObject : obj.transform.root.gameObject;
                if (TryConsumeShield(targetObj))
                {
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
        /// Called by ObstacleBase when a collision with runner is confirmed.
        /// </summary>
        public void HandleObstacleHitFromSource(ObstacleBase obstacle)
        {
            if (_isHandlingHit || _shieldDeflectImmunityTimer > 0f)
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

            if (_controller != null)
            {
                _controller.SetTriggerMode(true);
            }
            else
            {
                var col = GetComponent<Collider>();
                if (col != null) col.isTrigger = true;
            }

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
            if (obstacle is StreamRushLive.Features.Spawning.DrivingObstacleCar drivingCar)
            {
                string tierTitle = drivingCar.Tier switch
                {
                    StreamRushLive.Features.Spawning.VehicleTier.HeavyTruck => "Heavy Truck Hit!",
                    StreamRushLive.Features.Spawning.VehicleTier.PickupTruck => "Pickup Hit!",
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

            float kbDistance = obstacle is StreamRushLive.Features.Spawning.DrivingObstacleCar carObj ? carObj.KnockbackDistance : 2.2f;
            float kbDuration = obstacle is StreamRushLive.Features.Spawning.DrivingObstacleCar carObj2
                ? Mathf.Max(carObj2.KnockbackDuration, carObj2.ReverseWorldDuration)
                : 1.8f;

            if (_chatLaneRunner != null)
            {
                _chatLaneRunner.ApplyKnockback(kbDistance, kbDuration);
            }
            else if (_controller != null)
            {
                _controller.ApplyKnockback(kbDistance, kbDuration);
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
                float peakSpeed = obstacle is StreamRushLive.Features.Spawning.DrivingObstacleCar carObj3 ? carObj3.ReverseWorldPeakSpeed : -15f;
                float duration = obstacle is StreamRushLive.Features.Spawning.DrivingObstacleCar carObj4 ? carObj4.ReverseWorldDuration : 0.65f;
                speedManager.TriggerReverseWorldKnockback(peakSpeed, duration);
            }

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
                // Short 0.8s immunity against secondary colliders of the deflected obstacle
                _shieldDeflectImmunityTimer = 0.8f;

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

            StreamRushLive.Features.Spawning.DrivingObstacleCar drivingCar =
                obstacleObj.GetComponentInParent<StreamRushLive.Features.Spawning.DrivingObstacleCar>()
                ?? obstacleObj.GetComponent<StreamRushLive.Features.Spawning.DrivingObstacleCar>()
                ?? obstacleObj.GetComponentInChildren<StreamRushLive.Features.Spawning.DrivingObstacleCar>();

            if (drivingCar != null)
            {
                drivingCar.DeflectByShield(transform.position);
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
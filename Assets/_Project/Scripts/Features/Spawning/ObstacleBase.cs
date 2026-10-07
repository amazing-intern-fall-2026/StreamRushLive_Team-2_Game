using UnityEngine;
using SteamRush.Features.Runner;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Abstract base class for all obstacle hazards in the game.
    /// Configures penalty multipliers, hit-stop, and provides OnHitPlayer() hook.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public abstract class ObstacleBase : MonoBehaviour
    {
        [Header("Obstacle Info")]
        [SerializeField] protected string obstacleName = "Obstacle";
        [SerializeField] protected ObstacleType obstacleType;

        [Header("Penalty Settings")]
        [Tooltip("Energy penalty percentage (%).")]
        [SerializeField] protected float energyPenaltyPercent = 25f;

        [Tooltip("Hit-stop freeze duration in seconds.")]
        [SerializeField] protected float hitStopDuration = 0.15f;

        [Tooltip("Distance penalty deducted in meters (0 if none).")]
        [SerializeField] protected float distancePenaltyMeters = 0f;

        private bool _hasCollided = false;
        private bool _isShieldDeflected = false;

        public string ObstacleName => obstacleName;
        public ObstacleType Type => obstacleType;
        public float EnergyPenaltyPercent => energyPenaltyPercent;
        public float HitStopDuration => hitStopDuration;
        public float DistancePenaltyMeters => distancePenaltyMeters;
        public bool HasCollided => _hasCollided;
        public bool IsShieldDeflected
        {
            get => _isShieldDeflected;
            set => _isShieldDeflected = value;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_hasCollided || _isShieldDeflected) return;

            if (IsPlayer(collision.gameObject))
            {
                TriggerHit(collision.gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasCollided || _isShieldDeflected) return;

            if (IsPlayer(other.gameObject))
            {
                TriggerHit(other.gameObject);
            }
        }

        /// <summary>
        /// Resolves collision interaction with player.
        /// </summary>
        public void TriggerHit(GameObject player)
        {
            if (_hasCollided || _isShieldDeflected) return;

            // If runner is shielded, block impact completely and deflect obstacle
            SteamRush.Features.Runner.RunnerCollisionHandler collisionHandler = player != null
                ? (player.GetComponentInParent<SteamRush.Features.Runner.RunnerCollisionHandler>() ?? player.GetComponent<SteamRush.Features.Runner.RunnerCollisionHandler>())
                : null;

            if (collisionHandler != null && collisionHandler.HasShieldActive())
            {
                _hasCollided = true;
                _isShieldDeflected = true;
                collisionHandler.TryConsumeShield(gameObject);
                return;
            }

            _hasCollided = true;

            // 1. Apply distance penalty if configured
            if (distancePenaltyMeters > 0f)
            {
                TrackProgressTracker tracker = FindFirstObjectByType<TrackProgressTracker>();
                if (tracker != null)
                {
                    tracker.ReduceDistance(distancePenaltyMeters);
                }
            }

            // 2. Invoke obstacle-specific hit hook
            OnHitPlayer(player);

            // 3. Notify Player collision handler
            if (collisionHandler != null)
            {
                collisionHandler.HandleObstacleHitFromSource(this);
            }
        }

        /// <summary>
        /// Abstract hook allowing concrete obstacles to implement custom hit logic.
        /// </summary>
        public abstract void OnHitPlayer(GameObject player);

        protected virtual bool IsPlayer(GameObject obj)
        {
            return obj.CompareTag("Player")
                   || obj.GetComponentInParent<RunnerController>() != null;
        }
    }
}

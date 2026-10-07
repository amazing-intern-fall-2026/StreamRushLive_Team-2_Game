using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Bouncing Boulder obstacle (GDD v1.2 Section 2):
    /// Rolls towards runner (3.5 m/s) combined with world scroll speed while
    /// bouncing in a sine wave pattern with period 0.8s, height 1.8m.
    /// Penalty: -35% Energy and -15m distance.
    /// </summary>
    public class BouncingBoulderObstacle : ObstacleBase
    {
        [Header("Bounce & Roll Settings")]
        [SerializeField] private float bounceHeight = 1.8f;
        [SerializeField] private float bouncePeriod = 0.8f;
        [SerializeField] private float rollSpeed = 3.5f;

        private float _baseY;
        private float _elapsedTime;
        private MovingWorldObject _movingWorldObject;
        private WorldSpeedManager _speedManager;

        private void Awake()
        {
            obstacleType = ObstacleType.BouncingBoulder;
            energyPenaltyPercent = 35f;
            distancePenaltyMeters = 15f;
            _baseY = transform.position.y;
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
            _elapsedTime += Time.deltaTime;

            Vector3 position = transform.position;

            // If MovingWorldObject is absent, manually compound world scroll speed
            float worldSpeed = (_movingWorldObject == null && _speedManager != null) ? _speedManager.CurrentSpeed : 0f;
            position.x -= (rollSpeed + worldSpeed) * Time.deltaTime;

            // Sine wave bounce trajectory
            position.y = _baseY + bounceHeight * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * _elapsedTime / bouncePeriod));

            transform.position = position;
        }

        public override void OnHitPlayer(GameObject player)
        {
            // Handled in ObstacleBase (-15m distance) and RunnerCollisionHandler
        }
    }
}
using System.Collections;
using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Falling Hazard obstacle (GDD v1.2 Section 2):
    /// Drops vertically onto the track. A blinking shadow warns the runner
    /// on the road for warningDuration (1.0s) before falling.
    /// </summary>
    public class FallingHazardObstacle : ObstacleBase
    {
        [Header("Falling Hazard Settings")]
        [SerializeField] private float warningDuration = 1.0f;
        [SerializeField] private SpriteRenderer warningShadowSprite;
        [SerializeField] private float blinkInterval = 0.15f;
        [SerializeField] private Rigidbody hazardRigidbody;

        private WorldSpeedManager _speedManager;
        private MovingWorldObject _movingWorldObject;

        private void Awake()
        {
            obstacleType = ObstacleType.FallingHazard;
            energyPenaltyPercent = 40f;
            _movingWorldObject = GetComponent<MovingWorldObject>();

            if (hazardRigidbody == null)
            {
                hazardRigidbody = GetComponent<Rigidbody>();
            }

            if (hazardRigidbody != null)
            {
                hazardRigidbody.isKinematic = true;
            }
        }

        private void Start()
        {
            if (_speedManager == null)
            {
                _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
            StartCoroutine(PrepareToFall());
        }

        private void Update()
        {
            // Only move manually if MovingWorldObject is absent to prevent double movement
            if (_movingWorldObject == null)
            {
                float speed = _speedManager != null ? _speedManager.CurrentSpeed : 0f;
                transform.position += Vector3.left * (speed * Time.deltaTime);
            }
        }

        public override void OnHitPlayer(GameObject player)
        {
            // Handled in ObstacleBase and RunnerCollisionHandler
        }

        private IEnumerator PrepareToFall()
        {
            float elapsed = 0f;
            bool isVisible = true;

            while (elapsed < warningDuration)
            {
                if (warningShadowSprite != null)
                {
                    warningShadowSprite.enabled = isVisible;
                    isVisible = !isVisible;
                }

                yield return new WaitForSeconds(blinkInterval);
                elapsed += blinkInterval;
            }

            if (warningShadowSprite != null)
            {
                warningShadowSprite.enabled = false;
            }

            if (hazardRigidbody != null)
            {
                hazardRigidbody.isKinematic = false;
            }
        }
    }
}
using System.Collections;
using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    public class FallingHazardObstacle : ObstacleBase
    {
        [SerializeField] private float warningDuration = 1.0f;
        [SerializeField] private SpriteRenderer warningShadowSprite;
        [SerializeField] private float blinkInterval = 0.15f;
        [SerializeField] private Rigidbody hazardRigidbody;

        private WorldSpeedManager _speedManager;

        private void Awake()
        {
            if (hazardRigidbody != null)
            {
                hazardRigidbody.isKinematic = true;
            }
        }

        private void Start()
        {
            _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            StartCoroutine(PrepareToFall());
        }

        private void Update()
        {
            float speed = _speedManager != null ? _speedManager.CurrentSpeed : 0f;
            transform.position += Vector3.left * speed * Time.deltaTime;
        }

        public override void OnHitPlayer(GameObject player)
        {
            Debug.Log($"Falling Hazard va chạm Player — cần trừ {energyPenaltyPercent}% Energy.", this);
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
using System.Collections;
using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Chướng ngại vật Rơi Từ Trời (Falling Hazard - GDD v1.2 Mục 2):
    /// Rơi thẳng đứng xuống đường chạy. Có bóng cảnh báo nhấp nháy trên mặt đường
    /// trong warningDuration (1.0s) trước khi vật thể rơi xuống.
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
            // Chỉ tự dịch chuyển nếu chưa có MovingWorldObject quản lý để tránh di chuyển x2 tốc độ
            if (_movingWorldObject == null)
            {
                float speed = _speedManager != null ? _speedManager.CurrentSpeed : 0f;
                transform.position += Vector3.left * (speed * Time.deltaTime);
            }
        }

        public override void OnHitPlayer(GameObject player)
        {
            // Đã được xử lý trong ObstacleBase và RunnerCollisionHandler
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
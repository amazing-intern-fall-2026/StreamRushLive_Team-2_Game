using UnityEngine;
using SteamRush.Features.Runner;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Lớp trừu tượng (Abstract Class) cơ sở cho toàn bộ chướng ngại vật (Obstacles) trong game:
    /// - Quản lý tên, loại ObstacleType.
    /// - Cấu hình mức độ phạt riêng biệt cho từng loại: lực đẩy lùi, trừ năng lượng, khựng hình, trừ quãng đường.
    /// - Cung cấp hàm abstract OnHitPlayer() để các lớp con tự định nghĩa hành vi phụ (ví dụ trừ quãng đường, xoay vòng, văng mảnh vỡ,...).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class ObstacleBase : MonoBehaviour
    {
        [Header("Obstacle Info")]
        [SerializeField] protected string obstacleName = "Obstacle";
        [SerializeField] protected ObstacleType obstacleType;

        [Header("Penalty Settings")]
        [Tooltip("Player knockback distance in meters.")]
        [SerializeField] protected float knockbackDistance = 1.5f;

        [Tooltip("Energy penalty percentage (%).")]
        [SerializeField] protected float energyPenaltyPercent = 25f;

        [Tooltip("Hit-stop freeze duration in seconds.")]
        [SerializeField] protected float hitStopDuration = 0.15f;

        [Tooltip("Distance penalty deducted in meters (0 if none).")]
        [SerializeField] protected float distancePenaltyMeters = 0f;

        private bool _hasCollided = false;

        public string ObstacleName => obstacleName;
        public ObstacleType Type => obstacleType;
        public float KnockbackDistance => knockbackDistance;
        public float EnergyPenaltyPercent => energyPenaltyPercent;
        public float HitStopDuration => hitStopDuration;
        public float DistancePenaltyMeters => distancePenaltyMeters;
        public bool HasCollided => _hasCollided;

        private void OnCollisionEnter(Collision collision)
        {
            if (_hasCollided) return;

            if (IsPlayer(collision.gameObject))
            {
                TriggerHit(collision.gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasCollided) return;

            if (IsPlayer(other.gameObject))
            {
                TriggerHit(other.gameObject);
            }
        }

        /// <summary>
        /// Kích hoạt chuỗi xử lý va chạm với Player.
        /// </summary>
        public void TriggerHit(GameObject player)
        {
            if (_hasCollided) return;

            _hasCollided = true;

            // 1. Áp dụng trừ quãng đường nếu có cấu hình
            if (distancePenaltyMeters > 0f)
            {
                TrackProgressTracker tracker = FindFirstObjectByType<TrackProgressTracker>();
                if (tracker != null)
                {
                    tracker.ReduceDistance(distancePenaltyMeters);
                }
            }

            // 2. Gọi hàm thực thi riêng của từng loại chướng ngại vật con
            OnHitPlayer(player);

            // 3. Báo cho bộ xử lý va chạm trên Player nếu có
            RunnerCollisionHandler collisionHandler = player.GetComponentInParent<RunnerCollisionHandler>();
            if (collisionHandler != null)
            {
                collisionHandler.HandleObstacleHitFromSource(this);
            }
        }

        /// <summary>
        /// Hàm trừu tượng: Cho phép từng loại vật cản cụ thể mở rộng logic phụ khi đâm trúng Player.
        /// </summary>
        public abstract void OnHitPlayer(GameObject player);

        protected virtual bool IsPlayer(GameObject obj)
        {
            return obj.CompareTag("Player")
                   || obj.GetComponentInParent<RunnerController>() != null;
        }
    }
}

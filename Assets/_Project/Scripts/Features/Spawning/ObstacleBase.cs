using UnityEngine;
using SteamRush.Features.Runner;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Lớp trừu tượng (Abstract Class) cơ sở cho toàn bộ chướng ngại vật (Obstacles) trong game:
    /// - Quản lý tên, loại ObstacleType.
    /// - Cấu hình mức độ phạt riêng biệt cho từng loại: trừ năng lượng, khựng hình, trừ quãng đường.
    /// - Không có lực đẩy lùi knockback (Runner luôn cố định tọa độ X).
    /// - Cung cấp hàm abstract OnHitPlayer() để các lớp con tự định nghĩa hành vi phụ (ví dụ trừ quãng đường, xoay vòng, văng mảnh vỡ,...).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class ObstacleBase : MonoBehaviour
    {
        [Header("Obstacle Info")]
        [SerializeField] protected string obstacleName = "Obstacle";
        [SerializeField] protected ObstacleType obstacleType;

        [Header("Penalty & Knockback Settings")]
        [Tooltip("Mức trừ năng lượng (%) khi va chạm.")]
        [SerializeField] protected float energyPenaltyPercent = 25f;

        [Tooltip("Thời gian đóng băng khung hình (hit-stop) tính bằng giây.")]
        [SerializeField] protected float hitStopDuration = 0.15f;

        [Tooltip("Khoảng cách đẩy lùi quãng đường đã đi (mét). Khi va chạm, Runner bị trừ lùi cự ly chặng tương ứng.")]
        [SerializeField] protected float distancePenaltyMeters = 5f;

        [Tooltip("Thời gian dừng hoặc hồi phục tốc độ thế giới (giây). Mặc định -1 (dùng thời gian mặc định 1.2s của hệ thống).")]
        [SerializeField] protected float recoveryDuration = -1f;

        [Header("Despawn Settings")]
        [Tooltip("Whether to automatically despawn/destroy this obstacle GameObject upon colliding with the player.")]
        [SerializeField] protected bool despawnOnHit = false;

        [Tooltip("Optional delay before despawn in seconds (0 = immediate at end of frame).")]
        [SerializeField] protected float despawnDelay = 0f;

        public static event System.Action<ObstacleBase, float> OnObstacleDistancePenaltyApplied;

        private bool _hasCollided = false;

        public string ObstacleName => obstacleName;
        public ObstacleType Type => obstacleType;
        public float EnergyPenaltyPercent => energyPenaltyPercent;
        public float HitStopDuration => hitStopDuration;
        public float DistancePenaltyMeters
        {
            get => distancePenaltyMeters;
            set => distancePenaltyMeters = Mathf.Max(0f, value);
        }
        public float KnockbackDistanceMeters
        {
            get => distancePenaltyMeters;
            set => distancePenaltyMeters = Mathf.Max(0f, value);
        }
        public virtual float RecoveryDuration
        {
            get => recoveryDuration;
            set => recoveryDuration = value;
        }
        public bool DespawnOnHit
        {
            get => despawnOnHit;
            set => despawnOnHit = value;
        }
        public float DespawnDelay
        {
            get => despawnDelay;
            set => despawnDelay = value;
        }
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

            // 1. Áp dụng đẩy lùi quãng đường nếu có cấu hình
            if (distancePenaltyMeters > 0f)
            {
                TrackProgressTracker tracker = FindFirstObjectByType<TrackProgressTracker>();
                if (tracker != null)
                {
                    tracker.ReduceDistance(distancePenaltyMeters, showPopup: false);
                }

                OnObstacleDistancePenaltyApplied?.Invoke(this, distancePenaltyMeters);
                OnApplyDistancePenalty(player, distancePenaltyMeters);
            }

            // 2. Gọi hàm thực thi riêng của từng loại chướng ngại vật con
            OnHitPlayer(player);

            // 3. Báo cho bộ xử lý va chạm trên Player nếu có
            RunnerCollisionHandler collisionHandler = player.GetComponentInParent<RunnerCollisionHandler>();
            if (collisionHandler != null)
            {
                collisionHandler.HandleObstacleHitFromSource(this);
            }

            // 4. Tự động huỷ/despawn vật thể sau khi va chạm nếu bật despawnOnHit
            if (despawnOnHit)
            {
                if (despawnDelay > 0f)
                {
                    Destroy(gameObject, despawnDelay);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }

        /// <summary>
        /// Hàm trừu tượng: Cho phép từng loại vật cản cụ thể mở rộng logic phụ khi đâm trúng Player.
        /// </summary>
        public abstract void OnHitPlayer(GameObject player);

        /// <summary>
        /// Hook ảo: Cho phép các lớp con mở rộng logic khi áp dụng hiệu ứng đẩy lùi cự ly.
        /// </summary>
        protected virtual void OnApplyDistancePenalty(GameObject player, float distance)
        {
        }

        protected virtual bool IsPlayer(GameObject obj)
        {
            return obj.CompareTag("Player")
                   || obj.GetComponentInParent<RunnerController>() != null;
        }
    }
}

using UnityEngine;
using SteamRush.Features.Runner;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Lớp trừu tượng (Abstract Class) cơ sở cho toàn bộ vật phẩm (Items) trong game:
    /// - Quản lý tên, loại ItemType.
    /// - Tự động phát hiện va chạm Trigger với Player.
    /// - Cung cấp hàm abstract OnCollected() để từng item con tự định nghĩa chức năng khi nhặt.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class ItemBase : MonoBehaviour
    {
        [Header("Item Info")]
        [SerializeField] protected string itemName = "Item";
        [SerializeField] protected ItemType itemType;

        [Tooltip("Automatically destroy GameObject when collected.")]
        [SerializeField] protected bool destroyOnCollect = true;

        private bool _isCollected = false;

        public string ItemName => itemName;
        public ItemType Type => itemType;
        public bool IsCollected => _isCollected;

        protected virtual void Reset()
        {
            // Mặc định cài đặt collider là Trigger khi gắn script này
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isCollected) return;

            if (IsPlayer(other.gameObject))
            {
                Collect(other.gameObject);
            }
        }

        /// <summary>
        /// Kích hoạt thu thập vật phẩm. Có thể được gọi từ Trigger hoặc gọi trực tiếp từ RunnerCollisionHandler.
        /// </summary>
        public void Collect(GameObject collector)
        {
            if (_isCollected) return;

            _isCollected = true;
            OnCollected(collector);

            if (destroyOnCollect)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Hàm trừu tượng: Mỗi loại item con bắt buộc phải cài đặt logic này (ví dụ hồi máu, cộng năng lượng, buff tốc độ,...).
        /// </summary>
        public abstract void OnCollected(GameObject collector);

        protected virtual bool IsPlayer(GameObject obj)
        {
            return obj.CompareTag("Player")
                   || obj.GetComponentInParent<RunnerController>() != null;
        }
    }
}

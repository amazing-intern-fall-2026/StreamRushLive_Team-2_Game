using UnityEngine;
using SteamRush.Features.Runner;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Abstract base class for all collectible items:
    /// - Manages name and ItemType.
    /// - Detects trigger interaction with player.
    /// - Defines abstract OnCollected() hook for concrete items.
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
            // Set collider to trigger by default
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
        /// Triggers collection logic. Called on trigger contact or from RunnerCollisionHandler.
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
        /// Abstract hook implemented by concrete items.
        /// </summary>
        public abstract void OnCollected(GameObject collector);

        protected virtual bool IsPlayer(GameObject obj)
        {
            return obj.CompareTag("Player")
                   || obj.GetComponentInParent<RunnerController>() != null;
        }
    }
}

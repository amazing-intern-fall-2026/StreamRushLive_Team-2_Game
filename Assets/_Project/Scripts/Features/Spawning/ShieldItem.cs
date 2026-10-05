using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Shield pickup item:
    /// - Inherits from ItemBase.
    /// - Grants temporary shield protection upon collection.
    /// </summary>
    public class ShieldItem : ItemBase
    {
        [Header("Shield Settings")]
        [Tooltip("Shield duration in seconds.")]
        [SerializeField] private float shieldDuration = 15f;

        public float ShieldDuration => shieldDuration;

        private void Awake()
        {
            itemName = "Shield";
            itemType = ItemType.Shield;
        }

        public override void OnCollected(GameObject collector)
        {
            // Find item effect component on collecting runner
            RunnerItemEffects itemEffects = collector.GetComponent<RunnerItemEffects>();

            if (itemEffects == null)
            {
                itemEffects = collector.GetComponentInParent<RunnerItemEffects>();
            }

            if (itemEffects != null)
            {
                itemEffects.ActivateShield(shieldDuration);

                Debug.Log($"[ShieldItem] Shield activated for {shieldDuration:F0}s.");
            }
            else
            {
                Debug.LogWarning("[ShieldItem] RunnerItemEffects not found on Runner.");
            }
        }
    }
}
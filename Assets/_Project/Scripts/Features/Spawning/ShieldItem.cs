using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Vật phẩm Khiên Chắn (Shield):
    /// - Kế thừa ItemBase.
    /// - Khi nhặt, kích hoạt trạng thái bảo vệ cho Runner.
    /// - Shield tồn tại tối đa 20 giây.
    /// - Shield sẽ được hệ thống Runner xử lý khi có va chạm.
    /// </summary>
    public class ShieldItem : ItemBase
    {
        [Header("Shield Settings")]
        [Tooltip("Shield duration in seconds.")]
        [SerializeField] private float shieldDuration = 20f;

        public float ShieldDuration => shieldDuration;

        private void Awake()
        {
            itemName = "Shield";
            itemType = ItemType.Shield;
        }

        public override void OnCollected(GameObject collector)
        {
            // Tìm hệ thống Item Effect trên Runner vừa nhặt Item.
            RunnerItemEffects itemEffects = collector.GetComponent<RunnerItemEffects>();

            if (itemEffects == null)
            {
                itemEffects = collector.GetComponentInParent<RunnerItemEffects>();
            }

            if (itemEffects != null)
            {
                itemEffects.ActivateShield(shieldDuration);
            }
            else
            {
                Debug.LogWarning("[ShieldItem] Không tìm thấy RunnerItemEffects trên Runner.");
            }
        }
    }
}
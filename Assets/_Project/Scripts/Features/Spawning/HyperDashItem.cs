using System.Collections;
using UnityEngine;
using SteamRush.Features.Runner;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Tên Lửa Vô Địch (Hyper Dash):
    /// - Kế thừa ItemBase.
    /// - Khi nhặt, lập tức đưa tốc độ thế giới lên 18.0 m/s.
    /// - Kích hoạt trạng thái bất tử trong 5 giây.
    /// - Khi đang bất tử, Runner có thể đi xuyên và phá hủy vật cản.
    /// - Hết 5 giây, tốc độ và trạng thái bất tử trở lại bình thường.
    /// </summary>
    public class HyperDashItem : ItemBase
    {
        [Header("Hyper Dash Settings")]
        [Tooltip("Maximum speed applied immediately while Hyper Dash is active.")]
        [SerializeField] private float maxSpeed = 18f;

        [Tooltip("Duration of Hyper Dash in seconds.")]
        [SerializeField] private float duration = 5f;

        private void Awake()
        {
            itemName = "Hyper Dash";
            itemType = ItemType.HyperDash;
        }

        public override void OnCollected(GameObject collector)
        {
            RunnerItemEffects itemEffects = collector.GetComponent<RunnerItemEffects>();
            if (itemEffects == null)
            {
                itemEffects = collector.GetComponentInParent<RunnerItemEffects>();
            }

            if (itemEffects != null)
            {
                itemEffects.ActivateHyperDash(duration, maxSpeed);
            }
            else
            {
                Debug.LogWarning("[HyperDashItem] Không tìm thấy RunnerItemEffects trên Runner.");
            }
        }
    }
}
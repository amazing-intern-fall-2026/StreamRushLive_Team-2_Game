using UnityEngine;
using SteamRush.Features.Runner;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Vật phẩm Giày Bật Cao (High Jump):
    /// - Kế thừa ItemBase.
    /// - Khi nhặt, tăng lực nhảy của Runner lên 40%.
    /// - Hiệu ứng duy trì trong 10 giây.
    /// - Sau khi hết thời gian, lực nhảy trở về giá trị ban đầu.
    /// </summary>
    public class HighJumpItem : ItemBase
    {
        [Header("High Jump Settings")]
        [Tooltip("High Jump duration in seconds.")]
        [SerializeField] private float effectDuration = 10f;

        [Tooltip("Jump force multiplier. 1.4 = +40% jump force.")]
        [SerializeField] private float jumpForceMultiplier = 1.4f;

        public float EffectDuration => effectDuration;
        public float JumpForceMultiplier => jumpForceMultiplier;

        private void Awake()
        {
            itemName = "High Jump";
            itemType = ItemType.HighJump;
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
                itemEffects.ActivateHighJump(effectDuration, jumpForceMultiplier);
            }
            else
            {
                Debug.LogWarning("[HighJumpItem] Không tìm thấy RunnerItemEffects trên Runner.");
            }
        }
    }
}
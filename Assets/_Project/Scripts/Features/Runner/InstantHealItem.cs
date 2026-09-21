using System.Collections;
using UnityEngine;
using SteamRush.Features.Runner;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Xử lý hiệu ứng hồi máu tức thì từ quà Donate.
    ///
    /// Gameplay mới:
    /// - Không phải vật phẩm vật lý trên đường chạy.
    /// - Không cần Player nhặt.
    /// - Khi được kích hoạt sẽ hồi ngay 1 tim.
    /// - Hiển thị hiệu ứng ánh sáng xanh lá trong thời gian ngắn.
    /// </summary>
    public class InstantHealItem : MonoBehaviour
    {
        [Header("Heal / Energy Settings")]
        [Tooltip("Lượng năng lượng hồi mỗi lần nhận quà hồi máu.")]
        [SerializeField] private float energyAmount = 20f;

        [Tooltip("Thời gian hiển thị hiệu ứng ánh sáng xanh lá.")]
        [SerializeField] private float effectDuration = 0.5f;

        [Header("Green Heal Effect")]
        [Tooltip("GameObject chứa hiệu ứng ánh sáng xanh lá.")]
        [SerializeField] private GameObject healEffect;

        private Coroutine _effectCoroutine;

        private void Awake()
        {
            if (healEffect != null)
            {
                healEffect.SetActive(false);
            }
        }

        /// <summary>
        /// Kích hoạt bình hồi phục năng lượng (GDD v1.2).
        /// Hàm này được MockChatConsole gọi khi giả lập Donate Heal/Energy.
        /// </summary>
        public void ActivateHeal()
        {
            EnergySystem energy = FindFirstObjectByType<EnergySystem>();
            if (energy != null)
            {
                energy.AddEnergy(energyAmount);
            }
            else
            {
                SteamRush.Features.StreamIntegration.FactionTugOfWarManager faction =
                    FindFirstObjectByType<SteamRush.Features.StreamIntegration.FactionTugOfWarManager>();
                if (faction != null)
                {
                    for (int i = 0; i < Mathf.RoundToInt(energyAmount); i++)
                    {
                        faction.OnLikeReceived("viewer_heal");
                    }
                }
            }

            Debug.Log($"[InstantHealItem] Donate Heal -> hồi +{energyAmount}% Năng lượng.");

            // Hiển thị hiệu ứng ánh sáng xanh lá.
            PlayHealEffect();
        }

        /// <summary>
        /// Bật hiệu ứng hồi máu.
        /// Nếu hiệu ứng đang chạy thì reset thời gian hiển thị.
        /// </summary>
        private void PlayHealEffect()
        {
            if (healEffect == null)
            {
                return;
            }

            if (_effectCoroutine != null)
            {
                StopCoroutine(_effectCoroutine);
            }

            _effectCoroutine = StartCoroutine(HealEffectCoroutine());
        }

        private IEnumerator HealEffectCoroutine()
        {
            healEffect.SetActive(true);

            yield return new WaitForSeconds(effectDuration);

            healEffect.SetActive(false);
            _effectCoroutine = null;
        }
    }
}
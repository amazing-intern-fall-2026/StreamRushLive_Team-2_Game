using System.Collections;
using UnityEngine;
using SteamRush.Features.Runner;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Handles instant heal / energy recovery effect from donate gifts.
    /// </summary>
    public class InstantHealItem : MonoBehaviour
    {
        [Header("Heal / Energy Settings")]
        [Tooltip("Energy recovered per heal gift.")]
        [SerializeField] private float energyAmount = 20f;

        [Tooltip("Display duration of green heal light effect.")]
        [SerializeField] private float effectDuration = 0.5f;

        [Header("Green Heal Effect")]
        [Tooltip("GameObject containing the green heal effect.")]
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
        /// Activates energy recovery bottle (GDD v1.2).
        /// Called by MockChatConsole or stream adapters.
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

            Debug.Log($"[InstantHealItem] Donate Heal -> recovered +{energyAmount}% Energy.");

            // Display heal particle effect
            PlayHealEffect();
        }

        /// <summary>
        /// Plays heal visual effect.
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
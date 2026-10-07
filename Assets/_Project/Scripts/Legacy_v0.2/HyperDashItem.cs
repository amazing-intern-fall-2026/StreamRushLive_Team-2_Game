using System.Collections;
using UnityEngine;
using SteamRush.Features.Runner;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Hyper Dash item:
    /// - Inherits from ItemBase.
    /// - Sets world speed to 18.0 m/s.
    /// - Grants invulnerability for 5 seconds to phase through and destroy obstacles.
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
            RunnerCollisionHandler collisionHandler =
                collector.GetComponentInParent<RunnerCollisionHandler>();

            EnergySystem energySystem =
                FindFirstObjectByType<EnergySystem>();

            if (collisionHandler != null)
            {
                collisionHandler.SetHyperDashState(true);
            }

            if (energySystem != null)
            {
                energySystem.SetSpeedOverride(maxSpeed);
            }

            StartCoroutine(HyperDashRoutine(collisionHandler, energySystem));
        }

        private IEnumerator HyperDashRoutine(
            RunnerCollisionHandler collisionHandler,
            EnergySystem energySystem)
        {
            yield return new WaitForSeconds(duration);

            if (collisionHandler != null)
            {
                collisionHandler.SetHyperDashState(false);
            }

            if (energySystem != null)
            {
                energySystem.ClearSpeedOverride();
            }
        }
    }
}
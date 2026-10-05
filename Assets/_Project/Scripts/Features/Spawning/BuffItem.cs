using UnityEngine;
using SteamRush.Features.UI;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Energy restore item (Buff Item):
    /// - Inherits from ItemBase.
    /// - Restores energy to EnergySystem and triggers HUD feedback upon collection.
    /// </summary>
    public class BuffItem : ItemBase
    {
        [Header("Buff Settings")]
        [Tooltip("Energy recovered on pickup (%).")]
        [SerializeField] private float energyRecoverAmount = 20f;

        private void Awake()
        {
            itemName = "Energy Buff";
            itemType = ItemType.EnergyBuff;
        }

        public override void OnCollected(GameObject collector)
        {
            EnergySystem energy = FindFirstObjectByType<EnergySystem>();
            if (energy != null)
            {
                energy.AddEnergy(energyRecoverAmount);
            }
            else
            {
                var factionManager = FindFirstObjectByType<SteamRush.Features.StreamIntegration.FactionTugOfWarManager>();
                if (factionManager != null)
                {
                    for (int i = 0; i < (int)energyRecoverAmount; i++)
                    {
                        factionManager.OnLikeReceived("buff_pickup");
                    }
                }

                var runner = FindFirstObjectByType<SteamRush.Features.Runner.ChatLaneRunnerController>();
                if (runner != null)
                {
                    runner.ExecuteSingleCommand("fast");
                }
            }

            HUDManager hud = FindFirstObjectByType<HUDManager>();
            if (hud != null)
            {
                hud.ShowStatusPopup($"+{energyRecoverAmount:F0}% Energy!", true);
            }

            AudioManager.Instance?.PlaySFX(SFXType.CollectEnergy, 0.9f);
        }
    }
}

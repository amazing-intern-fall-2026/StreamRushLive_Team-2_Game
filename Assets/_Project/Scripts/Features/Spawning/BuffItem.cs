using UnityEngine;
using SteamRush.Features.UI;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Vật phẩm hồi năng lượng (Buff Item):
    /// - Kế thừa ItemBase.
    /// - Khi nhặt, hồi phục năng lượng cho EnergySystem và hiển thị popup trên HUD.
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
            // 1. Hồi phục năng lượng trong EnergySystem (nếu có trong scene 1 làn)
            EnergySystem energy = FindFirstObjectByType<EnergySystem>();
            if (energy != null)
            {
                energy.AddEnergy(energyRecoverAmount);
            }
            else
            {
                // Fallback cho Scene 3-Lane Chat Runner: nạp năng lượng cho Phe Fan
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

            // 2. Hiển thị thông báo trên HUD
            HUDManager hud = FindFirstObjectByType<HUDManager>();
            if (hud != null)
            {
                hud.ShowStatusPopup($"+{energyRecoverAmount:F0}% Năng lượng!", true);
            }
        }
    }
}

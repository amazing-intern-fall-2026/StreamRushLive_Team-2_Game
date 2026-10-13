using UnityEngine;
using SteamRush.Features.StreamIntegration;

namespace StreamRushLive.Features.Gifts.Actions
{
    /// <summary>
    /// Activates protective shield for Runner (15s duration).
    /// </summary>
    public class ShieldGiftAction : IGiftAction
    {
        public GiftActionType ActionType => GiftActionType.Blue_Shield;
        public string DisplayName => "Shield";

        public void Execute(GiftContext context)
        {
            context.GiftManager?.ActivateShield(context.SenderName, context.CustomValue);
        }
    }

    /// <summary>
    /// Activates speed boost (Sprint buff 1.5x) for Runner (30s duration).
    /// </summary>
    public class SpeedBoostGiftAction : IGiftAction
    {
        public GiftActionType ActionType => GiftActionType.Blue_SpeedBoost;
        public string DisplayName => "Speed Boost";

        public void Execute(GiftContext context)
        {
            context.GiftManager?.ActivateSprintBuff(context.SenderName, context.CustomValue);
        }
    }

    /// <summary>
    /// Activates free control mode (0 energy lane switch and jump cost for 30s).
    /// </summary>
    public class FreeControlGiftAction : IGiftAction
    {
        public GiftActionType ActionType => GiftActionType.Blue_FreeControl;
        public string DisplayName => "Free Control";

        public void Execute(GiftContext context)
        {
            context.GiftManager?.ActivateFreeControl(context.SenderName, context.CustomValue);
        }
    }

    /// <summary>
    /// Grants +300 energy directly to Blue Team.
    /// </summary>
    public class BlueEnergyBottleGiftAction : IGiftAction
    {
        public GiftActionType ActionType => GiftActionType.Blue_EnergyBottle;
        public string DisplayName => "+Blue Energy";

        public void Execute(GiftContext context)
        {
            int amount = context.CustomValue > 0f ? Mathf.RoundToInt(context.CustomValue) : -1;
            context.GiftManager?.AddBlueEnergy(context.SenderName, amount);
        }
    }
}

using UnityEngine;
using SteamRush.Features.StreamIntegration;

namespace StreamRushLive.Features.Gifts.Actions
{
    /// <summary>
    /// Activates Pickup Truck (Hunting Beasts / Animals) Phase (30s duration).
    /// </summary>
    public class SpawnPickupTruckGiftAction : IGiftAction
    {
        public GiftActionType ActionType => GiftActionType.Red_SpawnPickup;
        public string DisplayName => "Hunting Beasts";

        public void Execute(GiftContext context)
        {
            if (context.GiftManager != null)
            {
                context.GiftManager.ActivatePickupTruckPhase(context.SenderName, context.CustomValue);
            }
            else if (context.ObstacleSpawner != null)
            {
                context.ObstacleSpawner.ActivatePickupTruckPhase(context.CustomValue);
                context.ObstacleSpawner.TriggerSpawnCarTier(StreamRushLive.Features.Spawning.ObstacleType.Animals, -1, true);
            }
        }
    }

    /// <summary>
    /// Activates Heavy Truck (Train) Phase (40s duration).
    /// </summary>
    public class SpawnHeavyTruckGiftAction : IGiftAction
    {
        public GiftActionType ActionType => GiftActionType.Red_SpawnHeavyTruck;
        public string DisplayName => "Train Phase";

        public void Execute(GiftContext context)
        {
            if (context.GiftManager != null)
            {
                context.GiftManager.ActivateHeavyTruckPhase(context.SenderName, context.CustomValue);
            }
            else if (context.ObstacleSpawner != null)
            {
                context.ObstacleSpawner.ActivateHeavyTruckPhase(context.CustomValue);
                context.ObstacleSpawner.TriggerSpawnCarTier(StreamRushLive.Features.Spawning.ObstacleType.Trains, -1, true);
            }
        }
    }

    /// <summary>
    /// Activates Unlimited Cars mode (60s duration, bypasses spawn limit).
    /// </summary>
    public class UnlimitedCarsGiftAction : IGiftAction
    {
        public GiftActionType ActionType => GiftActionType.Red_UnlimitedCars;
        public string DisplayName => "Unlimited Cars";

        public void Execute(GiftContext context)
        {
            context.GiftManager?.ActivateUnlimitedCars(context.SenderName, context.CustomValue);
        }
    }

    /// <summary>
    /// Grants +500 energy directly to Red Team.
    /// </summary>
    public class RedEnergyBottleGiftAction : IGiftAction
    {
        public GiftActionType ActionType => GiftActionType.Red_EnergyBottle;
        public string DisplayName => "+Red Energy";

        public void Execute(GiftContext context)
        {
            int amount = context.CustomValue > 0f ? Mathf.RoundToInt(context.CustomValue) : -1;
            context.GiftManager?.AddRedEnergy(context.SenderName, amount);
        }
    }
}

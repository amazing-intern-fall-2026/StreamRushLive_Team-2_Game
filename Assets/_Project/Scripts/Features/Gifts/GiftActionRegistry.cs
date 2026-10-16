using System.Collections.Generic;
using UnityEngine;
using SteamRush.Features.StreamIntegration;
using StreamRushLive.Features.Gifts.Actions;

namespace StreamRushLive.Features.Gifts
{
    /// <summary>
    /// Registry managing mappings from GiftActionType to IGiftAction implementations.
    /// Provides lookup and extensibility for dynamic gift routing.
    /// </summary>
    public static class GiftActionRegistry
    {
        private static readonly Dictionary<GiftActionType, IGiftAction> _registry = new Dictionary<GiftActionType, IGiftAction>();

        static GiftActionRegistry()
        {
            RegisterDefaults();
        }

        private static void RegisterDefaults()
        {
            // Blue Team Gifts
            Register(new ShieldGiftAction());
            Register(new SpeedBoostGiftAction());
            Register(new FreeControlGiftAction());
            Register(new BlueEnergyBottleGiftAction());

            // Red Team Gifts
            Register(new SpawnPickupTruckGiftAction());
            Register(new SpawnHeavyTruckGiftAction());
            Register(new UnlimitedCarsGiftAction());
            Register(new RedEnergyBottleGiftAction());

            // Special / Interaction Gifts
            Register(new GiftDanceAction());
            Register(new RainHazardGiftAction());
            Register(new VipRelayTicketGiftAction());
            Register(new FollowRunnerGiftAction());
            Register(new LikeEnergyGiftAction());
        }

        public static void Register(IGiftAction action)
        {
            if (action == null) return;
            _registry[action.ActionType] = action;
        }

        public static IGiftAction GetAction(GiftActionType actionType)
        {
            if (_registry.TryGetValue(actionType, out var action))
            {
                return action;
            }
            return null;
        }

        public static bool HasAction(GiftActionType actionType)
        {
            return _registry.ContainsKey(actionType);
        }
    }
}

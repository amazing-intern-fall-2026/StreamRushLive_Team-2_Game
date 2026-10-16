using UnityEngine;
using SteamRush.Features.StreamIntegration;

namespace StreamRushLive.Features.Gifts.Actions
{
    /// <summary>
    /// Triggers celebration Gift Dance on Runner.
    /// </summary>
    public class GiftDanceAction : IGiftAction
    {
        public GiftActionType ActionType => GiftActionType.Special_GiftDance;
        public string DisplayName => "Celebration Dance";

        public void Execute(GiftContext context)
        {
            context.GiftManager?.TriggerGiftDance(context.SenderName, context.CustomValue);
        }
    }

    /// <summary>
    /// Activates slippery rain hazard weather on the track.
    /// </summary>
    public class RainHazardGiftAction : IGiftAction
    {
        public GiftActionType ActionType => GiftActionType.Special_RainHazard;
        public string DisplayName => "Rain Hazard";

        public void Execute(GiftContext context)
        {
            float duration = context.CustomValue > 0f ? context.CustomValue : 60f;
            context.GiftManager?.ActivateRainHazard(context.SenderName, duration);
        }
    }

    /// <summary>
    /// Enqueues viewer into runner queue / next relay runner.
    /// </summary>
    public class VipRelayTicketGiftAction : IGiftAction
    {
        public GiftActionType ActionType => GiftActionType.Special_VIPRelayTicket;
        public string DisplayName => "VIP Relay Ticket";

        public void Execute(GiftContext context)
        {
            context.QueueManager?.EnqueueFollowerAsRunner(context.SenderName);
            context.HudManager?.ShowStatusPopup($"Runner: [{context.SenderName}]", true);
        }
    }

    /// <summary>
    /// Follower action mapping to runner relay queue.
    /// </summary>
    public class FollowRunnerGiftAction : IGiftAction
    {
        public GiftActionType ActionType => GiftActionType.Follow_Runner;
        public string DisplayName => "Follow: Runner";

        public void Execute(GiftContext context)
        {
            context.QueueManager?.EnqueueFollowerAsRunner(context.SenderName);
            context.HudManager?.ShowStatusPopup($"Runner: [{context.SenderName}]", true);
        }
    }

    /// <summary>
    /// Adds like energy according to sender faction.
    /// </summary>
    public class LikeEnergyGiftAction : IGiftAction
    {
        public GiftActionType ActionType => GiftActionType.Like_Energy;
        public string DisplayName => "Like Energy";

        public void Execute(GiftContext context)
        {
            int energy = context.CustomValue > 0f ? Mathf.RoundToInt(context.CustomValue) : 20;
            if (context.Faction == FactionType.Blue)
            {
                context.FactionManager?.AddLikes(FactionType.Blue, energy);
            }
            else
            {
                context.FactionManager?.AddLikes(FactionType.Red, energy);
            }
        }
    }
}

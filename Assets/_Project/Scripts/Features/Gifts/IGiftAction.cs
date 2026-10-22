using UnityEngine;
using SteamRush.Features.StreamIntegration;
using SteamRush.Features.Runner;
using StreamRushLive.Features.Spawning;
using SteamRush.Features.UI;

namespace StreamRushLive.Features.Gifts
{
    /// <summary>
    /// Context data transferred to each gift action execution.
    /// Encapsulates event metadata, sender info, and runtime subsystem references.
    /// </summary>
    public class GiftContext
    {
        public string SenderName { get; set; } = "Viewer";
        public string SenderUserId { get; set; } = null;
        public FactionType Faction { get; set; } = FactionType.Blue;
        public int TotalCoins { get; set; } = 0;
        public float CustomValue { get; set; } = -1f;
        public string ProfilePictureUrl { get; set; } = null;

        // Runtime Subsystems & Services
        public GiftManager GiftManager { get; set; }
        public ChatRunnerQueueManager QueueManager { get; set; }
        public FactionTugOfWarManager FactionManager { get; set; }
        public SingleObstacleSpawner ObstacleSpawner { get; set; }
        public HUDManager HudManager { get; set; }
    }

    /// <summary>
    /// Contract for all standalone stream gift actions (Command / Strategy pattern).
    /// </summary>
    public interface IGiftAction
    {
        GiftActionType ActionType { get; }
        string DisplayName { get; }
        void Execute(GiftContext context);
    }
}

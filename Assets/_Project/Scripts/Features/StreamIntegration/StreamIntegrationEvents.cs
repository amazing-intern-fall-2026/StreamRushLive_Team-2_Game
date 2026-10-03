using Newtonsoft.Json.Linq;

namespace SteamRush.Features.StreamIntegration
{
    /// <summary>
    /// Published when successfully connected to the TikTok live stream room.
    /// </summary>
    public readonly struct TikTokConnectedEvent
    {
        public readonly string UniqueId;
        public readonly string RoomId;
        public readonly JObject RawState;

        public TikTokConnectedEvent(string uniqueId, string roomId, JObject rawState)
        {
            UniqueId = uniqueId;
            RoomId = roomId;
            RawState = rawState;
        }
    }

    /// <summary>
    /// Published when disconnected from the TikTok live stream.
    /// </summary>
    public readonly struct TikTokDisconnectedEvent
    {
        public readonly string Reason;

        public TikTokDisconnectedEvent(string reason)
        {
            Reason = reason;
        }
    }

    /// <summary>
    /// Published when a viewer posts a comment/chat in the live stream.
    /// </summary>
    public readonly struct TikTokChatEvent
    {
        public readonly string UserId;
        public readonly string DisplayName;
        public readonly string Comment;
        public readonly string ProfilePictureUrl;

        public TikTokChatEvent(string userId, string displayName, string comment, string profilePictureUrl = null)
        {
            UserId = userId;
            DisplayName = displayName;
            Comment = comment;
            ProfilePictureUrl = profilePictureUrl;
        }
    }

    /// <summary>
    /// Published when a viewer follows the live stream channel.
    /// </summary>
    public readonly struct TikTokFollowEvent
    {
        public readonly string UserId;
        public readonly string DisplayName;
        public readonly string ProfilePictureUrl;

        public TikTokFollowEvent(string userId, string displayName, string profilePictureUrl = null)
        {
            UserId = userId;
            DisplayName = displayName;
            ProfilePictureUrl = profilePictureUrl;
        }
    }

    /// <summary>
    /// Published when likes are sent to the live stream. TotalLike is cumulative for the user/session.
    /// </summary>
    public readonly struct TikTokLikeEvent
    {
        public readonly string UserId;
        public readonly string DisplayName;
        public readonly int TotalLike;

        public TikTokLikeEvent(string userId, string displayName, int totalLike)
        {
            UserId = userId;
            DisplayName = displayName;
            TotalLike = totalLike;
        }
    }

    /// <summary>
    /// Published when a viewer sends a TikTok gift.
    /// </summary>
    public readonly struct TikTokGiftEvent
    {
        public readonly string UserId;
        public readonly string DisplayName;
        public readonly int GiftId;
        public readonly string GiftName;
        public readonly string GiftIconUrl;
        public readonly int Coins;
        public readonly int RepeatCount;
        public readonly int TotalCoins;
        public readonly string ProfilePictureUrl;

        public TikTokGiftEvent(
            string userId,
            string displayName,
            int giftId,
            string giftName,
            string giftIconUrl,
            int coins,
            int repeatCount,
            int totalCoins,
            string profilePictureUrl = null)
        {
            UserId = userId;
            DisplayName = displayName;
            GiftId = giftId;
            GiftName = giftName;
            GiftIconUrl = giftIconUrl;
            Coins = coins;
            RepeatCount = repeatCount;
            TotalCoins = totalCoins;
            ProfilePictureUrl = profilePictureUrl;
        }
    }

    /// <summary>
    /// Published when a viewer shares the live stream.
    /// </summary>
    public readonly struct TikTokShareEvent
    {
        public readonly string UserId;
        public readonly string DisplayName;

        public TikTokShareEvent(string userId, string displayName)
        {
            UserId = userId;
            DisplayName = displayName;
        }
    }
}

using UnityEngine;

namespace SteamRush.Features.UI.PreGameConfig
{
    /// <summary>
    /// Legacy builder forwarder retained for backwards compatibility.
    /// Delegates gift icon resolution to PreGameGiftIconResolver.
    /// Procedural UI construction has been replaced with Prefab assets.
    /// </summary>
    public static class PreGameUIBuilder
    {
        public static Sprite ResolveGiftIcon(int giftId, string giftName)
        {
            return PreGameGiftIconResolver.ResolveGiftIcon(giftId, giftName);
        }
    }
}

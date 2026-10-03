using System;
using System.Collections.Generic;

namespace SteamRush.Features.StreamIntegration
{
    // In-memory registry of users who followed during the active live stream session.
    // Identifier key is userId. Pure C# class, called on Unity main thread via OnUnityThread.
    public class TikTokFollowerRegistry : IFollowerStatusProvider
    {
        private readonly HashSet<string> _followerIds = new HashSet<string>();

        // Invoked when a new follower is registered.
        public event Action<string> FollowerAdded;

        public int Count => _followerIds.Count;

        public bool IsFollower(string userId)
        {
            return !string.IsNullOrEmpty(userId) && _followerIds.Contains(userId);
        }

        // Returns true if new follower. Must be called before checking queue access.
        public bool AddFollower(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            bool added = _followerIds.Add(userId);
            if (added)
            {
                FollowerAdded?.Invoke(userId);
            }

            return added;
        }

        public void Clear()
        {
            _followerIds.Clear();
        }

        // Registers this instance as the shared follower provider for FollowerGate.
        public void Register()
        {
            FollowerGate.SetSharedProvider(this);
        }

        // Unregisters on disconnect, reverting FollowerGate to mock passthrough for offline testing.
        public void Unregister()
        {
            FollowerGate.ClearSharedProvider(this);
        }
    }
}
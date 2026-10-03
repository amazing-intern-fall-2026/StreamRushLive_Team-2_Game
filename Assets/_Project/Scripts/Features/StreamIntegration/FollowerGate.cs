using UnityEngine;
using SteamRush.Core;

namespace SteamRush.Features.StreamIntegration
{
    // Provides live stream follower status through this interface.
    public interface IFollowerStatusProvider
    {
        bool IsFollower(string userId);
    }

    // Controls follower gating for queue entry and chat commands.
    public class FollowerGate
    {
        // Shared provider registered automatically by TikTokFollowerRegistry upon connection.
        private static IFollowerStatusProvider _sharedProvider;

        /// <summary>
        /// When false (default): Anyone can play without follow restriction.
        /// When true: Only viewers who follow during the live stream can control or join the queue.
        /// </summary>
        public static bool StrictFollowerOnly = false;

        private readonly IFollowerStatusProvider _followerStatusProvider;

        public FollowerGate(IFollowerStatusProvider followerStatusProvider = null)
        {
            _followerStatusProvider = followerStatusProvider;
        }

        public static void SetSharedProvider(IFollowerStatusProvider provider)
        {
            _sharedProvider = provider;
        }

        public static void ClearSharedProvider(IFollowerStatusProvider provider)
        {
            if (_sharedProvider == provider)
            {
                _sharedProvider = null;
            }
        }

        public bool CanJoinQueue(string userId)
        {
            return IsFollower(userId);
        }

        public bool CanSendCommand(string userId)
        {
            bool isFollower = IsFollower(userId);

            if (!isFollower)
            {
                Debug.Log($"[FollowerGate] {userId} is not following - ignored command.");
                EventBus.Publish(new NonFollowerCommandRejectedEvent(userId));
            }

            return isFollower;
        }

        private bool IsFollower(string userId)
        {
            if (!StrictFollowerOnly)
            {
                return true;
            }

            IFollowerStatusProvider provider = _followerStatusProvider ?? _sharedProvider;

            if (provider == null)
            {
                return true;
            }

            return provider.IsFollower(userId);
        }
    }
}
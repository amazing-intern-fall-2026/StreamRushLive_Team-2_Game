using System;
using UnityEngine;
using UnityEngine.Events;
using SteamRush.Core;
using SteamRush.Features.Runner;
using SteamRush.Features.UI;

namespace SteamRush.Features.StreamIntegration
{
    /// <summary>
    /// Listens for TikTok follow events via EventBus.
    /// Registers followers in the session registry and enqueues new followers into the Runner relay queue.
    /// </summary>
    [DisallowMultipleComponent]
    public class TikTokFollowAdapter : MonoBehaviour
    {
        [Serializable]
        public class FollowerJoinedEvent : UnityEvent<string, string> { }

        [Header("Relay Queue & UI")]
        [SerializeField] private ChatRunnerQueueManager _queueManager;
        [SerializeField] private HUDManager _hudManager;

        [Header("Diagnostics & Audio")]
        [SerializeField] private bool _showPopups = true;
        [SerializeField] private bool _logEvents = true;

        [Header("Events")]
        [SerializeField] private FollowerJoinedEvent _followerJoined = new FollowerJoinedEvent();
        public FollowerJoinedEvent FollowerJoined => _followerJoined;

        private readonly TikTokFollowerRegistry _followers = new TikTokFollowerRegistry();
        public TikTokFollowerRegistry Followers => _followers;

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            _followers.Register();
            EventBus.Subscribe<TikTokFollowEvent>(OnFollowReceived);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TikTokFollowEvent>(OnFollowReceived);
            _followers.Unregister();
        }

        public void EnsureReferences()
        {
            if (_queueManager == null) _queueManager = FindFirstObjectByType<ChatRunnerQueueManager>();
            if (_hudManager == null) _hudManager = FindFirstObjectByType<HUDManager>();
        }

        public void ClearFollowers()
        {
            _followers.Clear();
        }

        private void OnFollowReceived(TikTokFollowEvent evt)
        {
            EnsureReferences();

            if (string.IsNullOrEmpty(evt.UserId))
            {
                return;
            }

            string displayName = !string.IsNullOrEmpty(evt.DisplayName) ? evt.DisplayName : evt.UserId;
            bool isNew = _followers.AddFollower(evt.UserId);

            if (_logEvents)
            {
                Debug.Log($"[TikTokFollowAdapter] FOLLOW: {displayName} ({evt.UserId}) - {(isNew ? "New" : "Existing")}");
            }

            if (!isNew)
            {
                return;
            }

            if (_queueManager != null)
            {
                _queueManager.EnqueueFollowerAsRunner(displayName);
            }

            if (_showPopups && _hudManager != null)
            {
                _hudManager.ShowStatusPopup($"[{displayName}] Followed -> Next Runner!", true);
            }

            var giftPanel = FindFirstObjectByType<SteamRush.Features.UI.Views.GiftInfoPanelController>();
            giftPanel?.HighlightGift(0, "Follow");

            _followerJoined?.Invoke(evt.UserId, displayName);
            AudioManager.Instance?.PlaySFX(SFXType.StreamNewFollower);
        }
    }
}

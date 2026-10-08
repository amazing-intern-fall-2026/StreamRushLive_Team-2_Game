using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using UnityEngine;
using SteamRush.Core;

namespace SteamRush.Features.StreamIntegration
{
    /// <summary>
    /// Network connection hub for TikTok Live over Socket.IO.
    /// Dedicated solely to managing backend connection lifecycle, raw packet parsing,
    /// and dispatching strongly-typed events via EventBus.
    /// Specific gameplay logic is handled by modular adapters (Chat, Like, Follow, Gift, Profile).
    /// </summary>
    [DisallowMultipleComponent]
    public class TikTokLiveClient : MonoBehaviour
    {
        [Header("Backend Connection")]
        [Tooltip("TikTok Live backend Socket.IO URL (default port: 3001).")]
        [SerializeField] private string _serverUrl = "http://localhost:3001";

        [Tooltip("TikTok username of the active live stream (without @).")]
        [SerializeField] private string _tiktokUniqueId = "";

        [Tooltip("Auto-connect to live backend on Start.")]
        [SerializeField] private bool _connectOnStart = false;

        [Tooltip("Enforce WebSocket transport only.")]
        [SerializeField] private bool _webSocketOnly = true;

        [Header("Socket Event Names")]
        [SerializeField] private string _setUniqueIdEvent = "setUniqueID";
        [SerializeField] private string _chatEvent = "chat";
        [SerializeField] private string _followEvent = "follow";
        [SerializeField] private string _likeEvent = "like";
        [SerializeField] private string _giftEvent = "gift";

        [Header("JSON Property Paths")]
        [SerializeField] private string _userIdPath = "data.user.userId";
        [SerializeField] private string _nicknamePath = "data.user.nickname";
        [SerializeField] private string _uniqueIdPath = "data.user.uniqueId";
        [SerializeField] private string _commentPath = "comment";
        [SerializeField] private string _totalLikePath = "totalLike";
        [SerializeField] private string _giftNamePath = "giftName";
        [SerializeField] private string _giftIdPath = "giftId";
        [SerializeField] private string _diamondCountPath = "diamondCount";
        [SerializeField] private string _coinCountPath = "coinCount";
        [SerializeField] private string _repeatCountPath = "repeatCount";

        [Header("Diagnostics")]
        [SerializeField] private bool _logEvents = true;

        [Header("Event Timing Safeguard")]
        [Tooltip("If TRUE: Discards historical/buffered events from before connection to only process fresh stream data.")]
        [SerializeField] private bool _ignorePastEventsOnConnect = true;

        private long _sessionConnectUnixMs = 0;

        [Header("Modular Adapters (Auto-Resolved)")]
        [SerializeField] private TikTokChatAdapter _chatAdapter;
        [SerializeField] private TikTokLikeAdapter _likeAdapter;
        [SerializeField] private TikTokFollowAdapter _followAdapter;
        [SerializeField] private TikTokGiftRouter _giftRouter;
        [SerializeField] private TikTokProfileSync _profileSync;

        private SocketIOUnity _socket;

        #region Public Properties & Backward Compatibility

        public bool IsConnected => _socket != null && _socket.Connected;
        public bool IsTikTokLiveConnected { get; private set; }
        public string CurrentRoomId { get; private set; }
        public string ServerUrl => _serverUrl;
        public string TikTokUniqueId => _tiktokUniqueId;

        public string HostDisplayName => _profileSync != null ? _profileSync.HostDisplayName : string.Empty;
        public Sprite HostAvatarSprite => _profileSync != null ? _profileSync.HostAvatarSprite : null;
        public List<TikTokGiftMapping> GiftMappings => _giftRouter != null ? _giftRouter.GiftMappings : null;
        public TikTokFollowerRegistry Followers => _followAdapter != null ? _followAdapter.Followers : null;

        public bool RequireFollowToPlay
        {
            get => _chatAdapter != null ? _chatAdapter.RequireFollowToPlay : FollowerGate.StrictFollowerOnly;
            set
            {
                if (_chatAdapter != null) _chatAdapter.RequireFollowToPlay = value;
                FollowerGate.StrictFollowerOnly = value;
            }
        }

        public bool SyncHostAsInitialRunner
        {
            get => _profileSync != null && _profileSync.SyncHostAsInitialRunner;
            set
            {
                if (_profileSync != null) _profileSync.SyncHostAsInitialRunner = value;
            }
        }

        public void AddGiftMapping(TikTokGiftMapping mapping)
        {
            _giftRouter?.AddGiftMapping(mapping);
        }

        public bool RemoveGiftMapping(int giftId, string giftName = null)
        {
            return _giftRouter != null && _giftRouter.RemoveGiftMapping(giftId, giftName);
        }

        public void SetServerUrl(string url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                _serverUrl = url.Trim();
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureAdapters();
            // Automatically correct old HTTP port references (9090 or 9091) to Socket.IO port 3001
            if (!string.IsNullOrEmpty(_serverUrl) && (_serverUrl.Contains(":9090") || _serverUrl.Contains(":9091")))
            {
                _serverUrl = "http://localhost:3001";
            }
        }

        private void Start()
        {
            EnsureAdapters();
        }

        private void OnEnable()
        {
            EnsureAdapters();
            if (_connectOnStart)
            {
                Connect();
            }
        }

        private void OnDisable()
        {
            Disconnect();
        }

        #endregion

        #region Adapter Setup

        /// <summary>
        /// Ensures all 5 modular adapters are linked to this client.
        /// </summary>
        [ContextMenu("Setup Modular Adapters")]
        public void EnsureAdapters()
        {
            if (_chatAdapter == null && !TryGetComponent(out _chatAdapter))
            {
                _chatAdapter = gameObject.AddComponent<TikTokChatAdapter>();
            }

            if (_likeAdapter == null && !TryGetComponent(out _likeAdapter))
            {
                _likeAdapter = gameObject.AddComponent<TikTokLikeAdapter>();
            }

            if (_followAdapter == null && !TryGetComponent(out _followAdapter))
            {
                _followAdapter = gameObject.AddComponent<TikTokFollowAdapter>();
            }

            if (_giftRouter == null && !TryGetComponent(out _giftRouter))
            {
                _giftRouter = gameObject.AddComponent<TikTokGiftRouter>();
            }

            if (_profileSync == null && !TryGetComponent(out _profileSync))
            {
                _profileSync = gameObject.AddComponent<TikTokProfileSync>();
            }
            if (_profileSync != null)
            {
                _profileSync.EnsureReferences();
            }
        }

        #endregion

        #region Connection Management

        public void ConnectWithUsername(string username)
        {
            string normalized = NormalizeUniqueId(username);
            string previousUser = NormalizeUniqueId(_tiktokUniqueId);

            // If already connected to the same username on the same backend, reuse the active connection
            if (IsConnected && IsTikTokLiveConnected && string.Equals(previousUser, normalized, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[TikTokLiveClient] Already connected to active stream @{normalized}. Reusing existing live connection.");
                return;
            }

            if (!string.IsNullOrEmpty(normalized))
            {
                _tiktokUniqueId = normalized;
            }

            Disconnect();
            Connect();
        }

        [ContextMenu("Connect")]
        public void Connect()
        {
            if (_socket != null) return;
            _sessionConnectUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            string uniqueId = NormalizeUniqueId(_tiktokUniqueId);
            if (string.IsNullOrEmpty(uniqueId))
            {
                Debug.LogWarning("[TikTokLiveClient] TikTok Unique ID is empty - skipping auto connect.");
                return;
            }

            EnsureAdapters();
            if (_likeAdapter != null) _likeAdapter.ResetProgress();
            if (_followAdapter != null) _followAdapter.ClearFollowers();

            try
            {
                var options = new SocketIOOptions();
                if (_webSocketOnly)
                {
                    options.Transport = SocketIOClient.Transport.TransportProtocol.WebSocket;
                }

                _socket = new SocketIOUnity(new Uri(_serverUrl), options);
                _socket.JsonSerializer = new NewtonsoftJsonSerializer();

                _socket.OnConnected += (sender, e) =>
                {
                    Debug.Log($"[TikTokLiveClient] Connected socket to {_serverUrl}. Sent {_setUniqueIdEvent} = '{uniqueId}'");
                    _socket.Emit(_setUniqueIdEvent, uniqueId);
                };

                _socket.OnDisconnected += (sender, reason) =>
                {
                    Debug.Log($"[TikTokLiveClient] Lost backend connection: {reason}");
                    EventBus.Publish(new TikTokDisconnectedEvent(reason));
                };

                _socket.OnError += (sender, error) =>
                {
                    Debug.LogWarning($"[TikTokLiveClient] Socket error: {error}");
                };

                // Dispatch socket payloads on the Unity main thread for thread-safe EventBus publishing
                _socket.OnUnityThread(_chatEvent, OnRawChatReceived);
                _socket.OnUnityThread(_followEvent, OnRawFollowReceived);
                _socket.OnUnityThread(_likeEvent, OnRawLikeReceived);
                _socket.OnUnityThread(_giftEvent, OnRawGiftReceived);
                _socket.OnUnityThread("share", OnRawShareReceived);
                _socket.OnUnityThread("tiktokConnected", OnRawTikTokConnected);
                _socket.OnUnityThread("tiktokDisconnected", OnRawTikTokDisconnected);
                _socket.OnUnityThread("roomInfo", OnRawRoomInfoReceived);
                _socket.OnUnityThread("connected", OnRawRoomInfoReceived);
                _socket.OnUnityThread("streamerInfo", OnRawRoomInfoReceived);

                _socket.Connect();
                if (_logEvents)
                {
                    Debug.Log($"[TikTokLiveClient] Connecting to {_serverUrl}...");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TikTokLiveClient] Failed to connect: {ex.Message}");
                Disconnect();
            }
        }

        [ContextMenu("Disconnect")]
        public void Disconnect()
        {
            IsTikTokLiveConnected = false;
            CurrentRoomId = "";
            _sessionConnectUnixMs = 0;

            if (_socket != null)
            {
                try
                {
                    _socket.Disconnect();
                    _socket.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TikTokLiveClient] Error while disconnecting: {ex.Message}");
                }
                _socket = null;
            }
        }

        #endregion

        #region Raw Socket Listeners -> EventBus Dispatches

        private void OnRawChatReceived(SocketIOResponse response)
        {
            JObject json = ParseResponse(response);
            if (json == null || IsHistoricalEvent(json, "chat")) return;

            string comment = ReadStringWithFallback(json, _commentPath, "comment", "text");
            string userId = ReadStringWithFallback(json, _userIdPath, "userId", "uniqueId", "data.user.uniqueId");
            if (string.IsNullOrEmpty(comment) || string.IsNullOrEmpty(userId)) return;

            string displayName = GetDisplayName(json, userId);
            string avatar = ReadStringWithFallback(json, "data.user.profilePictureUrl", "profilePictureUrl");

            EventBus.Publish(new TikTokChatEvent(userId, displayName, comment, avatar));
        }

        private void OnRawFollowReceived(SocketIOResponse response)
        {
            JObject json = ParseResponse(response);
            if (json == null || IsHistoricalEvent(json, "follow")) return;

            string userId = ReadStringWithFallback(json, _userIdPath, "userId", "uniqueId", "data.user.uniqueId");
            if (string.IsNullOrEmpty(userId)) return;

            string displayName = GetDisplayName(json, userId);
            string avatar = ReadStringWithFallback(json, "data.user.profilePictureUrl", "profilePictureUrl");

            EventBus.Publish(new TikTokFollowEvent(userId, displayName, avatar));
        }

        private void OnRawLikeReceived(SocketIOResponse response)
        {
            JObject json = ParseResponse(response);
            if (json == null || IsHistoricalEvent(json, "like")) return;

            string userId = ReadStringWithFallback(json, _userIdPath, "userId", "uniqueId", "data.user.uniqueId");
            string totalLikeRaw = ReadStringWithFallback(json, _totalLikePath, "totalLike", "totalLikeCount", "likeCount");
            if (string.IsNullOrEmpty(userId) || !int.TryParse(totalLikeRaw, out int totalLike)) return;

            string displayName = GetDisplayName(json, userId);

            EventBus.Publish(new TikTokLikeEvent(userId, displayName, totalLike));
        }

        private void OnRawGiftReceived(SocketIOResponse response)
        {
            JObject json = ParseResponse(response);
            if (json == null || IsHistoricalEvent(json, "gift")) return;

            string userId = ReadStringWithFallback(json, _userIdPath, "userId", "uniqueId", "data.user.uniqueId");
            string giftName = ReadStringWithFallback(json, _giftNamePath, "giftName", "data.giftName");
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(giftName)) return;

            string displayName = GetDisplayName(json, userId);

            int coins = 1;
            string coinStr = ReadStringWithFallback(json, _diamondCountPath, _coinCountPath, "cointCount", "totalCoins");
            if (!string.IsNullOrEmpty(coinStr) && int.TryParse(coinStr, out int parsedCoins))
            {
                coins = parsedCoins;
            }

            int repeatCount = 1;
            string repeatStr = ReadStringWithFallback(json, _repeatCountPath, "repeatCount");
            if (!string.IsNullOrEmpty(repeatStr) && int.TryParse(repeatStr, out int parsedRepeat))
            {
                repeatCount = Mathf.Max(1, parsedRepeat);
            }

            int totalValue = coins * repeatCount;
            string totalCoinsStr = ReadString(json, "totalCoins");
            if (!string.IsNullOrEmpty(totalCoinsStr) && int.TryParse(totalCoinsStr, out int parsedTotal) && parsedTotal > 0)
            {
                totalValue = parsedTotal;
            }

            int giftId = 0;
            string giftIdStr = ReadStringWithFallback(json, _giftIdPath, "giftId", "gift.id", "data.giftId", "gift_id");
            if (!string.IsNullOrEmpty(giftIdStr) && int.TryParse(giftIdStr, out int parsedGiftId))
            {
                giftId = parsedGiftId;
            }

            string giftIconUrl = ReadStringWithFallback(json, "giftIconUrl", "data.giftIconUrl", "giftPictureUrl", "gift.icon.url_list[0]");
            string avatar = ReadStringWithFallback(json, "data.user.profilePictureUrl", "profilePictureUrl");

            EventBus.Publish(new TikTokGiftEvent(
                userId, displayName, giftId, giftName, giftIconUrl, coins, repeatCount, totalValue, avatar));
        }

        private void OnRawShareReceived(SocketIOResponse response)
        {
            JObject json = ParseResponse(response);
            if (json == null || IsHistoricalEvent(json, "share")) return;

            string userId = ReadStringWithFallback(json, _userIdPath, "userId", "uniqueId", "data.user.uniqueId");
            string displayName = GetDisplayName(json, userId);

            EventBus.Publish(new TikTokShareEvent(userId, displayName));
        }

        private void OnRawTikTokConnected(SocketIOResponse response)
        {
            _sessionConnectUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string channel = NormalizeUniqueId(_tiktokUniqueId);
            Debug.Log($"[TikTokLiveClient] Connected to TikTok Live successfully! Channel: @{channel} (Session Start: {_sessionConnectUnixMs})");

            JObject json = ParseResponse(response);
            string roomId = ReadStringWithFallback(json, "roomId", "room_id", "data.roomId");
            IsTikTokLiveConnected = true;
            CurrentRoomId = roomId;
            EventBus.Publish(new TikTokConnectedEvent(channel, roomId, json));
        }

        private void OnRawTikTokDisconnected(SocketIOResponse response)
        {
            string reason = response != null ? response.ToString() : "Disconnected";
            Debug.LogWarning($"[TikTokLiveClient] TikTok Live disconnected: {reason}");
            IsTikTokLiveConnected = false;
            CurrentRoomId = "";
            EventBus.Publish(new TikTokDisconnectedEvent(reason));
        }

        private void OnRawRoomInfoReceived(SocketIOResponse response)
        {
            JObject json = ParseResponse(response);
            if (json == null) return;

            string channel = NormalizeUniqueId(_tiktokUniqueId);
            string roomId = ReadStringWithFallback(json, "roomId", "room_id", "data.roomId");
            IsTikTokLiveConnected = true;
            CurrentRoomId = roomId;
            EventBus.Publish(new TikTokConnectedEvent(channel, roomId, json));
        }

        #endregion

        #region Helpers

        private string GetDisplayName(JObject json, string fallbackUserId)
        {
            string nickname = ReadStringWithFallback(json, _nicknamePath, "nickname", "data.user.nickname");
            if (!string.IsNullOrEmpty(nickname)) return nickname;

            string uniqueId = ReadStringWithFallback(json, _uniqueIdPath, "uniqueId", "data.user.uniqueId");
            return string.IsNullOrEmpty(uniqueId) ? fallbackUserId : uniqueId;
        }

        private static JObject ParseResponse(SocketIOResponse response)
        {
            if (response == null) return null;
            try
            {
                return response.GetValue<JObject>();
            }
            catch
            {
                try
                {
                    JArray array = JArray.Parse(response.ToString());
                    return array.Count > 0 ? array[0] as JObject : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        private static string ReadString(JObject json, string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            JToken token = json.SelectToken(path);
            return token == null || token.Type == JTokenType.Null ? string.Empty : token.ToString();
        }

        private static string ReadStringWithFallback(JObject json, params string[] paths)
        {
            if (json == null || paths == null) return string.Empty;
            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                JToken token = json.SelectToken(path);
                if (token != null && token.Type != JTokenType.Null && !string.IsNullOrWhiteSpace(token.ToString()))
                {
                    return token.ToString().Trim();
                }
            }
            return string.Empty;
        }

        private static string NormalizeUniqueId(string raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().TrimStart('@');
        }

        private static long ReadLongWithFallback(JObject json, params string[] paths)
        {
            if (json == null || paths == null) return 0;
            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                JToken token = json.SelectToken(path);
                if (token != null && token.Type != JTokenType.Null)
                {
                    if (long.TryParse(token.ToString().Trim(), out long val))
                    {
                        return val;
                    }
                }
            }
            return 0;
        }

        private bool IsHistoricalEvent(JObject json, string eventType = "event")
        {
            if (!_ignorePastEventsOnConnect || _sessionConnectUnixMs <= 0 || json == null) return false;

            long eventTime = ReadLongWithFallback(json, "createTime", "timestamp");
            if (eventTime > 0)
            {
                if (eventTime < 10000000000L) eventTime *= 1000L;
                if (eventTime < _sessionConnectUnixMs - 3000L)
                {
                    if (_logEvents)
                    {
                        Debug.Log($"[TikTokLiveClient] Ignored historical {eventType} (eventTime: {eventTime}, sessionStart: {_sessionConnectUnixMs}).");
                    }
                    return true;
                }
            }
            return false;
        }

        #endregion
    }
}
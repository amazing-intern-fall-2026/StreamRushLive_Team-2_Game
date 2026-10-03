using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using UnityEngine;
using SteamRush.Core;
using StreamRushLive.Features.Gifts;
using StreamRushLive.Features.Spawning;
using SteamRush.Features.Runner;
using SteamRush.Features.UI;
using SteamRush.Features.UI.Views;

namespace SteamRush.Features.StreamIntegration
{
    public enum GiftActionType
    {
        [InspectorName("Blue: Shield")]
        Blue_Shield,

        [InspectorName("Blue: Speed Boost")]
        Blue_SpeedBoost,

        [InspectorName("Blue: Free Control")]
        Blue_FreeControl,

        [InspectorName("Blue: +Energy")]
        Blue_EnergyBottle,

        [InspectorName("Red: Pickup Truck")]
        Red_SpawnPickup,

        [InspectorName("Red: Heavy Truck")]
        Red_SpawnHeavyTruck,

        [InspectorName("Red: Unlimited Cars")]
        Red_UnlimitedCars,

        [InspectorName("Red: +Energy")]
        Red_EnergyBottle,

        [InspectorName("Special: Meme Dance")]
        Special_GiftDance,

        [InspectorName("Special: Rain Hazard")]
        Special_RainHazard,

        [InspectorName("Special: VIP Ticket")]
        Special_VIPRelayTicket,

        [InspectorName("Dynamic: By Faction")]
        Dynamic_ByFaction,

        [InspectorName("Like: +Energy")]
        Like_Energy
    }

    [System.Serializable]
    public class TikTokGiftMapping
    {
        [Tooltip("TikTok Gift ID (e.g. 5655 for Rose, 5269 for TikTok). Match the exact ID to link effects.")]
        public int giftId = 0;

        [Tooltip("Gift name for identification in Inspector (e.g. Rose, Donut, Lion, Cap).")]
        public string giftName = "Rose";

        [Tooltip("Sprite icon for this gift (leaves empty to auto-resolve official TikTok icon).")]
        public Sprite giftIcon;

        [Tooltip("Description displayed directly on the in-game gift card UI.")]
        [UnityEngine.Serialization.FormerlySerializedAs("englishDescription")]
        public string description = "+300 Blue Energy";

        [Tooltip("Gameplay action triggered when viewers send this gift.")]
        public GiftActionType action = GiftActionType.Blue_EnergyBottle;

        [Tooltip("Custom value: Duration (seconds) or Energy amount. Set 0 for default.")]
        public float customValue = 0f;
    }

    /// <summary>
    /// Network connection hub for TikTok Live over Socket.IO.
    /// Manages socket lifecycle, parses raw incoming events, and broadcasts strongly-typed events via EventBus.
    /// Automatically connects modular adapters (Chat, Like, Follow, Gift, Profile) for gameplay routing.
    /// </summary>
    [DisallowMultipleComponent]
    public class TikTokLiveClient : MonoBehaviour
    {
        [Header("Backend Connection")]
        [Tooltip("TikTok Live backend server URL (default: http://localhost:9090).")]
        [SerializeField] private string _serverUrl = "http://localhost:9090";

        [Tooltip("TikTok username of the active live stream (without @).")]
        [SerializeField] private string _tiktokUniqueId = "";

        [Tooltip("Auto-connect to live backend on Start.")]
        [SerializeField] private bool _connectOnStart = true;

        [Tooltip("Enforce WebSocket transport only.")]
        [SerializeField] private bool _webSocketOnly = true;

        [Header("Host Profile")]
        [SerializeField] private bool _syncHostAsInitialRunner = true;
        [SerializeField] private string _hostDisplayName = "";
        [SerializeField] private Sprite _hostAvatarSprite;

        [Header("Follower Requirement")]
        [Tooltip("If TRUE: Viewers must follow on TikTok Live to play and send chat commands. If FALSE (default): Anyone in the live stream can play immediately.")]
        [SerializeField] private bool _requireFollowToPlay = false;

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

        [Header("TikTok Gift Mappings (Migrated to TikTokGiftRouter)")]
        [SerializeField] private List<TikTokGiftMapping> _giftMappings = new List<TikTokGiftMapping>();

        [Header("Likes to Energy Settings")]
        [SerializeField] private int _likesPerEnergyStep = 20;
        [SerializeField] private int _energyPerStep = 5;

        [Header("Subsystem References")]
        [SerializeField] private FactionTugOfWarManager _factionManager;
        [SerializeField] private HUDManager _hudManager;
        [SerializeField] private ChatLaneRunnerController _runnerController;
        [SerializeField] private ChatRunnerQueueManager _queueManager;
        [SerializeField] private GiftManager _giftManager;
        [SerializeField] private GiftInfoPanelController _giftPanelController;

        [Header("Display & Logs")]
        [SerializeField] private bool _showPopups = true;
        [SerializeField] private bool _logEvents = true;

        [Header("Modular Adapters")]
        [SerializeField] private TikTokChatAdapter _chatAdapter;
        [SerializeField] private TikTokLikeAdapter _likeAdapter;
        [SerializeField] private TikTokFollowAdapter _followAdapter;
        [SerializeField] private TikTokGiftRouter _giftRouter;
        [SerializeField] private TikTokProfileSync _profileSync;

        private SocketIOUnity _socket;
        private readonly TikTokFollowerRegistry _fallbackFollowers = new TikTokFollowerRegistry();

        #region Public Properties

        public bool IsConnected => _socket != null && _socket.Connected;
        public string ServerUrl => _serverUrl;
        public string TikTokUniqueId => _tiktokUniqueId;
        public string HostDisplayName => _profileSync != null ? _profileSync.HostDisplayName : _hostDisplayName;
        public Sprite HostAvatarSprite => _profileSync != null ? _profileSync.HostAvatarSprite : _hostAvatarSprite;
        public TikTokFollowerRegistry Followers => _followAdapter != null ? _followAdapter.Followers : _fallbackFollowers;

        public bool RequireFollowToPlay
        {
            get => _requireFollowToPlay;
            set
            {
                _requireFollowToPlay = value;
                FollowerGate.StrictFollowerOnly = value;
                if (_chatAdapter != null) _chatAdapter.RequireFollowToPlay = value;
            }
        }

        public bool SyncHostAsInitialRunner
        {
            get => _syncHostAsInitialRunner;
            set
            {
                _syncHostAsInitialRunner = value;
                if (_profileSync != null) _profileSync.SyncHostAsInitialRunner = value;
            }
        }

        public List<TikTokGiftMapping> GiftMappings
        {
            get => _giftRouter != null ? _giftRouter.GiftMappings : _giftMappings;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureAdapters();
            FollowerGate.StrictFollowerOnly = _requireFollowToPlay;
        }

        private void Start()
        {
            EnsureAdapters();
            FollowerGate.StrictFollowerOnly = _requireFollowToPlay;
        }

        private void OnEnable()
        {
            FollowerGate.StrictFollowerOnly = _requireFollowToPlay;
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

        #region Adapter Setup & Compatibility

        /// <summary>
        /// Ensures all 5 modular adapters are attached to this GameObject and initialized with inspector settings.
        /// </summary>
        [ContextMenu("Setup Modular Adapters")]
        public void EnsureAdapters()
        {
            if (_chatAdapter == null && !TryGetComponent(out _chatAdapter))
            {
                _chatAdapter = gameObject.AddComponent<TikTokChatAdapter>();
            }
            if (_chatAdapter != null)
            {
                _chatAdapter.RequireFollowToPlay = _requireFollowToPlay;
                _chatAdapter.EnsureReferences();
            }

            if (_likeAdapter == null && !TryGetComponent(out _likeAdapter))
            {
                _likeAdapter = gameObject.AddComponent<TikTokLikeAdapter>();
            }
            if (_likeAdapter != null)
            {
                _likeAdapter.LikesPerEnergyStep = _likesPerEnergyStep;
                _likeAdapter.EnergyPerStep = _energyPerStep;
                _likeAdapter.EnsureReferences();
            }

            if (_followAdapter == null && !TryGetComponent(out _followAdapter))
            {
                _followAdapter = gameObject.AddComponent<TikTokFollowAdapter>();
            }
            if (_followAdapter != null)
            {
                _followAdapter.EnsureReferences();
            }

            if (_giftRouter == null && !TryGetComponent(out _giftRouter))
            {
                _giftRouter = gameObject.AddComponent<TikTokGiftRouter>();
            }
            if (_giftRouter != null)
            {
                if ((_giftRouter.GiftMappings == null || _giftRouter.GiftMappings.Count == 0) && _giftMappings != null && _giftMappings.Count > 0)
                {
                    _giftRouter.SetGiftMappings(_giftMappings);
                }
                _giftRouter.EnsureReferences();
            }

            if (_profileSync == null && !TryGetComponent(out _profileSync))
            {
                _profileSync = gameObject.AddComponent<TikTokProfileSync>();
            }
            if (_profileSync != null)
            {
                _profileSync.TikTokUniqueId = _tiktokUniqueId;
                _profileSync.SyncHostAsInitialRunner = _syncHostAsInitialRunner;
                _profileSync.EnsureReferences();
            }
        }

        public void AddGiftMapping(TikTokGiftMapping mapping)
        {
            if (_giftRouter != null)
            {
                _giftRouter.AddGiftMapping(mapping);
            }
            else if (mapping != null)
            {
                _giftMappings.Add(mapping);
            }
        }

        public bool RemoveGiftMapping(int giftId, string giftName = null)
        {
            if (_giftRouter != null)
            {
                return _giftRouter.RemoveGiftMapping(giftId, giftName);
            }

            return _giftMappings.RemoveAll(m =>
                (giftId > 0 && m.giftId == giftId) ||
                (!string.IsNullOrEmpty(giftName) && string.Equals(m.giftName, giftName, StringComparison.OrdinalIgnoreCase))) > 0;
        }

        #endregion

        #region Connection Management

        [ContextMenu("Connect")]
        public void Connect()
        {
            if (_socket != null) return;

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
            if (json == null) return;

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
            if (json == null) return;

            string userId = ReadStringWithFallback(json, _userIdPath, "userId", "uniqueId", "data.user.uniqueId");
            if (string.IsNullOrEmpty(userId)) return;

            string displayName = GetDisplayName(json, userId);
            string avatar = ReadStringWithFallback(json, "data.user.profilePictureUrl", "profilePictureUrl");

            EventBus.Publish(new TikTokFollowEvent(userId, displayName, avatar));
        }

        private void OnRawLikeReceived(SocketIOResponse response)
        {
            JObject json = ParseResponse(response);
            if (json == null) return;

            string userId = ReadStringWithFallback(json, _userIdPath, "userId", "uniqueId", "data.user.uniqueId");
            string totalLikeRaw = ReadStringWithFallback(json, _totalLikePath, "totalLike", "totalLikeCount", "likeCount");
            if (string.IsNullOrEmpty(userId) || !int.TryParse(totalLikeRaw, out int totalLike)) return;

            string displayName = GetDisplayName(json, userId);

            EventBus.Publish(new TikTokLikeEvent(userId, displayName, totalLike));
        }

        private void OnRawGiftReceived(SocketIOResponse response)
        {
            JObject json = ParseResponse(response);
            if (json == null) return;

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
            if (json == null) return;

            string userId = ReadStringWithFallback(json, _userIdPath, "userId", "uniqueId", "data.user.uniqueId");
            string displayName = GetDisplayName(json, userId);

            EventBus.Publish(new TikTokShareEvent(userId, displayName));
            if (_showPopups && _hudManager != null)
            {
                _hudManager.ShowStatusPopup($"[{displayName}] shared the livestream!", true);
            }
        }

        private void OnRawTikTokConnected(SocketIOResponse response)
        {
            string channel = NormalizeUniqueId(_tiktokUniqueId);
            Debug.Log($"[TikTokLiveClient] Connected to TikTok Live successfully! Channel: @{channel}");
            if (_showPopups && _hudManager != null)
            {
                _hudManager.ShowStatusPopup($"TikTok Live: @{channel} connected!", true);
            }

            JObject json = ParseResponse(response);
            string roomId = ReadStringWithFallback(json, "roomId", "room_id", "data.roomId");
            EventBus.Publish(new TikTokConnectedEvent(channel, roomId, json));
        }

        private void OnRawTikTokDisconnected(SocketIOResponse response)
        {
            string reason = response != null ? response.ToString() : "Disconnected";
            Debug.LogWarning($"[TikTokLiveClient] TikTok Live disconnected: {reason}");
            if (_showPopups && _hudManager != null)
            {
                _hudManager.ShowStatusPopup("TikTok Live disconnected!", false);
            }
            EventBus.Publish(new TikTokDisconnectedEvent(reason));
        }

        private void OnRawRoomInfoReceived(SocketIOResponse response)
        {
            JObject json = ParseResponse(response);
            if (json == null) return;

            string channel = NormalizeUniqueId(_tiktokUniqueId);
            string roomId = ReadStringWithFallback(json, "roomId", "room_id", "data.roomId");
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

        #endregion

        #region Editor Support

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_giftMappings != null && _giftMappings.Count > 0)
            {
                bool changed = false;
                string dir = "Assets/_Project/Textures/TikTokGifts";

                foreach (var item in _giftMappings)
                {
                    if (item.giftId > 0)
                    {
                        string idPath = $"{dir}/{item.giftId}.png";
                        Sprite idSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(idPath);
                        if (idSprite != null && item.giftIcon != idSprite)
                        {
                            item.giftIcon = idSprite;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }

            if (_giftPanelController == null)
            {
                _giftPanelController = FindFirstObjectByType<GiftInfoPanelController>();
            }

            FollowerGate.StrictFollowerOnly = _requireFollowToPlay;

            if (_giftPanelController != null && !Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall -= RefreshGiftPanelInEditor;
                UnityEditor.EditorApplication.delayCall += RefreshGiftPanelInEditor;
            }
        }

        private void RefreshGiftPanelInEditor()
        {
            if (this == null || _giftPanelController == null) return;
            _giftPanelController.BuildGiftDisplay();
        }

        [ContextMenu("Sort Gift Mappings By Team")]
        public void SortGiftMappingsByTeam()
        {
            if (_giftPanelController == null)
            {
                _giftPanelController = FindFirstObjectByType<GiftInfoPanelController>();
            }

            if (_giftPanelController != null)
            {
                _giftPanelController.SyncSortedOrderToClient();
            }
        }
#endif

        #endregion
    }
}
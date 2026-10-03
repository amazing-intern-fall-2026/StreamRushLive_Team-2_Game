using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
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
    /// Connects Unity with the TikTok Live backend over Socket.IO.
    /// Integrates 4 core live stream interactions: Follow, Chat, Like, Gift.
    /// </summary>
    public class TikTokLiveClient : MonoBehaviour
    {
        [Header("Backend Connection")]
        [Tooltip("TikTok Live backend server URL (default: http://localhost:9090).")]
        [SerializeField] private string _serverUrl = "http://localhost:9090";

        [Tooltip("TikTok username of the active live stream (without @).")]
        [SerializeField] private string _tiktokUniqueId = "";

        [Header("Host / Streamer Profile")]
        [Tooltip("Auto-sync host username and avatar as the initial Runner.")]
        [SerializeField] private bool _syncHostAsInitialRunner = true;
        [Tooltip("Display name fetched from host's TikTok profile.")]
        [SerializeField] private string _hostDisplayName = "";
        [Tooltip("Avatar Sprite downloaded directly from host's TikTok profile.")]
        [SerializeField] private Sprite _hostAvatarSprite;

        [Tooltip("Auto-connect to live backend on Start.")]
        [SerializeField] private bool _connectOnStart = true;

        [Tooltip("Enforce WebSocket transport only.")]
        [SerializeField] private bool _webSocketOnly = true;

        [Header("Follower Requirement")]
        [Tooltip("If TRUE: Viewers must follow on TikTok Live to play and send chat commands. If FALSE (default): Anyone in the live stream can play immediately.")]
        [UnityEngine.Serialization.FormerlySerializedAs("_strictFollowerOnly")]
        [SerializeField] private bool _requireFollowToPlay = false;

        public bool RequireFollowToPlay
        {
            get => _requireFollowToPlay;
            set
            {
                _requireFollowToPlay = value;
                FollowerGate.StrictFollowerOnly = value;
            }
        }

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

        [Header("TikTok Gift Mappings")]
        [Tooltip("List of TikTok gift mappings (linked by Gift ID or Name) to in-game actions.")]
        [SerializeField] private List<TikTokGiftMapping> _giftMappings = new List<TikTokGiftMapping>
        {
            new TikTokGiftMapping { giftId = 5655, giftName = "Rose", action = GiftActionType.Blue_EnergyBottle, customValue = 300, description = "+300 Blue Energy" },
            new TikTokGiftMapping { giftId = 5269, giftName = "TikTok", action = GiftActionType.Blue_SpeedBoost, customValue = 20, description = "Turbo Speed +50% (20s)" },
            new TikTokGiftMapping { giftId = 5487, giftName = "Finger Heart", action = GiftActionType.Blue_Shield, customValue = 15, description = "Invincible Shield (15s)" },
            new TikTokGiftMapping { giftId = 5585, giftName = "Dumbbell", action = GiftActionType.Red_SpawnPickup, customValue = 0, description = "Pickup Truck Hazard" },
            new TikTokGiftMapping { giftId = 5879, giftName = "Cap", action = GiftActionType.Blue_SpeedBoost, customValue = 30, description = "Sprint Speed Boost (30s)" },
            new TikTokGiftMapping { giftId = 5338, giftName = "Donut", action = GiftActionType.Blue_Shield, customValue = 20, description = "Protective Shield (20s)" },
            new TikTokGiftMapping { giftId = 6001, giftName = "Lion", action = GiftActionType.Red_UnlimitedCars, customValue = 60, description = "Unlimited Cars Rush (60s)" },
            new TikTokGiftMapping { giftId = 0, giftName = "Dance", action = GiftActionType.Special_GiftDance, customValue = 5, description = "Meme Victory Dance (5s)" },
            new TikTokGiftMapping { giftId = 0, giftName = "Rain", action = GiftActionType.Special_RainHazard, customValue = 60, description = "Slippery Rainy Track (60s)" },
            new TikTokGiftMapping { giftId = 0, giftName = "VIP", action = GiftActionType.Special_VIPRelayTicket, customValue = 0, description = "Priority Relay Runner" }
        };

        public List<TikTokGiftMapping> GiftMappings => _giftMappings;

        [Header("Likes to Energy")]
        [Tooltip("Likes threshold to award energy (default: 20 likes = +10 energy).")]
        [SerializeField] private int _likesPerEnergyStep = 20;
        [SerializeField] private int _energyPerStep = 10;

        [Header("Gameplay Subsystems")]
        [SerializeField] private FactionTugOfWarManager _factionManager;
        [SerializeField] private HUDManager _hudManager;
        [SerializeField] private ChatLaneRunnerController _runnerController;
        [SerializeField] private ChatRunnerQueueManager _queueManager;
        [SerializeField] private GiftManager _giftManager;
        [SerializeField] private GiftInfoPanelController _giftPanelController;

        [Header("Display & Logs")]
        [SerializeField] private bool _showPopups = true;
        [SerializeField] private bool _logEvents = true;

        [Serializable] public class FollowerJoinedEvent : UnityEvent<string, string> { }
        [SerializeField] private FollowerJoinedEvent _followerJoined = new FollowerJoinedEvent();
        public FollowerJoinedEvent FollowerJoined => _followerJoined;

        private readonly TikTokFollowerRegistry _followers = new TikTokFollowerRegistry();
        private readonly Dictionary<string, int> _processedLikeStepsByUser = new Dictionary<string, int>();
        private SocketIOUnity _socket;

        public TikTokFollowerRegistry Followers => _followers;
        public bool IsConnected => _socket != null && _socket.Connected;
        public string ServerUrl => _serverUrl;
        public string TikTokUniqueId => _tiktokUniqueId;
        public string HostDisplayName => _hostDisplayName;
        public Sprite HostAvatarSprite => _hostAvatarSprite;
        public bool SyncHostAsInitialRunner
        {
            get => _syncHostAsInitialRunner;
            set => _syncHostAsInitialRunner = value;
        }

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

        /// <summary>
        /// Adds a new gift mapping and updates the gift UI layout.
        /// </summary>
        public void AddGiftMapping(TikTokGiftMapping mapping)
        {
            if (mapping == null) return;
            _giftMappings.Add(mapping);
            if (_giftPanelController != null)
            {
                _giftPanelController.BuildGiftDisplay();
            }
        }

        /// <summary>
        /// Removes a gift mapping by ID or gift name and updates the gift UI.
        /// </summary>
        public bool RemoveGiftMapping(int giftId, string giftName = null)
        {
            int removed = _giftMappings.RemoveAll(m =>
                (giftId > 0 && m.giftId == giftId) ||
                (!string.IsNullOrEmpty(giftName) && string.Equals(m.giftName, giftName, StringComparison.OrdinalIgnoreCase)));

            if (removed > 0 && _giftPanelController != null)
            {
                _giftPanelController.BuildGiftDisplay();
                return true;
            }
            return false;
        }

        private void Awake()
        {
            EnsureReferences();
            FollowerGate.StrictFollowerOnly = _requireFollowToPlay;
        }

        private void Start()
        {
            EnsureReferences();
            FollowerGate.StrictFollowerOnly = _requireFollowToPlay;
            if (_syncHostAsInitialRunner)
            {
                SyncHostProfileToRunner();
            }
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

        public void EnsureReferences()
        {
            if (_factionManager == null) _factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            if (_hudManager == null) _hudManager = FindFirstObjectByType<HUDManager>();
            if (_runnerController == null) _runnerController = FindFirstObjectByType<ChatLaneRunnerController>();
            if (_queueManager == null) _queueManager = FindFirstObjectByType<ChatRunnerQueueManager>();
            if (_giftManager == null) _giftManager = GiftManager.Instance ?? FindFirstObjectByType<GiftManager>();
            if (_giftPanelController == null) _giftPanelController = FindFirstObjectByType<GiftInfoPanelController>();
        }

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

            _followers.Clear();
            _followers.Register();
            _processedLikeStepsByUser.Clear();

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
                };

                _socket.OnError += (sender, error) =>
                {
                    Debug.LogWarning($"[TikTokLiveClient] Socket error: {error}");
                };

                // Listen to socket events on the Unity main thread for thread-safe gameplay and UI updates
                _socket.OnUnityThread(_chatEvent, HandleChat);
                _socket.OnUnityThread(_followEvent, HandleFollow);
                _socket.OnUnityThread(_likeEvent, HandleLike);
                _socket.OnUnityThread(_giftEvent, HandleGift);
                _socket.OnUnityThread("share", HandleShare);
                _socket.OnUnityThread("tiktokConnected", HandleTikTokConnected);
                _socket.OnUnityThread("tiktokDisconnected", HandleTikTokDisconnected);
                _socket.OnUnityThread("roomInfo", HandleRoomInfo);
                _socket.OnUnityThread("connected", HandleRoomInfo);
                _socket.OnUnityThread("streamerInfo", HandleRoomInfo);

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

            _followers.Unregister();
        }

        #region Event Handlers

        /// <summary>
        /// Handles new follower events and adds them to the Runner relay queue.
        /// </summary>
        private void HandleFollow(SocketIOResponse response)
        {
            EnsureReferences();
            JObject json = ParseResponse(response);
            if (json == null) return;

            string userId = ReadStringWithFallback(json, _userIdPath, "userId", "uniqueId", "data.user.uniqueId");
            if (string.IsNullOrEmpty(userId)) return;

            string displayName = GetDisplayName(json, userId);
            bool isNew = _followers.AddFollower(userId);

            if (_logEvents)
            {
                Debug.Log($"[TikTokLiveClient] FOLLOW: {displayName} ({userId}) - {(isNew ? "New" : "Existing")}");
            }

            if (!isNew) return;

            if (_queueManager != null)
            {
                _queueManager.TryEnqueueFollower(displayName);
            }

            ShowPopup($"[{displayName}] Joined Queue!", true);
            _followerJoined.Invoke(userId, displayName);
            AudioManager.Instance?.PlaySFX(SFXType.StreamNewFollower);
        }

        /// <summary>
        /// Handles live chat commands:
        /// - Faction select: "blue", "red"
        /// - Runner controls (Blue team): "1", "2", "3", "jump", "fast"
        /// - Obstacle deployment (Red team): "1", "2", "3"
        /// </summary>
        private void HandleChat(SocketIOResponse response)
        {
            EnsureReferences();
            JObject json = ParseResponse(response);
            if (json == null) return;

            string comment = ReadStringWithFallback(json, _commentPath, "comment", "text");
            string userId = ReadStringWithFallback(json, _userIdPath, "userId", "uniqueId", "data.user.uniqueId");
            if (string.IsNullOrEmpty(comment) || string.IsNullOrEmpty(userId)) return;

            string displayName = GetDisplayName(json, userId);
            string trimmedCmd = comment.Trim().ToLowerInvariant();

            if (!_requireFollowToPlay)
            {
                _followers.AddFollower(userId);
            }
            else if (!_followers.IsFollower(userId))
            {
                if (_logEvents)
                {
                    Debug.Log($"[TikTokLiveClient] {displayName} ({userId}) is not following - ignored chat command: '{comment}'.");
                }
                _hudManager?.ShowStatusPopup($"[{displayName}] Follow to play!", false);
                return;
            }

            if (_logEvents)
            {
                Debug.Log($"[TikTokLiveClient] Chat: [{displayName}] '{comment}'");
            }

            if (trimmedCmd == "blue" || trimmedCmd == "red")
            {
                if (_factionManager != null)
                {
                    bool changed = _factionManager.OnChatCommand(userId, trimmedCmd);
                    if (changed)
                    {
                        bool isFan = _factionManager.GetFaction(userId) == FactionType.Fan;
                        string teamName = isFan ? "Blue Team (Runner Support)" : "Red Team (Obstacle Hazard)";
                        ShowPopup($"[{displayName}] joined {teamName}!", isFan);
                    }
                }
                return;
            }

            FactionType faction = _factionManager != null ? _factionManager.GetFaction(userId) : FactionType.Fan;

            if (faction == FactionType.Fan)
            {
                // Blue team (Fan): Runner controls
                if (trimmedCmd == "1" || trimmedCmd == "2" || trimmedCmd == "3" || 
                    trimmedCmd == "left" || trimmedCmd == "right")
                {
                    _runnerController?.ExecuteSingleCommand(trimmedCmd);
                    _hudManager?.ShowFanAction(displayName, $"Lane {trimmedCmd}");
                }
                else if (trimmedCmd == "jump" || trimmedCmd == "j")
                {
                    _runnerController?.ExecuteSingleCommand("jump");
                    _hudManager?.ShowFanAction(displayName, "Jump!");
                }
                else if (trimmedCmd == "fast" || trimmedCmd == "speed")
                {
                    _runnerController?.ExecuteSingleCommand("fast");
                    _hudManager?.ShowFanAction(displayName, "Turbo Boost!");
                }
            }
            else
            {
                // Red team (Anti): Spawn obstacles on lane 1, 2, or 3
                if (trimmedCmd == "1" || trimmedCmd == "2" || trimmedCmd == "3")
                {
                    if (int.TryParse(trimmedCmd, out int laneIndex))
                    {
                        bool spawned = _factionManager != null && _factionManager.TrySpawnAntiObstacleCar(userId, laneIndex);
                        if (spawned)
                        {
                            _hudManager?.ShowAntiAction(displayName, $"Car on Lane {laneIndex}!");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles like events and awards energy to the sender's faction.
        /// </summary>
        private void HandleLike(SocketIOResponse response)
        {
            EnsureReferences();
            JObject json = ParseResponse(response);
            if (json == null) return;

            string userId = ReadStringWithFallback(json, _userIdPath, "userId", "uniqueId", "data.user.uniqueId");
            string totalLikeRaw = ReadStringWithFallback(json, _totalLikePath, "totalLike", "totalLikeCount", "likeCount");
            if (string.IsNullOrEmpty(userId) || !int.TryParse(totalLikeRaw, out int totalLike)) return;

            int newSteps = _likesPerEnergyStep > 0 ? totalLike / _likesPerEnergyStep : 0;
            _processedLikeStepsByUser.TryGetValue(userId, out int oldSteps);

            if (newSteps <= oldSteps) return;

            _processedLikeStepsByUser[userId] = newSteps;
            int stepsGained = newSteps - oldSteps;
            int energyAmount = stepsGained * _energyPerStep;

            FactionType faction = _factionManager != null ? _factionManager.GetFaction(userId) : FactionType.Fan;
            string displayName = GetDisplayName(json, userId);

            if (_factionManager != null)
            {
                if (faction == FactionType.Fan)
                {
                    _factionManager.AddLikes(FactionType.Fan, energyAmount);
                }
                else
                {
                    _factionManager.AddLikes(FactionType.Anti, energyAmount);
                }
            }

            AudioManager.Instance?.PlaySFX(SFXType.StreamLike, 0.5f);

            if (_logEvents)
            {
                Debug.Log($"[TikTokLiveClient] LIKE: {displayName} ({userId}) reached {totalLike} likes (+{stepsGained} steps) -> +{energyAmount} energy for {faction}.");
            }
        }

        /// <summary>
        /// Handles gift events and triggers corresponding in-game gameplay actions.
        /// </summary>
        private void HandleGift(SocketIOResponse response)
        {
            EnsureReferences();
            JObject json = ParseResponse(response);
            if (json == null) return;

            string userId = ReadStringWithFallback(json, _userIdPath, "userId", "uniqueId", "data.user.uniqueId");
            string giftName = ReadStringWithFallback(json, _giftNamePath, "giftName", "data.giftName");
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(giftName)) return;

            string lowerName = giftName.ToLowerInvariant();
            if (!lowerName.Contains("dance") && !lowerName.Contains("nhảy") && !lowerName.Contains("vũ"))
            {
                AudioManager.Instance?.PlaySFX(SFXType.StreamDonateGift, 0.65f);
            }

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
            FactionType faction = _factionManager != null ? _factionManager.GetFaction(userId) : FactionType.Fan;

            int giftId = 0;
            string giftIdStr = ReadStringWithFallback(json, _giftIdPath, "giftId", "gift.id", "data.giftId", "gift_id");
            if (!string.IsNullOrEmpty(giftIdStr) && int.TryParse(giftIdStr, out int parsedGiftId))
            {
                giftId = parsedGiftId;
            }

            if (_logEvents)
            {
                Debug.Log($"[TikTokLiveClient] GIFT: [{displayName}] sent [{giftName}] (Gift ID: {giftId}) x{repeatCount} ({totalValue} coins) - Faction: {faction}");
            }

            string giftIconUrl = ReadStringWithFallback(json, "giftIconUrl", "data.giftIconUrl", "giftPictureUrl", "gift.icon.url_list[0]");
            if (!string.IsNullOrEmpty(giftIconUrl) && giftId > 0)
            {
                _giftPanelController?.UpdateGiftIconFromLive(giftId, giftName, giftIconUrl);
            }

            _giftPanelController?.HighlightGift(giftId, giftName);

            // Priority 1: Match configured gift mappings
            if (TryExecuteGiftMapping(giftId, giftName, displayName, repeatCount, totalValue, faction))
            {
                return;
            }

            // Fallback gift routing if not configured in mappings
            if (lowerName.Contains("dance") || lowerName.Contains("nhảy") || lowerName.Contains("vũ"))
            {
                _giftManager?.TriggerGiftDance(displayName);
                return;
            }

            if (lowerName.Contains("vip") || lowerName.Contains("ticket") || lowerName.Contains("vé"))
            {
                _queueManager?.TryEnqueuePriorityFollower(displayName);
                ShowPopup($"VIP: [{displayName}]", true);
                return;
            }

            if (lowerName.Contains("mưa") || lowerName.Contains("rain") || lowerName.Contains("dù") || lowerName.Contains("umbrella"))
            {
                _giftManager?.ActivateRainHazard(displayName, 60f);
                return;
            }

            if (totalValue >= 100 || lowerName.Contains("lion") || lowerName.Contains("sư tử") || 
                lowerName.Contains("tên lửa") || lowerName.Contains("rocket"))
            {
                if (faction == FactionType.Anti)
                {
                    _giftManager?.ActivateUnlimitedCars(displayName, 60f);
                }
                else
                {
                    _giftManager?.ActivateHeavyTruckPhase(displayName);
                }
                return;
            }

            if (faction == FactionType.Fan)
            {
                // Blue team (Fan) fallback actions
                if (lowerName.Contains("shield") || lowerName.Contains("khiên") || lowerName.Contains("donut") || totalValue >= 30)
                {
                    _giftManager?.ActivateShield(displayName, 15f);
                }
                else if (lowerName.Contains("sprint") || lowerName.Contains("speed") || lowerName.Contains("cap") || lowerName.Contains("mũ"))
                {
                    _giftManager?.ActivateSprintBuff(displayName, 30f);
                }
                else
                {
                    _giftManager?.AddBlueEnergy(displayName, 300);
                }
            }
            else
            {
                // Red team (Anti) fallback actions
                if (lowerName.Contains("heavy") || lowerName.Contains("tải") || totalValue >= 50)
                {
                    _giftManager?.SpawnAntiCar(VehicleTier.HeavyTruck, displayName);
                }
                else if (lowerName.Contains("pickup") || lowerName.Contains("bán tải") || totalValue >= 20)
                {
                    _giftManager?.SpawnAntiCar(VehicleTier.PickupTruck, displayName);
                }
                else if (lowerName.Contains("car") || lowerName.Contains("sedan") || lowerName.Contains("xe") || totalValue >= 10)
                {
                    _giftManager?.SpawnAntiCar(VehicleTier.SedanCar, displayName);
                }
                else
                {
                    _giftManager?.AddRedEnergy(displayName, 500);
                }
            }
        }

        /// <summary>
        /// Executes mapped gift action matching gift ID or gift name.
        /// </summary>
        private bool TryExecuteGiftMapping(int giftId, string giftName, string displayName, int repeatCount, int totalCoins, FactionType faction)
        {
            if (_giftMappings == null || _giftMappings.Count == 0) return false;

            TikTokGiftMapping matched = null;

            if (giftId > 0)
            {
                matched = _giftMappings.Find(m => m.giftId == giftId);
            }

            if (matched == null && !string.IsNullOrEmpty(giftName))
            {
                string lower = giftName.ToLowerInvariant();
                matched = _giftMappings.Find(m => !string.IsNullOrEmpty(m.giftName) && lower.Contains(m.giftName.ToLowerInvariant()));
            }

            if (matched == null) return false;

            float val = matched.customValue;
            switch (matched.action)
            {
                case GiftActionType.Blue_Shield:
                    _giftManager?.ActivateShield(displayName, val > 0 ? val : -1f);
                    break;
                case GiftActionType.Blue_SpeedBoost:
                    _giftManager?.ActivateSprintBuff(displayName, val > 0 ? val : -1f);
                    break;
                case GiftActionType.Blue_FreeControl:
                    _giftManager?.ActivateFreeControl(displayName, val > 0 ? val : -1f);
                    break;
                case GiftActionType.Blue_EnergyBottle:
                    _giftManager?.AddBlueEnergy(displayName, val > 0 ? Mathf.RoundToInt(val) : -1);
                    break;
                case GiftActionType.Red_SpawnPickup:
                    _giftManager?.SpawnAntiCar(VehicleTier.PickupTruck, displayName);
                    break;
                case GiftActionType.Red_SpawnHeavyTruck:
                    _giftManager?.SpawnAntiCar(VehicleTier.HeavyTruck, displayName);
                    break;
                case GiftActionType.Red_UnlimitedCars:
                    _giftManager?.ActivateUnlimitedCars(displayName, val > 0 ? val : 60f);
                    break;
                case GiftActionType.Red_EnergyBottle:
                    _giftManager?.AddRedEnergy(displayName, val > 0 ? Mathf.RoundToInt(val) : -1);
                    break;
                case GiftActionType.Special_GiftDance:
                    _giftManager?.TriggerGiftDance(displayName);
                    break;
                case GiftActionType.Special_RainHazard:
                    _giftManager?.ActivateRainHazard(displayName, val > 0 ? val : 60f);
                    break;
                case GiftActionType.Special_VIPRelayTicket:
                    _queueManager?.TryEnqueuePriorityFollower(displayName);
                    ShowPopup($"VIP: [{displayName}]", true);
                    break;
                case GiftActionType.Like_Energy:
                    int energy = val > 0 ? Mathf.RoundToInt(val) : 10;
                    if (faction == FactionType.Fan)
                        _factionManager?.AddLikes(FactionType.Fan, energy);
                    else
                        _factionManager?.AddLikes(FactionType.Anti, energy);
                    break;
                case GiftActionType.Dynamic_ByFaction:
                    return false; // Fallback to faction handling
            }

            if (_logEvents)
            {
                Debug.Log($"[TikTokLiveClient] Activated [{matched.action}] for gift [{giftName}] (ID: {giftId}) from {displayName}!");
            }
            return true;
        }

        #endregion

        #region Helpers

        private void ShowPopup(string message, bool isFan)
        {
            if (!_showPopups) return;
            EnsureReferences();
            _hudManager?.ShowStatusPopup(message, isFan);
        }

        private string GetDisplayName(JObject json, string fallbackUserId)
        {
            string nickname = ReadStringWithFallback(json, _nicknamePath, "nickname", "data.user.nickname");
            if (!string.IsNullOrEmpty(nickname)) return nickname;

            string uniqueId = ReadStringWithFallback(json, _uniqueIdPath, "uniqueId", "data.user.uniqueId");
            return string.IsNullOrEmpty(uniqueId) ? fallbackUserId : uniqueId;
        }

        private static JObject ParseResponse(SocketIOResponse response)
        {
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

        #region Host Profile & Avatar Sync

        [ContextMenu("Sync Host Profile Now")]
        public void SyncHostProfileToRunner()
        {
            string uniqueId = NormalizeUniqueId(_tiktokUniqueId);
            if (string.IsNullOrEmpty(uniqueId)) return;

            EnsureReferences();

            string initialName = !string.IsNullOrEmpty(_hostDisplayName) ? _hostDisplayName : uniqueId;
            if (_queueManager != null)
            {
                _queueManager.SetCurrentRunner(initialName, _hostAvatarSprite, false);
            }
            else if (_hudManager != null)
            {
                _hudManager.UpdateRunnerInfo(initialName, _hostAvatarSprite, false);
            }

            StartCoroutine(FetchTikTokHostProfileRoutine(uniqueId));
        }

        private IEnumerator FetchTikTokHostProfileRoutine(string uniqueId)
        {
            string profileUrl = $"https://www.tiktok.com/@{uniqueId}";
            using (UnityWebRequest webReq = UnityWebRequest.Get(profileUrl))
            {
                webReq.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                webReq.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
                webReq.timeout = 8;
                yield return webReq.SendWebRequest();

                if (webReq.result == UnityWebRequest.Result.Success)
                {
                    string html = webReq.downloadHandler.text;
                    string rawNick = ExtractRegexGroup(html, @"\""nickname\"":\""([^\""]+)\""");
                    string rawAvatar = ExtractRegexGroup(html, @"\""avatar(?:Larger|Medium|Thumb)\"":\""(https:[^\""]+)\""");

                    if (!string.IsNullOrEmpty(rawNick))
                    {
                        try
                        {
                            _hostDisplayName = Regex.Unescape(rawNick);
                        }
                        catch
                        {
                            _hostDisplayName = rawNick;
                        }
                    }
                    else
                    {
                        _hostDisplayName = uniqueId;
                    }

                    if (!string.IsNullOrEmpty(rawAvatar))
                    {
                        string avatarUrl = rawAvatar;
                        try
                        {
                            avatarUrl = Regex.Unescape(rawAvatar);
                        }
                        catch { }

                        yield return StartCoroutine(DownloadAvatarTextureRoutine(avatarUrl, sprite =>
                        {
                            _hostAvatarSprite = sprite;
                            ApplyHostProfileToRunner();
                        }));
                    }
                    else
                    {
                        ApplyHostProfileToRunner();
                    }
                }
                else
                {
                    if (_logEvents)
                    {
                        Debug.LogWarning($"[TikTokLiveClient] Failed to load TikTok web profile @{uniqueId}: {webReq.error}. Using unique ID as runner name.");
                    }
                    _hostDisplayName = uniqueId;
                    ApplyHostProfileToRunner();
                }
            }
        }

        private IEnumerator DownloadAvatarTextureRoutine(string avatarUrl, Action<Sprite> onLoaded)
        {
            using (UnityWebRequest imgReq = UnityWebRequestTexture.GetTexture(avatarUrl))
            {
                imgReq.timeout = 10;
                yield return imgReq.SendWebRequest();

                if (imgReq.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(imgReq);
                    if (tex != null)
                    {
                        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        onLoaded?.Invoke(sprite);
                    }
                }
                else if (_logEvents)
                {
                    Debug.LogWarning($"[TikTokLiveClient] Failed to download avatar from CDN: {imgReq.error}");
                }
            }
        }

        private void ApplyHostProfileToRunner()
        {
            string finalName = !string.IsNullOrEmpty(_hostDisplayName) ? _hostDisplayName : NormalizeUniqueId(_tiktokUniqueId);
            if (string.IsNullOrEmpty(finalName)) return;

            EnsureReferences();
            if (_queueManager != null)
            {
                _queueManager.SetCurrentRunner(finalName, _hostAvatarSprite, false);
            }
            else if (_hudManager != null)
            {
                _hudManager.UpdateRunnerInfo(finalName, _hostAvatarSprite, false);
            }

            if (_logEvents)
            {
                Debug.Log($"[TikTokLiveClient] Synced Runner from live channel: {finalName} (@{NormalizeUniqueId(_tiktokUniqueId)})");
            }
        }

        private void HandleTikTokConnected(SocketIOResponse response)
        {
            Debug.Log($"[TikTokLiveClient] Connected to TikTok Live successfully (Team 5 Backend)! Channel: @{NormalizeUniqueId(_tiktokUniqueId)}");
            ShowPopup($"TikTok Live: @{NormalizeUniqueId(_tiktokUniqueId)} connected!", true);
            HandleRoomInfo(response);
        }

        private void HandleTikTokDisconnected(SocketIOResponse response)
        {
            string reason = response != null ? response.ToString() : "Disconnected";
            Debug.LogWarning($"[TikTokLiveClient] TikTok Live disconnected: {reason}");
            ShowPopup("TikTok Live disconnected!", false);
        }

        private void HandleShare(SocketIOResponse response)
        {
            JObject json = ParseResponse(response);
            if (json == null) return;
            string userId = ReadStringWithFallback(json, _userIdPath, "userId", "uniqueId", "data.user.uniqueId");
            string displayName = GetDisplayName(json, userId);
            ShowPopup($"[{displayName}] shared the livestream!", true);
            if (_logEvents)
            {
                Debug.Log($"[TikTokLiveClient] SHARE: {displayName} ({userId}) shared the livestream.");
            }
        }

        private void HandleRoomInfo(SocketIOResponse response)
        {
            JObject json = ParseResponse(response);
            if (json == null) return;

            string nick = ReadStringWithFallback(json, 
                "roomInfo.owner.nickname", 
                "owner.nickname", 
                "data.owner.nickname", 
                "nickname", 
                "data.user.nickname");

            string avt = ReadStringWithFallback(json, 
                "roomInfo.owner.avatar_thumb.url_list[0]", 
                "roomInfo.owner.avatarThumb.urlList[0]", 
                "owner.avatar_thumb.url_list[0]", 
                "data.owner.avatar_thumb.url_list[0]", 
                "data.user.profilePictureUrl",
                "avatarUrl");

            if (!string.IsNullOrEmpty(nick))
            {
                _hostDisplayName = nick;
            }

            if (!string.IsNullOrEmpty(avt))
            {
                StartCoroutine(DownloadAvatarTextureRoutine(avt, sprite =>
                {
                    _hostAvatarSprite = sprite;
                    ApplyHostProfileToRunner();
                }));
            }
            else if (!string.IsNullOrEmpty(nick))
            {
                ApplyHostProfileToRunner();
            }
        }

        private static string ExtractRegexGroup(string text, string pattern)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern)) return string.Empty;
            Match m = Regex.Match(text, pattern);
            return m.Success && m.Groups.Count > 1 ? m.Groups[1].Value : string.Empty;
        }

        #endregion
    }
}
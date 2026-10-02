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
        Dynamic_ByFaction
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

        [Tooltip("Short description displayed on the in-game gift card.")]
        public string englishDescription = "+300 Blue Energy";

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
            new TikTokGiftMapping { giftId = 5655, giftName = "Rose", action = GiftActionType.Blue_EnergyBottle, customValue = 300, englishDescription = "+300 Blue Energy" },
            new TikTokGiftMapping { giftId = 5269, giftName = "TikTok", action = GiftActionType.Blue_SpeedBoost, customValue = 20, englishDescription = "Turbo Speed +50% (20s)" },
            new TikTokGiftMapping { giftId = 5487, giftName = "Finger Heart", action = GiftActionType.Blue_Shield, customValue = 15, englishDescription = "Invincible Shield (15s)" },
            new TikTokGiftMapping { giftId = 5585, giftName = "Dumbbell", action = GiftActionType.Red_SpawnPickup, customValue = 0, englishDescription = "Pickup Truck Hazard" },
            new TikTokGiftMapping { giftId = 5879, giftName = "Cap", action = GiftActionType.Blue_SpeedBoost, customValue = 30, englishDescription = "Sprint Speed Boost (30s)" },
            new TikTokGiftMapping { giftId = 5338, giftName = "Donut", action = GiftActionType.Blue_Shield, customValue = 20, englishDescription = "Protective Shield (20s)" },
            new TikTokGiftMapping { giftId = 6001, giftName = "Lion", action = GiftActionType.Red_UnlimitedCars, customValue = 60, englishDescription = "Unlimited Cars Rush (60s)" },
            new TikTokGiftMapping { giftId = 0, giftName = "Dance", action = GiftActionType.Special_GiftDance, customValue = 5, englishDescription = "Meme Victory Dance (5s)" },
            new TikTokGiftMapping { giftId = 0, giftName = "Rain", action = GiftActionType.Special_RainHazard, customValue = 60, englishDescription = "Slippery Rainy Track (60s)" },
            new TikTokGiftMapping { giftId = 0, giftName = "VIP", action = GiftActionType.Special_VIPRelayTicket, customValue = 0, englishDescription = "Priority Relay Runner" }
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

        [ContextMenu("Sắp Xếp Quà Theo Phe (Blue -> Red -> Special)")]
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
        /// Thêm 1 món quà mới vào cấu hình và tự động cập nhật sắp xếp trên UI.
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
        /// Xóa 1 món quà khỏi cấu hình và tự động cập nhật sắp xếp trên UI.
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
                Debug.LogWarning("[TikTokLiveClient] Chưa nhập TikTok Unique ID (username kênh đang Live) - bỏ qua kết nối tự động.");
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
                    Debug.Log($"[TikTokLiveClient] Đã kết nối Socket tới {_serverUrl}. Gửi {_setUniqueIdEvent} = '{uniqueId}'");
                    _socket.Emit(_setUniqueIdEvent, uniqueId);
                };

                _socket.OnDisconnected += (sender, reason) =>
                {
                    Debug.Log($"[TikTokLiveClient] Mất kết nối backend: {reason}");
                };

                _socket.OnError += (sender, error) =>
                {
                    Debug.LogWarning($"[TikTokLiveClient] Lỗi socket: {error}");
                };

                // Lắng nghe các event trên Unity Main Thread để gọi an toàn API gameplay & UI
                _socket.OnUnityThread(_chatEvent, HandleChat);
                _socket.OnUnityThread(_followEvent, HandleFollow);
                _socket.OnUnityThread(_likeEvent, HandleLike);
                _socket.OnUnityThread(_giftEvent, HandleGift);
                _socket.OnUnityThread("roomInfo", HandleRoomInfo);
                _socket.OnUnityThread("connected", HandleRoomInfo);
                _socket.OnUnityThread("streamerInfo", HandleRoomInfo);

                _socket.Connect();
                Debug.Log($"[TikTokLiveClient] Đang kết nối tới {_serverUrl}...");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TikTokLiveClient] Không thể kết nối: {ex.Message}");
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
                    Debug.LogWarning($"[TikTokLiveClient] Lỗi khi ngắt kết nối: {ex.Message}");
                }
                _socket = null;
            }

            _followers.Unregister();
        }

        #region Event Handlers

        /// <summary>
        /// Xử lý sự kiện Follower mới -> Thêm vào hàng đợi tiếp sức Runner.
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
                Debug.Log($"[TikTokLiveClient] FOLLOW: {displayName} ({userId}) - {(isNew ? "Mới" : "Đã có")}");
            }

            if (!isNew) return;

            // Đưa người theo dõi mới vào hàng đợi chạy Runner tiếp sức
            if (_queueManager != null)
            {
                _queueManager.TryEnqueueFollower(displayName);
            }

            ShowPopup($"[{displayName}] Joined Queue!", true);
            _followerJoined.Invoke(userId, displayName);
            AudioManager.Instance?.PlaySFX(SFXType.StreamNewFollower);
        }

        /// <summary>
        /// Xử lý sự kiện Chat/Comment:
        /// - Chọn phe: "blue", "red"
        /// - Điều khiển Runner (phe Blue): "1", "2", "3", "jump", "fast"
        /// - Thả xe cản đường (phe Red): "1", "2", "3"
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
                Debug.Log($"<color=#FFAA00>[TikTokLiveClient] {displayName} ({userId}) chưa Follow kênh -> Bỏ qua lệnh chat: '{comment}'. Bật Follow để chơi!</color>");
                _hudManager?.ShowStatusPopup($"[{displayName}] Follow để chơi!", false);
                return;
            }

            Debug.Log($"[TikTokLiveClient] Nhận Chat: [{displayName}] '{comment}'");

            // 1. Kiểm tra Lệnh Chọn Phe: chỉ chấp nhận "blue" hoặc "red"
            if (trimmedCmd == "blue" || trimmedCmd == "red")
            {
                if (_factionManager != null)
                {
                    bool changed = _factionManager.OnChatCommand(userId, trimmedCmd);
                    if (changed)
                    {
                        bool isFan = _factionManager.GetFaction(userId) == FactionType.Fan;
                        string teamName = isFan ? "Blue Team (Ủng hộ Runner)" : "Red Team (Cản đường Runner)";
                        ShowPopup($"[{displayName}] đã gia nhập {teamName}!", isFan);
                    }
                }
                return;
            }

            // 2. Kiểm tra Phe Hiện Tại của Người Xem để điều hướng hành động tương ứng
            FactionType faction = _factionManager != null ? _factionManager.GetFaction(userId) : FactionType.Fan;

            if (faction == FactionType.Fan)
            {
                // ===== PHE BLUE (FAN): ĐIỀU KHIỂN RUNNER =====
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
                // ===== PHE RED (ANTI): THẢ XE CẢN ĐƯỜNG TRÊN LÀN (1, 2, 3) =====
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
        /// Xử lý sự kiện Like -> Tích lũy tim và nạp năng lượng tương ứng cho phe của người like.
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
                Debug.Log($"[TikTokLiveClient] LIKE: {displayName} ({userId}) like mốc {totalLike} (+{stepsGained} bước) -> +{energyAmount} năng lượng {faction}.");
            }
        }

        /// <summary>
        /// Xử lý sự kiện Gift -> Ánh xạ quà tặng vào các cơ chế gameplay thực tế.
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

            // Đọc giá trị xu hoặc số kim cương của quà
            int coins = 1;
            string coinStr = ReadString(json, _diamondCountPath);
            if (string.IsNullOrEmpty(coinStr)) coinStr = ReadString(json, _coinCountPath);
            if (string.IsNullOrEmpty(coinStr)) coinStr = ReadString(json, "cointCount"); // fallback typo backend
            if (!string.IsNullOrEmpty(coinStr) && int.TryParse(coinStr, out int parsedCoins))
            {
                coins = parsedCoins;
            }

            // Đọc số lượng quà dồn (repeatCount)
            int repeatCount = 1;
            string repeatStr = ReadString(json, _repeatCountPath);
            if (!string.IsNullOrEmpty(repeatStr) && int.TryParse(repeatStr, out int parsedRepeat))
            {
                repeatCount = Mathf.Max(1, parsedRepeat);
            }

            int totalValue = coins * repeatCount;
            FactionType faction = _factionManager != null ? _factionManager.GetFaction(userId) : FactionType.Fan;

            // Đọc Gift ID từ JSON
            int giftId = 0;
            string giftIdStr = ReadStringWithFallback(json, _giftIdPath, "giftId", "gift.id", "data.giftId", "gift_id");
            if (!string.IsNullOrEmpty(giftIdStr) && int.TryParse(giftIdStr, out int parsedGiftId))
            {
                giftId = parsedGiftId;
            }

            // In log VÀNG CỰC KỲ NỔI BẬT để bạn thấy ngay Gift ID của món quà trên Console Unity:
            Debug.Log($"<color=#FFD700><b>[TikTokLiveClient] 🎁 QUÀ TẶNG:</b> [{displayName}] tặng <b>[{giftName}]</b> (Gift ID: <b>{giftId}</b>) x{repeatCount} ({totalValue} xu) - Phe {faction}</color>");

            // Đọc Icon URL của quà trực tiếp từ TikTok (nếu backend có gửi) và tự động cập nhật ảnh theo ID
            string giftIconUrl = ReadStringWithFallback(json, "giftIconUrl", "data.giftIconUrl", "giftPictureUrl", "gift.icon.url_list[0]");
            if (!string.IsNullOrEmpty(giftIconUrl) && giftId > 0)
            {
                _giftPanelController?.UpdateGiftIconFromLive(giftId, giftName, giftIconUrl);
            }

            // Kích hoạt hiệu ứng phát sáng / nảy thẻ quà trên UI GiftInfoPanel
            _giftPanelController?.HighlightGift(giftId, giftName);

            // 1. ƯU TIÊN SỐ 1: Kiểm tra Bảng Gift Mappings theo ID hoặc Tên quà
            if (TryExecuteGiftMapping(giftId, giftName, displayName, repeatCount, totalValue, faction))
            {
                return;
            }

            // ===== PHÂN LOẠI DỰ PHÒNG (FALLBACK) NẾU CHƯA CÓ TRONG BẢNG MAPPINGS =====

            // 1. Quà Nhảy Meme Ăn Mừng (Gift Dance)
            if (lowerName.Contains("dance") || lowerName.Contains("nhảy") || lowerName.Contains("vũ"))
            {
                _giftManager?.TriggerGiftDance(displayName);
                return;
            }

            // 2. Quà Vé VIP Chuyển Gậy Hàng Đợi (VIP Queue Ticket)
            if (lowerName.Contains("vip") || lowerName.Contains("ticket") || lowerName.Contains("vé"))
            {
                _queueManager?.TryEnqueuePriorityFollower(displayName);
                ShowPopup($"VIP: [{displayName}]", true);
                return;
            }

            // 3. Quà Thời Tiết Mưa (Gift Environment Rain)
            if (lowerName.Contains("mưa") || lowerName.Contains("rain") || lowerName.Contains("dù") || lowerName.Contains("umbrella"))
            {
                _giftManager?.ActivateRainHazard(displayName, 60f);
                return;
            }

            // 4. Quà Đặc Quyền Lớn (> 100 xu): Bão Xe Không Giới Hạn hoặc Bão Xe Tải Nặng
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

            // 4. Quà Tăng Tốc / Khiên / Năng Lượng / Xe Phân Cấp theo phe
            if (faction == FactionType.Fan)
            {
                // --- PHE FAN / BLUE TEAM ---
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
                    // Quà tặng thông thường (Hoa Hồng, Quả tạ, Cà phê...) -> Bình Năng Lượng Xanh (+300)
                    _giftManager?.AddBlueEnergy(displayName, 300);
                }
            }
            else
            {
                // --- PHE ANTI / RED TEAM ---
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
                    // Quà hỗ trợ nạp Năng Lượng Đỏ (+500)
                    _giftManager?.AddRedEnergy(displayName, 500);
                }
            }
        }

        /// <summary>
        /// Tìm kiếm và thực thi hành động từ bảng Gift Mappings theo Gift ID hoặc Tên quà.
        /// </summary>
        private bool TryExecuteGiftMapping(int giftId, string giftName, string displayName, int repeatCount, int totalCoins, FactionType faction)
        {
            if (_giftMappings == null || _giftMappings.Count == 0) return false;

            TikTokGiftMapping matched = null;

            // 1. Ưu tiên so khớp chính xác theo Gift ID (nếu giftId > 0)
            if (giftId > 0)
            {
                matched = _giftMappings.Find(m => m.giftId == giftId);
            }

            // 2. Nếu không có theo ID, so khớp theo Tên quà (không phân biệt hoa thường)
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
                case GiftActionType.Dynamic_ByFaction:
                    return false; // Để fallback xử lý theo phe
            }

            Debug.Log($"<color=#00FFCC>[TikTokLiveClient] Đã kích hoạt [{matched.action}] cho quà [{giftName}] (ID: {giftId}) từ {displayName}!</color>");
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

            // 1. Áp dụng ngay Unique ID làm tên runner khởi đầu trước
            string initialName = !string.IsNullOrEmpty(_hostDisplayName) ? _hostDisplayName : uniqueId;
            if (_queueManager != null)
            {
                _queueManager.SetCurrentRunner(initialName, _hostAvatarSprite, false);
            }
            else if (_hudManager != null)
            {
                _hudManager.UpdateRunnerInfo(initialName, _hostAvatarSprite, false);
            }

            // 2. Chạy coroutine tải thông tin chi tiết (Nickname hiển thị thật + Avatar HD) từ TikTok
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
                        Debug.LogWarning($"[TikTokLiveClient] Không thể tải web profile TikTok @{uniqueId}: {webReq.error}. Dùng Unique ID làm tên Runner.");
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
                    Debug.LogWarning($"[TikTokLiveClient] Tải avatar từ CDN thất bại: {imgReq.error}");
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
                Debug.Log($"<color=#00FF88>[TikTokLiveClient] >>> ĐÃ ĐỒNG BỘ RUNNER TỪ KÊNH LIVE: <b>{finalName}</b> (@{NormalizeUniqueId(_tiktokUniqueId)}) <<<</color>");
            }
        }

        private void HandleRoomInfo(SocketIOResponse response)
        {
            JObject json = ParseResponse(response);
            if (json == null) return;

            string nick = ReadString(json, "owner.nickname");
            if (string.IsNullOrEmpty(nick)) nick = ReadString(json, "data.owner.nickname");
            if (string.IsNullOrEmpty(nick)) nick = ReadString(json, "nickname");

            string avt = ReadString(json, "owner.avatar_thumb.url_list[0]");
            if (string.IsNullOrEmpty(avt)) avt = ReadString(json, "data.owner.avatar_thumb.url_list[0]");
            if (string.IsNullOrEmpty(avt)) avt = ReadString(json, "avatarUrl");

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
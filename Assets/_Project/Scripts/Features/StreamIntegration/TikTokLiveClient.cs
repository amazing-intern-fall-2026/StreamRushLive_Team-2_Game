using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;
using StreamRushLive.Features.Gifts;
using StreamRushLive.Features.Spawning;
using SteamRush.Features.Runner;
using SteamRush.Features.UI;

namespace SteamRush.Features.StreamIntegration
{
    /// <summary>
    /// Kết nối trực tiếp Unity với Backend TikTok Live qua Socket.IO.
    /// Tích hợp đầy đủ 4 luồng sự kiện tương tác cốt lõi vào Gameplay:
    /// 1. Follow: Ghi danh người theo dõi vào Hàng đợi Tiếp sức Runner (Relay Queue).
    /// 2. Chat / Comment: Chọn phe (Blue/Red), điều khiển Runner (Đổi làn 1/2/3, Nhảy, Fast) hoặc thả xe cản đường phe Anti.
    /// 3. Like: Tích lũy tim nạp năng lượng tương ứng cho phe Fan hoặc Anti (đầy 1000 tim tự động thả xe).
    /// 4. Gift: Tự động kích hoạt các hiệu ứng đặc quyền (Khiên bảo vệ, Bình năng lượng, Tăng tốc Turbo, Thả xe cản đường, Bão xe, Nhảy meme, Vé VIP).
    /// </summary>
    public class TikTokLiveClient : MonoBehaviour
    {
        [Header("Kết Nối Backend")]
        [Tooltip("Địa chỉ backend TikTok Live. Mặc định cổng 9090 chạy local.")]
        [SerializeField] private string _serverUrl = "http://localhost:9090";

        [Tooltip("Username TikTok của kênh ĐANG LIVE (không cần dấu @).")]
        [SerializeField] private string _tiktokUniqueId = "";

        [Tooltip("Tự động kết nối khi vào màn chơi.")]
        [SerializeField] private bool _connectOnStart = true;

        [Tooltip("Chỉ sử dụng giao thức WebSocket.")]
        [SerializeField] private bool _webSocketOnly = true;

        [Header("Tên Sự Kiện Socket (Khớp Backend)")]
        [SerializeField] private string _setUniqueIdEvent = "setUniqueID";
        [SerializeField] private string _chatEvent = "chat";
        [SerializeField] private string _followEvent = "follow";
        [SerializeField] private string _likeEvent = "like";
        [SerializeField] private string _giftEvent = "gift";

        [Header("Đường Dẫn Thuộc Tính JSON")]
        [SerializeField] private string _userIdPath = "data.user.userId";
        [SerializeField] private string _nicknamePath = "data.user.nickname";
        [SerializeField] private string _uniqueIdPath = "data.user.uniqueId";
        [SerializeField] private string _commentPath = "comment";
        [SerializeField] private string _totalLikePath = "totalLike";
        [SerializeField] private string _giftNamePath = "giftName";
        [SerializeField] private string _diamondCountPath = "diamondCount";
        [SerializeField] private string _coinCountPath = "coinCount";
        [SerializeField] private string _repeatCountPath = "repeatCount";

        [Header("Quy Đổi Like -> Năng Lượng")]
        [Tooltip("Số lượt like đạt mốc để cộng 1 lần năng lượng (mặc định: 20 like = +10 năng lượng ~ 1%).")]
        [SerializeField] private int _likesPerEnergyStep = 20;
        [SerializeField] private int _energyPerStep = 10;

        [Header("Tham Chiếu Gameplay Subsystems")]
        [SerializeField] private FactionTugOfWarManager _factionManager;
        [SerializeField] private HUDManager _hudManager;
        [SerializeField] private ChatLaneRunnerController _runnerController;
        [SerializeField] private ChatRunnerQueueManager _queueManager;
        [SerializeField] private GiftManager _giftManager;

        [Header("Hiển Thị & Log")]
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

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
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

            string userId = ReadString(json, _userIdPath);
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

            string comment = ReadString(json, _commentPath);
            string userId = ReadString(json, _userIdPath);
            if (string.IsNullOrEmpty(comment) || string.IsNullOrEmpty(userId)) return;

            string displayName = GetDisplayName(json, userId);
            string trimmedCmd = comment.Trim().ToLowerInvariant();

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

            string userId = ReadString(json, _userIdPath);
            string totalLikeRaw = ReadString(json, _totalLikePath);
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

            string userId = ReadString(json, _userIdPath);
            string giftName = ReadString(json, _giftNamePath);
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

            if (_logEvents)
            {
                Debug.Log($"[TikTokLiveClient] GIFT: {displayName} tặng {giftName} x{repeatCount} ({totalValue} xu) - Phe {faction}.");
            }

            // ===== PHÂN LOẠI & KÍCH HOẠT CƠ CHẾ GAME THEO LOẠI QUÀ =====

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
            string nickname = ReadString(json, _nicknamePath);
            if (!string.IsNullOrEmpty(nickname)) return nickname;

            string uniqueId = ReadString(json, _uniqueIdPath);
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

        private static string NormalizeUniqueId(string raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().TrimStart('@');
        }

        #endregion
    }
}
using System;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;

namespace SteamRush.Features.StreamIntegration
{
    // Ket noi Unity voi backend TikTok Live (Nhom 5) qua socket.io. Giai doan thu nghiem: CHI xu ly
    // event "follow" va "chat". Chua xu ly like/gift.
    //
    // Luong backend (theo code Nhom 5, backend/src/sockets/index.ts):
    //   1. Client ket noi socket.io toi backend (mac dinh port 9090).
    //   2. Client emit "setUniqueID" kem username TikTok dang Live -> backend moi noi vao TikTok.
    //   3. Backend emit "chat"   : data.user.{userId, uniqueId, nickname}, comment (NGOAI data).
    //      Backend emit "follow" : data.user.{userId, uniqueId, nickname}.
    //
    // Follow -> TikTokFollowerRegistry (luu RAM, khoa = userId) -> FollowerGate cho phep vao phe.
    // Chat "red"/"blue" -> FactionTugOfWarManager.OnChatCommand (da qua FollowerGate).
    // Tat _connectOnStart hoac disable component nay de quay lai test bang MockChatConsole.
    public class TikTokLiveClient : MonoBehaviour
    {
        [Header("Ket noi backend")]
        [Tooltip("Dia chi backend Nhom 5 (chay local). Mac dinh PORT=9090 trong backend/.env.")]
        [SerializeField] private string _serverUrl = "http://localhost:9090"; 
        [Tooltip("Username TikTok cua kenh DANG LIVE (khong can dau @).")]
        [SerializeField] private string _tiktokUniqueId = "";
        [Tooltip("Tu ket noi khi component duoc bat. Tat de test offline bang MockChatConsole.")]
        [SerializeField] private bool _connectOnStart = true;
        [Tooltip("Chi dung WebSocket (khuyen nghi). Bo chon neu server khong nhan WebSocket.")]
        [SerializeField] private bool _webSocketOnly = true;

        [Header("Ten event (khop backend Nhom 5)")]
        [SerializeField] private string _setUniqueIdEvent = "setUniqueID";
        [SerializeField] private string _chatEvent = "chat";
        [SerializeField] private string _followEvent = "follow";

        [Header("Duong dan JSON (khop backend Nhom 5)")]
        [SerializeField] private string _userIdPath = "data.user.userId";
        [SerializeField] private string _nicknamePath = "data.user.nickname";
        [SerializeField] private string _uniqueIdPath = "data.user.uniqueId";
        [SerializeField] private string _commentPath = "comment";

        [Header("Tham chieu (de trong = tu tim trong scene)")]
        [SerializeField] private FactionTugOfWarManager _factionManager;
        [SerializeField] private SteamRush.Features.UI.HUDManager _hudManager;

        [Header("Hien thi / Debug")]
        [SerializeField] private bool _showPopups = true;
        [SerializeField] private bool _logEvents = true;

        [Serializable] public class FollowerJoinedEvent : UnityEvent<string, string> { }
        [Tooltip("Ban khi co follower MOI (userId, ten hien thi). Dung de noi them hanh dong neu can.")]
        [SerializeField] private FollowerJoinedEvent _followerJoined = new FollowerJoinedEvent();
        public FollowerJoinedEvent FollowerJoined => _followerJoined;

        private readonly TikTokFollowerRegistry _followers = new TikTokFollowerRegistry();
        private SocketIOUnity _socket;

        public TikTokFollowerRegistry Followers => _followers;
        public bool IsConnected => _socket != null && _socket.Connected;

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

        [ContextMenu("Connect")]
        public void Connect()
        {
            if (_socket != null)
            {
                return;
            }

            string uniqueId = NormalizeUniqueId(_tiktokUniqueId);
            if (string.IsNullOrEmpty(uniqueId))
            {
                Debug.LogWarning("[TikTokLiveClient] Chưa nhập Tiktok Unique Id (username kênh đang Live) - không kết nối.");
                return;
            }

            // Phien live moi: bat dau danh sach follower tu dau, roi dang ky lam nguon follower chung.
            _followers.Clear();
            _followers.Register();

            try
            {
                var options = new SocketIOOptions();
                if (_webSocketOnly)
                {
                    options.Transport = SocketIOClient.Transport.TransportProtocol.WebSocket;
                }

                _socket = new SocketIOUnity(new Uri(_serverUrl), options);
                _socket.JsonSerializer = new NewtonsoftJsonSerializer();

                // Cac callback nay chay o thread nen: chi log/emit, KHONG dung API Unity khac o day.
                _socket.OnConnected += (sender, e) =>
                {
                    Debug.Log($"[TikTokLiveClient] Đã kết nối backend {_serverUrl}. Gửi {_setUniqueIdEvent} = {uniqueId}");
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

                // OnUnityThread: handler chay o main thread (Update) nen goi duoc API Unity/UI.
                _socket.OnUnityThread(_chatEvent, HandleChat);
                _socket.OnUnityThread(_followEvent, HandleFollow);

                _socket.Connect();
                Debug.Log($"[TikTokLiveClient] Đang kết nối tới {_serverUrl} ...");
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

            // Go dang ky de FollowerGate quay ve che do mock (cho qua) khi khong con ket noi that.
            _followers.Unregister();
        }

        private void HandleFollow(SocketIOResponse response)
        {
            JObject json = ParseResponse(response);
            if (json == null)
            {
                return;
            }

            string userId = ReadString(json, _userIdPath);
            if (string.IsNullOrEmpty(userId))
            {
                Debug.LogWarning($"[TikTokLiveClient] Event follow thiếu userId (path '{_userIdPath}'). Raw: {json}");
                return;
            }

            string displayName = GetDisplayName(json, userId);

            // Phai them vao registry TRUOC khi lam bat ky viec gi khac can quyen Follow.
            bool isNew = _followers.AddFollower(userId);

            if (_logEvents)
            {
                Debug.Log($"[TikTokLiveClient] FOLLOW: {displayName} ({userId}) - {(isNew ? "mới" : "đã có")}");
            }

            if (!isNew)
            {
                return;
            }

            ShowPopup($"[{displayName}] vừa Follow kênh! Gõ red hoặc blue để chọn phe.", true);
            _followerJoined.Invoke(userId, displayName);
        }

        private void HandleChat(SocketIOResponse response)
        {
            JObject json = ParseResponse(response);
            if (json == null)
            {
                return;
            }

            string comment = ReadString(json, _commentPath);
            string userId = ReadString(json, _userIdPath);
            if (string.IsNullOrEmpty(comment) || string.IsNullOrEmpty(userId))
            {
                return;
            }

            if (_factionManager == null)
            {
                _factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            }

            if (_factionManager == null)
            {
                Debug.LogWarning("[TikTokLiveClient] Không tìm thấy FactionTugOfWarManager trong scene.");
                return;
            }

            string displayName = GetDisplayName(json, userId);

            // OnChatCommand tu kiem tra Follow (FollowerGate) va tra true CHI KHI thanh vien thay doi.
            bool changed = _factionManager.OnChatCommand(userId, comment);

            if (_logEvents)
            {
                Debug.Log($"[TikTokLiveClient] CHAT: {displayName} ({userId}): \"{comment}\" -> {(changed ? "đổi/vào phe" : "bỏ qua")}");
            }

            if (!changed)
            {
                return;
            }

            if (_factionManager.GetFaction(userId) == FactionType.Fan)
            {
                ShowPopup($"[{displayName}] đã gia nhập phe FAN (Blue)! (Ủng hộ Runner)", true);
            }
            else
            {
                ShowPopup($"[{displayName}] đã gia nhập phe ANTI (Red)! (Cản đường Runner)", false);
            }
        }

        private void ShowPopup(string message, bool isFan)
        {
            if (!_showPopups)
            {
                return;
            }

            if (_hudManager == null)
            {
                _hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            }

            _hudManager?.ShowStatusPopup(message, isFan);
        }

        private string GetDisplayName(JObject json, string fallbackUserId)
        {
            string nickname = ReadString(json, _nicknamePath);
            if (!string.IsNullOrEmpty(nickname))
            {
                return nickname;
            }

            string uniqueId = ReadString(json, _uniqueIdPath);
            return string.IsNullOrEmpty(uniqueId) ? fallbackUserId : uniqueId;
        }

        // Backend emit 1 object; thu doc truc tiep, that bai thi doc qua chuoi JSON dang mang [ {...} ].
        private static JObject ParseResponse(SocketIOResponse response)
        {
            try
            {
                return response.GetValue<JObject>();
            }
            catch (Exception)
            {
                try
                {
                    JArray array = JArray.Parse(response.ToString());
                    return array.Count > 0 ? array[0] as JObject : null;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TikTokLiveClient] Không đọc được JSON từ backend: {ex.Message}");
                    return null;
                }
            }
        }

        // userId co the la so hoac chuoi (backend lay user.id || user.idStr) -> luon doc thanh chuoi.
        private static string ReadString(JObject json, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            JToken token = json.SelectToken(path);
            if (token == null || token.Type == JTokenType.Null)
            {
                return string.Empty;
            }

            return token.ToString();
        }

        private static string NormalizeUniqueId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return raw.Trim().TrimStart('@');
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SteamRush.Core;

namespace SteamRush.Features.StreamIntegration
{
    public enum FactionType
    {
        Fan,
        Anti
    }

    /// <summary>
    /// Manages the tug-of-war balance between Fan and Anti factions, member memberships, and ability costs.
    /// </summary>
    public class FactionTugOfWarManager : MonoBehaviour
    {
        [Tooltip("Ngưỡng năng lượng để phe Anti tự động sinh xe cản đường (Full thanh = AntiMaxValue). Mặc định = 1000.")]
        [SerializeField] private int _antiCarThreshold = 1000;
        [Tooltip("Chi phí năng lượng trừ khi phe Anti tự động sinh xe cản đường (bằng chi phí player spawn xe = 100).")]
        [SerializeField] private int _antiCarCost = 100;
        [Tooltip("Chi phí năng lượng phe Anti để thả xe cản đường theo làn chỉ định (1, 2, 3). Mặc định = 100.")]
        [SerializeField] private int _antiCarLaneCost = 100;
        [Tooltip("Chi phí năng lượng phe Fan để thả vật phẩm hỗ trợ (khiên/buff) theo làn chỉ định (1, 2, 3). Mặc định = 50.")]
        [SerializeField] private int _fanItemLaneCost = 50;
        [SerializeField] private FactionType _defaultFaction = FactionType.Fan;

        [Serializable] public class FactionValuesChangedEvent : UnityEvent<int, int> { }
        [SerializeField] private FactionValuesChangedEvent _factionValuesChanged = new FactionValuesChangedEvent();
        public FactionValuesChangedEvent FactionValuesChanged => _factionValuesChanged;

        [Serializable] public class FactionMemberCountsChangedEvent : UnityEvent<int, int> { }
        [SerializeField] private FactionMemberCountsChangedEvent _factionMemberCountsChanged = new FactionMemberCountsChangedEvent();
        public FactionMemberCountsChangedEvent FactionMemberCountsChanged => _factionMemberCountsChanged;

        private readonly FollowerGate _followerGate = new FollowerGate();

        private readonly Dictionary<string, FactionType> _userFactions = new Dictionary<string, FactionType>();

        public int FanMemberCount
        {
            get
            {
                int count = 0;
                foreach (var f in _userFactions.Values)
                {
                    if (f == FactionType.Fan) count++;
                }
                return count;
            }
        }

        public int AntiMemberCount
        {
            get
            {
                int count = 0;
                foreach (var f in _userFactions.Values)
                {
                    if (f == FactionType.Anti) count++;
                }
                return count;
            }
        }

        [SerializeField] private int _initialFanLikes = 100;
        [SerializeField] private int _initialAntiLikes = 200;

        private int _fanLikes;
        private int _antiLikes;

        public int FanLikes => _fanLikes;
        public int AntiLikes => _antiLikes;
        public int AntiCarThreshold => _antiCarThreshold;
        public int AntiCarCost => _antiCarCost;
        public int AntiCarLaneCost => _antiCarLaneCost;
        public int FanItemLaneCost => _fanItemLaneCost;

        private void Awake()
        {
            _fanLikes = _initialFanLikes > 0 ? _initialFanLikes : 100;
            _antiLikes = _initialAntiLikes;

            var ui = FindFirstObjectByType<SteamRush.Features.UI.FactionTugOfWarUI>();
            if (ui != null && ui.AntiMaxValue > 0)
            {
                _antiCarThreshold = ui.AntiMaxValue;
            }
            _antiCarCost = _antiCarLaneCost;
        }

        private void Start()
        {
            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
            _factionMemberCountsChanged.Invoke(FanMemberCount, AntiMemberCount);
        }

        public FactionType GetFaction(string userId)
        {
            return _userFactions.TryGetValue(userId, out FactionType faction) ? faction : _defaultFaction;
        }

        public void OnLikeReceived(string userId)
        {
            if (!_followerGate.CanSendCommand(userId))
            {
                return;
            }

            FactionType faction = GetFaction(userId);

            if (faction == FactionType.Fan)
            {
                _fanLikes++;
            }
            else
            {
                _antiLikes++;
                CheckAntiCarThreshold();
            }

            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
        }

        /// <summary>
        /// Cộng lượt thích (Likes) theo phe (hỗ trợ cộng dồn theo batch từ khán giả tap tim)
        /// </summary>
        public void AddLikes(FactionType faction, int amount = 1)
        {
            if (amount <= 0) return;

            int maxFan = 1000;
            int maxAnti = _antiCarThreshold > 0 ? _antiCarThreshold : 1000;
            var ui = FindFirstObjectByType<SteamRush.Features.UI.FactionTugOfWarUI>();
            if (ui != null)
            {
                if (ui.FanMaxValue > 0) maxFan = ui.FanMaxValue;
                if (ui.AntiMaxValue > 0) maxAnti = ui.AntiMaxValue;
            }

            if (faction == FactionType.Fan)
            {
                _fanLikes = Mathf.Clamp(_fanLikes + amount, 0, maxFan);
            }
            else
            {
                _antiLikes = Mathf.Clamp(_antiLikes + amount, 0, maxAnti);
                CheckAntiCarThreshold();
            }

            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
        }

        // Bắn event yêu cầu sinh xe cản đường khi phe Anti tích đầy thanh năng lượng
        private void CheckAntiCarThreshold()
        {
            if (_antiLikes < _antiCarThreshold)
            {
                return;
            }

            Debug.Log($"[FactionTugOfWarManager] Phe Anti full thanh ({_antiLikes}/{_antiCarThreshold}) - tự động sinh xe ngẫu nhiên và trừ {_antiCarCost} năng lượng.");

            EventBus.Publish(new RequestCarSpawnEvent(0)); // 0 = Random lane

            _antiLikes = Mathf.Max(0, _antiLikes - _antiCarCost);
        }

        // Fan Like sạc Năng Lượng dùng cho lệnh fast hoặc thả vật phẩm bảo vệ.
        // ChatLaneRunnerController gọi hàm này để tiêu hao năng lượng.
        public bool TryConsumeFanEnergy(int amount)
        {
            if (_fanLikes <= 0)
            {
                return false;
            }

            int toDeduct = Mathf.Min(amount, _fanLikes);
            _fanLikes -= toDeduct;
            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
            return _fanLikes > 0;
        }

        // GDD v1.4.1 muc 2.2: tru nang luong Fan theo tung thao tac dieu khien (doi lan -10, nhay
        // -20). Khac voi TryConsumeFanEnergy (dung cho fast, tru lien tuc theo thoi gian, van tru
        // duoc mot phan neu khong du): ham nay la "tra tien mot lan cho 1 hanh dong roi rac" - chi
        // tru DU hoac KHONG tru gi ca, va tra ve false de goi noi (ChatLaneRunnerController) biet
        // hanh dong co duoc phep thuc hien hay khong (khoa doi lan / khoa nhay khi Fan = 0%).
        public bool TrySpendFanEnergy(int cost)
        {
            if (_fanLikes <= 0)
            {
                return false;
            }

            _fanLikes = Mathf.Max(0, _fanLikes - cost);
            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
            return true;
        }

        /// <summary>
        /// [DEBUG] Tăng/giảm năng lượng phe Fan (+/- delta). Clamped [0, 1000].
        /// </summary>
        public void DebugAdjustFanEnergy(int delta)
        {
            int max = 1000;
            var ui = FindFirstObjectByType<SteamRush.Features.UI.FactionTugOfWarUI>();
            if (ui != null && ui.FanMaxValue > 0) max = ui.FanMaxValue;

            _fanLikes = Mathf.Clamp(_fanLikes + delta, 0, max);
            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
            Debug.Log($"[FactionTugOfWarManager] Debug Fan Energy: {_fanLikes}/{max} (delta: {(delta >= 0 ? "+" : "")}{delta})");
        }

        /// <summary>
        /// [DEBUG] Tăng/giảm năng lượng phe Anti (+/- delta). Clamped [0, _antiCarThreshold].
        /// Nếu tăng chạm mốc full thanh, tự động kích hoạt sinh xe cản đường!
        /// </summary>
        public void DebugAdjustAntiEnergy(int delta)
        {
            int max = _antiCarThreshold > 0 ? _antiCarThreshold : 1000;
            var ui = FindFirstObjectByType<SteamRush.Features.UI.FactionTugOfWarUI>();
            if (ui != null && ui.AntiMaxValue > 0) max = ui.AntiMaxValue;

            _antiLikes = Mathf.Clamp(_antiLikes + delta, 0, max);
            if (delta > 0)
            {
                CheckAntiCarThreshold();
            }
            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
            Debug.Log($"[FactionTugOfWarManager] Debug Anti Energy: {_antiLikes}/{max} (delta: {(delta >= 0 ? "+" : "")}{delta})");
        }

        // Doi phe qua chat: blue/#blue/fan/#fan -> Fan, red/#red/anti/#anti -> Anti (khong phan biet hoa thuong).
        // Tra ve true CHI KHI thanh vien thay doi (vao phe lan dau hoac doi sang phe khac).
        public bool OnChatCommand(string userId, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            string normalized = message.Trim().ToLowerInvariant();

            FactionType target;
            if (normalized == "blue" || normalized == "#blue" || normalized == "#fan" || normalized == "fan")
            {
                target = FactionType.Fan;
            }
            else if (normalized == "red" || normalized == "#red" || normalized == "#anti" || normalized == "anti")
            {
                target = FactionType.Anti;
            }
            else
            {
                return false; // khong phai lenh phe
            }

            // Chi kiem tra Follow khi that su la lenh phe, de comment binh thuong cua
            // nguoi chua Follow khong bi log/publish event moi lan.
            if (!_followerGate.CanSendCommand(userId))
            {
                return false;
            }

            // Khong dung GetFaction: no tra mac dinh Fan cho nguoi chua co phe.
            if (_userFactions.TryGetValue(userId, out FactionType current) && current == target)
            {
                return false; // nhan lai dung phe cu
            }

            SetFaction(userId, target);
            return true;
        }

        // Doi phe theo hanh vi donate (GDD v1.3 muc 4): tang qua bay -> Anti, tang Khien/Mau -> Fan.
        public void OnDonateReceived(string userId, bool isTrapGift)
        {
            if (!_followerGate.CanSendCommand(userId))
            {
                return;
            }

            SetFaction(userId, isTrapGift ? FactionType.Anti : FactionType.Fan);
        }

        // Phe Anti nhắn 1, 2, 3 để thả xe cản đường trên làn mong muốn, giá 100 năng lượng / xe.
        // ===== [Dhuy] BEGIN - F7 Unlimited Mode: bỏ qua trừ năng lượng khi SingleObstacleSpawner
        // đang ở Unlimited Mode (60s), giữ nguyên hành vi cũ khi không active. =====
        public bool TrySpawnAntiObstacleCar(string userId, int laneIndex)
        {
            if (!_followerGate.CanSendCommand(userId))
            {
                return false;
            }

            var spawner = FindFirstObjectByType<StreamRushLive.Features.Spawning.SingleObstacleSpawner>();
            bool isUnlimited = spawner != null && spawner.IsUnlimitedModeActive;

            if (!isUnlimited)
            {
                if (_antiLikes < _antiCarLaneCost)
                {
                    Debug.LogWarning($"[FactionTugOfWarManager] Phe Anti không đủ năng lượng! Cần {_antiCarLaneCost}, hiện có {_antiLikes}.");
                    return false;
                }

                _antiLikes -= _antiCarLaneCost;
                _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
            }

            Debug.Log($"[FactionTugOfWarManager] Phe Anti ({userId}) {(isUnlimited ? "[UNLIMITED MODE] " : "")}spawn xe cản đường trên Làn {laneIndex}!");
            EventBus.Publish(new RequestCarSpawnEvent(laneIndex));
            return true;
        }
        // ===== [Dhuy] END =====

        // Phe Fan nhắn fan 1, fan 2, fan 3 hoặc #shield/#buff để thả item hỗ trợ Runner trên làn mong muốn
        public bool TrySpawnFanItem(string userId, int laneIndex, bool isShield = false)
        {
            if (!_followerGate.CanSendCommand(userId))
            {
                return false;
            }

            if (_fanLikes < _fanItemLaneCost)
            {
                Debug.LogWarning($"[FactionTugOfWarManager] Phe Fan không đủ năng lượng! Cần {_fanItemLaneCost}, hiện có {_fanLikes}.");
                return false;
            }

            var spawner = FindFirstObjectByType<StreamRushLive.Features.Spawning.SingleObstacleSpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[FactionTugOfWarManager] Không tìm thấy SingleObstacleSpawner để thả item.");
                return false;
            }

            bool spawned = spawner.TriggerSpawnFanItem(laneIndex, isShield);
            if (!spawned) return false;

            _fanLikes -= _fanItemLaneCost;
            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);

            Debug.Log($"[FactionTugOfWarManager] Phe Fan ({userId}) tiêu hao {_fanItemLaneCost} năng lượng -> Thả {(isShield ? "Khiên" : "Bình Năng Lượng")} trên Làn {laneIndex}!");
            return true;
        }

        public bool HasFaction(string userId) => _userFactions.ContainsKey(userId);

        public List<string> GetMembersOfFaction(FactionType faction)
        {
            var list = new List<string>();
            foreach (var kvp in _userFactions)
            {
                if (kvp.Value == faction) list.Add(kvp.Key);
            }
            return list;
        }

        public void SetFaction(string userId, FactionType faction)
        {
            _userFactions[userId] = faction;
            _factionMemberCountsChanged.Invoke(FanMemberCount, AntiMemberCount);
        }
    }
}   
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

    // Quan ly "keo co" 2 phe Fan/Anti (GDD v1.3 muc 4 - Hai phe). Tra cuu phe theo userId O(1)
    // qua Dictionary. KHONG goi thang SingleObstacleSpawner.cs (DangHuy, S1-23) - chi Publish
    // RequestCarSpawnEvent qua EventBus khi du dieu kien; 1 adapter rieng se noi 2 ben sau khi
    // S1-23 co san (xem ghi chu trong EventBus.cs).
    // Bao du lieu ra ngoai (UI) qua UnityEvent - FactionTugOfWarUI la View thuan, khong tu doc
    // Dictionary cua class nay.
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

        // GDD v1.3 muc 2: "Khan gia DA FOLLOW moi duoc tu do chon gia nhap 1 trong 2 phe" - Like/
        // doi phe/donate deu phai qua Gate nay truoc, giong het cach ChatRunnerQueueManager dang
        // dung cho lenh dieu khien. Chua gan IFollowerStatusProvider that -> mac dinh cho qua (mock).
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

            // Tự động đồng bộ ngưỡng spawn xe tự động bằng đúng dung lượng full thanh Anti (Full thanh mới spawn xe)
            var ui = FindFirstObjectByType<SteamRush.Features.UI.FactionTugOfWarUI>();
            if (ui != null && ui.AntiMaxValue > 0)
            {
                _antiCarThreshold = ui.AntiMaxValue;
            }
            // Trừ số lượng bằng với số lượng như lúc player spawn xe (_antiCarLaneCost = 100)
            _antiCarCost = _antiCarLaneCost;
        }

        private void Start()
        {
            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
            _factionMemberCountsChanged.Invoke(FanMemberCount, AntiMemberCount);
        }

        // Doc phe hien tai cua 1 nguoi dung - chua tung xuat hien thi mac dinh _defaultFaction.
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

        // Doi phe qua chat: #FAN/#Blue -> Fan, #ANTI/#Red -> Anti (khong phan biet hoa thuong).
        public void OnChatCommand(string userId, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (!_followerGate.CanSendCommand(userId))
            {
                return;
            }

            string normalized = message.Trim().ToLowerInvariant();

            if (normalized == "blue" || normalized == "#blue" || normalized == "#fan" || normalized == "fan")
            {
                SetFaction(userId, FactionType.Fan);
            }
            else if (normalized == "red" || normalized == "#red" || normalized == "#anti" || normalized == "anti")
            {
                SetFaction(userId, FactionType.Anti);
            }
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

        public void SetFaction(string userId, FactionType faction)
        {
            _userFactions[userId] = faction;
            _factionMemberCountsChanged.Invoke(FanMemberCount, AntiMemberCount);
        }
    }
}
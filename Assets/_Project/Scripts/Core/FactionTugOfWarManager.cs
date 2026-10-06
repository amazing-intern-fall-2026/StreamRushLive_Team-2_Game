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
        [Header("Auto Car Spawn Settings")]
        [Tooltip("If true, auto-spawns vehicle when Anti energy bar reaches 100% capacity.")]
        [SerializeField] private bool _autoSpawnOnFullEnergy = true;

        [Tooltip("Energy threshold for Anti team to auto-spawn obstacle vehicles (Full bar = AntiMaxValue). Default = 1000.")]
        [SerializeField] private int _antiCarThreshold = 1000;

        private float _lastAutoSpawnAttemptTime;
        private const float AUTO_SPAWN_RETRY_INTERVAL = 0.5f;

        public bool AutoSpawnOnFullEnergy
        {
            get => _autoSpawnOnFullEnergy;
            set => _autoSpawnOnFullEnergy = value;
        }

        private int _antiCarCost = 100;
        private int _antiCarLaneCost = 100;
        private int _fanItemLaneCost = 0;
        private int _fanEnergyGiftAmount = 300;
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
        public int AntiCarCost => StreamRushLive.Features.Gifts.GiftManager.Instance != null 
            ? StreamRushLive.Features.Gifts.GiftManager.Instance.SedanCarCost 
            : _antiCarCost;

        public int AntiCarLaneCost 
        { 
            get => StreamRushLive.Features.Gifts.GiftManager.Instance != null 
                ? StreamRushLive.Features.Gifts.GiftManager.Instance.SedanCarCost 
                : _antiCarLaneCost; 
            set => _antiCarLaneCost = value; 
        }

        public int FanItemLaneCost 
        { 
            get => StreamRushLive.Features.Gifts.GiftManager.Instance != null 
                ? StreamRushLive.Features.Gifts.GiftManager.Instance.ShieldEnergyCost 
                : _fanItemLaneCost; 
            set => _fanItemLaneCost = value; 
        }

        public int FanEnergyGiftAmount 
        { 
            get => StreamRushLive.Features.Gifts.GiftManager.Instance != null 
                ? StreamRushLive.Features.Gifts.GiftManager.Instance.BlueEnergyBottleAmount 
                : _fanEnergyGiftAmount; 
            set => _fanEnergyGiftAmount = value; 
        }

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

        public int GetAntiEnergyMax()
        {
            var ui = FindFirstObjectByType<SteamRush.Features.UI.FactionTugOfWarUI>();
            if (ui != null && ui.AntiMaxValue > 0)
            {
                _antiCarThreshold = ui.AntiMaxValue;
                return ui.AntiMaxValue;
            }
            return _antiCarThreshold > 0 ? _antiCarThreshold : 1000;
        }

        public int GetFanEnergyMax()
        {
            var ui = FindFirstObjectByType<SteamRush.Features.UI.FactionTugOfWarUI>();
            if (ui != null && ui.FanMaxValue > 0)
            {
                return ui.FanMaxValue;
            }
            return 1000;
        }

        public int GetVehicleEnergyCost(StreamRushLive.Features.Spawning.VehicleTier tier)
        {
            var gm = StreamRushLive.Features.Gifts.GiftManager.Instance ?? FindFirstObjectByType<StreamRushLive.Features.Gifts.GiftManager>();
            if (gm != null)
            {
                var config = gm.GetCarConfig(tier);
                if (config != null) return config.energyCost;
            }
            return _antiCarCost > 0 ? _antiCarCost : 10;
        }

        private void Update()
        {
            if (_autoSpawnOnFullEnergy && _antiLikes >= GetAntiEnergyMax())
            {
                if (Time.time - _lastAutoSpawnAttemptTime >= AUTO_SPAWN_RETRY_INTERVAL)
                {
                    _lastAutoSpawnAttemptTime = Time.time;
                    TryAutoSpawnCarFromFullEnergy();
                }
            }
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
                int maxFan = GetFanEnergyMax();
                _fanLikes = Mathf.Clamp(_fanLikes + 1, 0, maxFan);
            }
            else
            {
                int maxAnti = GetAntiEnergyMax();
                _antiLikes = Mathf.Clamp(_antiLikes + 1, 0, maxAnti);
                CheckAntiCarThreshold();
            }

            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
        }

        public void AddLikes(FactionType faction, int amount = 1)
        {
            if (amount <= 0) return;

            int maxFan = GetFanEnergyMax();
            int maxAnti = GetAntiEnergyMax();

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

        private void CheckAntiCarThreshold()
        {
            if (_antiLikes >= GetAntiEnergyMax())
            {
                TryAutoSpawnCarFromFullEnergy();
            }
        }

        /// <summary>
        /// Automatically spawns an obstacle vehicle when Red energy reaches 100% full capacity,
        /// and deducts the required energy cost for that vehicle from Red energy.
        /// </summary>
        public bool TryAutoSpawnCarFromFullEnergy()
        {
            if (!_autoSpawnOnFullEnergy) return false;

            int maxThreshold = GetAntiEnergyMax();
            if (_antiLikes < maxThreshold)
            {
                return false;
            }

            var spawner = FindFirstObjectByType<StreamRushLive.Features.Spawning.SingleObstacleSpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[FactionTugOfWarManager] SingleObstacleSpawner not found to auto-spawn vehicle on full energy.");
                return false;
            }

            // Determine tier to spawn (uses active vehicle phase if running, otherwise default Sedan)
            StreamRushLive.Features.Spawning.VehicleTier targetTier = spawner.ActiveVehiclePhase ?? StreamRushLive.Features.Spawning.VehicleTier.SedanCar;
            int carCost = GetVehicleEnergyCost(targetTier);

            // Attempt to spawn vehicle on an available lane
            bool spawned = spawner.TriggerSpawnCarFromAntiLikes(targetTier);
            if (!spawned)
            {
                // Lanes currently occupied or max concurrent limit reached, will retry next frame
                return false;
            }

            // Successfully spawned! Deduct the required energy cost for this vehicle
            _antiLikes = Mathf.Max(0, _antiLikes - carCost);
            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);

            // Audio & HUD announcements
            PlayCarSpawnSFX(targetTier);
            var hud = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            hud?.ShowAntiAction("Red Team", $"Full Energy -> {targetTier} (-{carCost})");
            hud?.ShowStatusPopup($"Full Energy -> {targetTier} (-{carCost} Energy)", false);

            Debug.Log($"[FactionTugOfWarManager] Red Energy FULL ({maxThreshold})! Auto-spawned [{targetTier}] and deducted {carCost} energy. Remaining: {_antiLikes}");
            return true;
        }

        private void PlayCarSpawnSFX(StreamRushLive.Features.Spawning.VehicleTier tier)
        {
            switch (tier)
            {
                case StreamRushLive.Features.Spawning.VehicleTier.HeavyTruck:
                    AudioManager.Instance?.PlaySFX(SFXType.HeavyTruckHorn, 1.0f);
                    break;
                case StreamRushLive.Features.Spawning.VehicleTier.PickupTruck:
                    AudioManager.Instance?.PlaySFX(SFXType.PickupHorn, 0.9f);
                    break;
                case StreamRushLive.Features.Spawning.VehicleTier.SedanCar:
                default:
                    AudioManager.Instance?.PlaySFX(SFXType.AntiCarSpawn, 0.9f);
                    break;
            }
        }

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

        public bool TrySpendFanEnergy(int cost)
        {
            if (_fanLikes <= 0 || _fanLikes < cost)
            {
                return false;
            }

            _fanLikes = Mathf.Max(0, _fanLikes - cost);
            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
            return true;
        }

        public bool TrySpendAntiEnergy(int cost)
        {
            var spawner = FindFirstObjectByType<StreamRushLive.Features.Spawning.SingleObstacleSpawner>();
            if (spawner != null && spawner.IsUnlimitedModeActive)
            {
                return true;
            }

            if (_antiLikes <= 0 || _antiLikes < cost)
            {
                return false;
            }

            _antiLikes = Mathf.Max(0, _antiLikes - cost);
            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
            return true;
        }

        public void DebugAdjustFanEnergy(int delta)
        {
            int max = GetFanEnergyMax();
            _fanLikes = Mathf.Clamp(_fanLikes + delta, 0, max);
            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
        }

        public void DebugAdjustAntiEnergy(int delta)
        {
            int max = GetAntiEnergyMax();
            _antiLikes = Mathf.Clamp(_antiLikes + delta, 0, max);
            if (delta > 0)
            {
                CheckAntiCarThreshold();
            }
            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
        }

        public bool OnChatCommand(string userId, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            string normalized = message.Trim().ToLowerInvariant();

            FactionType target;
            if (normalized == "blue")
            {
                target = FactionType.Fan;
            }
            else if (normalized == "red")
            {
                target = FactionType.Anti;
            }
            else
            {
                return false;
            }

            if (!_followerGate.CanSendCommand(userId))
            {
                return false;
            }

            if (_userFactions.TryGetValue(userId, out FactionType current) && current == target)
            {
                return false;
            }

            SetFaction(userId, target);
            return true;
        }

        public void OnDonateReceived(string userId, bool isTrapGift)
        {
            if (!_followerGate.CanSendCommand(userId))
            {
                return;
            }

            SetFaction(userId, isTrapGift ? FactionType.Anti : FactionType.Fan);
        }

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
                if (_antiLikes <= 0 || _antiLikes < _antiCarLaneCost)
                {
                    return false;
                }

                _antiLikes -= _antiCarLaneCost;
                _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
            }

            EventBus.Publish(new RequestCarSpawnEvent(laneIndex));
            AudioManager.Instance?.PlaySFX(SFXType.AntiCarSpawn);
            return true;
        }

        public void AddFanEnergy(int amount) => DebugAdjustFanEnergy(amount);

        public void AddAntiEnergy(int amount) => DebugAdjustAntiEnergy(amount);

        public bool TryActivateFanGift(string userId, int laneIndex = 0, bool isShield = false)
        {
            if (!_followerGate.CanSendCommand(userId))
            {
                return false;
            }

            if (isShield)
            {
                int cost = StreamRushLive.Features.Gifts.GiftManager.Instance != null 
                    ? StreamRushLive.Features.Gifts.GiftManager.Instance.ShieldEnergyCost 
                    : _fanItemLaneCost;

                if (cost > 0 && _fanLikes < cost)
                {
                    return false;
                }

                if (cost > 0)
                {
                    _fanLikes -= cost;
                    _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
                }

                if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
                {
                    return StreamRushLive.Features.Gifts.GiftManager.Instance.ActivateShield(userId);
                }

                var runner = FindFirstObjectByType<SteamRush.Features.Runner.ChatLaneRunnerController>();
                var giftEffects = runner != null
                    ? (runner.GetComponent<StreamRushLive.Features.Spawning.RunnerGiftEffects>() ?? runner.GetComponentInChildren<StreamRushLive.Features.Spawning.RunnerGiftEffects>())
                    : FindFirstObjectByType<StreamRushLive.Features.Spawning.RunnerGiftEffects>();

                if (giftEffects != null)
                {
                    giftEffects.ActivateShield(15f);
                }
                return true;
            }
            else
            {
                if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
                {
                    StreamRushLive.Features.Gifts.GiftManager.Instance.AddBlueEnergy(userId);
                    return true;
                }

                int amount = _fanEnergyGiftAmount > 0 ? _fanEnergyGiftAmount : 300;
                DebugAdjustFanEnergy(amount);
                return true;
            }
        }

        public bool TrySpawnFanItem(string userId, int laneIndex, bool isShield = false)
            => TryActivateFanGift(userId, laneIndex, isShield);

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
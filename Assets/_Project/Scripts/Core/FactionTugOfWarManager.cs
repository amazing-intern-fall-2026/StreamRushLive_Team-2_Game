using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SteamRush.Core;

namespace SteamRush.Features.StreamIntegration
{
    public enum FactionType
    {
        Blue = 0,
        Red = 1,

        [System.Obsolete("Use Blue instead.")]
        Fan = 0,
        [System.Obsolete("Use Red instead.")]
        Anti = 1
    }

    /// <summary>
    /// Manages the tug-of-war balance between Blue and Red teams, viewer memberships, and ability costs.
    /// Acts as the coordinator / arbiter between the two FactionEnergyPools (Blue & Red).
    /// </summary>
    public class FactionTugOfWarManager : MonoBehaviour
    {
        [Header("Faction Energy Pools (Shared Component)")]
        [Tooltip("Energy pool for Team Blue (Runner support).")]
        [SerializeField] private FactionEnergyPool _blueTeamPool;
        [Tooltip("Energy pool for Team Red (Obstacle hazard).")]
        [SerializeField] private FactionEnergyPool _redTeamPool;

        public FactionEnergyPool BlueTeamPool => _blueTeamPool;
        public FactionEnergyPool RedTeamPool => _redTeamPool;

        [Header("Auto Obstacle Spawn Settings")]
        [Tooltip("If true, auto-spawns vehicle when Red energy bar reaches 100% capacity.")]
        [SerializeField] private bool _autoSpawnOnFullEnergy = true;

        [Tooltip("Energy threshold for Red team to auto-spawn obstacle vehicles (Full bar = RedMaxValue). Default = 1000.")]
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
        [SerializeField] private FactionType _defaultFaction = FactionType.Blue;

        [Serializable] public class FactionValuesChangedEvent : UnityEvent<int, int> { }
        [SerializeField] private FactionValuesChangedEvent _factionValuesChanged = new FactionValuesChangedEvent();
        public FactionValuesChangedEvent FactionValuesChanged => _factionValuesChanged;

        [Serializable] public class FactionMemberCountsChangedEvent : UnityEvent<int, int> { }
        [SerializeField] private FactionMemberCountsChangedEvent _factionMemberCountsChanged = new FactionMemberCountsChangedEvent();
        public FactionMemberCountsChangedEvent FactionMemberCountsChanged => _factionMemberCountsChanged;

        private readonly FollowerGate _followerGate = new FollowerGate();
        private readonly Dictionary<string, FactionType> _userFactions = new Dictionary<string, FactionType>();

        public int BlueMemberCount
        {
            get
            {
                int count = 0;
                foreach (var f in _userFactions.Values)
                {
                    if (f == FactionType.Blue) count++;
                }
                return count;
            }
        }

        public int RedMemberCount
        {
            get
            {
                int count = 0;
                foreach (var f in _userFactions.Values)
                {
                    if (f == FactionType.Red) count++;
                }
                return count;
            }
        }

        public int FanMemberCount => BlueMemberCount;
        public int AntiMemberCount => RedMemberCount;

        [SerializeField] private int _initialFanLikes = 100;
        [SerializeField] private int _initialAntiLikes = 200;

        private int _fanLikes;
        private int _antiLikes;

        public int BlueLikes => _blueTeamPool != null ? _blueTeamPool.CurrentEnergy : _fanLikes;
        public int RedLikes => _redTeamPool != null ? _redTeamPool.CurrentEnergy : _antiLikes;
        public int FanLikes => BlueLikes;
        public int AntiLikes => RedLikes;

        public int RedObstacleThreshold => _antiCarThreshold;
        public int AntiCarThreshold => RedObstacleThreshold;

        public int RedObstacleCost => StreamRushLive.Features.Gifts.GiftManager.Instance != null 
            ? StreamRushLive.Features.Gifts.GiftManager.Instance.SedanCarCost 
            : _antiCarCost;
        public int AntiCarCost => RedObstacleCost;

        public int RedObstacleLaneCost 
        { 
            get => StreamRushLive.Features.Gifts.GiftManager.Instance != null 
                ? StreamRushLive.Features.Gifts.GiftManager.Instance.SedanCarCost 
                : _antiCarLaneCost; 
            set => _antiCarLaneCost = value; 
        }
        public int AntiCarLaneCost 
        {
            get => RedObstacleLaneCost;
            set => RedObstacleLaneCost = value;
        }

        public int BlueItemLaneCost 
        { 
            get => StreamRushLive.Features.Gifts.GiftManager.Instance != null 
                ? StreamRushLive.Features.Gifts.GiftManager.Instance.ShieldEnergyCost 
                : _fanItemLaneCost; 
            set => _fanItemLaneCost = value; 
        }
        public int FanItemLaneCost
        {
            get => BlueItemLaneCost;
            set => BlueItemLaneCost = value;
        }

        public int BlueEnergyGiftAmount 
        { 
            get => StreamRushLive.Features.Gifts.GiftManager.Instance != null 
                ? StreamRushLive.Features.Gifts.GiftManager.Instance.BlueEnergyBottleAmount 
                : _fanEnergyGiftAmount; 
            set => _fanEnergyGiftAmount = value; 
        }
        public int FanEnergyGiftAmount
        {
            get => BlueEnergyGiftAmount;
            set => BlueEnergyGiftAmount = value;
        }

        /// <summary>
        /// Tug-of-war balance ratio between Blue and Red: Blue / (Blue + Red). Returns 0.5f if both are 0.
        /// </summary>
        public float TugOfWarRatio
        {
            get
            {
                int blue = FanLikes;
                int red = AntiLikes;
                int total = blue + red;
                return total > 0 ? (float)blue / total : 0.5f;
            }
        }

        private void Awake()
        {
            var ui = FindFirstObjectByType<SteamRush.Features.UI.FactionTugOfWarUI>();
            if (ui != null && ui.RedMaxValue > 0)
            {
                _antiCarThreshold = ui.RedMaxValue;
            }
            _antiCarCost = _antiCarLaneCost;

            EnsureEnergyPools();
        }

        private void EnsureEnergyPools()
        {
            int initialFan = _initialFanLikes > 0 ? _initialFanLikes : 100;
            int initialAnti = _initialAntiLikes > 0 ? _initialAntiLikes : 200;
            int maxFan = GetFanEnergyMax();
            int maxAnti = GetAntiEnergyMax();

            if (_blueTeamPool == null)
            {
                var pools = GetComponents<FactionEnergyPool>();
                foreach (var p in pools)
                {
                    if (p.Faction == FactionType.Blue) { _blueTeamPool = p; break; }
                }
                if (_blueTeamPool == null)
                {
                    _blueTeamPool = gameObject.AddComponent<FactionEnergyPool>();
                    _blueTeamPool.Initialize(FactionType.Blue, initialFan, maxFan);
                }
            }
            else
            {
                _blueTeamPool.Initialize(FactionType.Blue, initialFan, maxFan);
            }

            if (_redTeamPool == null)
            {
                var pools = GetComponents<FactionEnergyPool>();
                foreach (var p in pools)
                {
                    if (p.Faction == FactionType.Red) { _redTeamPool = p; break; }
                }
                if (_redTeamPool == null)
                {
                    _redTeamPool = gameObject.AddComponent<FactionEnergyPool>();
                    _redTeamPool.Initialize(FactionType.Red, initialAnti, maxAnti);
                }
            }
            else
            {
                _redTeamPool.Initialize(FactionType.Red, initialAnti, maxAnti);
            }

            _fanLikes = _blueTeamPool.CurrentEnergy;
            _antiLikes = _redTeamPool.CurrentEnergy;

            _blueTeamPool.OnEnergyChanged += (curr, max) => SyncFactionValues();
            _redTeamPool.OnEnergyChanged += (curr, max) =>
            {
                SyncFactionValues();
                CheckAntiCarThreshold();
            };
        }

        private void Start()
        {
            SyncFactionValues();
            _factionMemberCountsChanged.Invoke(FanMemberCount, AntiMemberCount);
        }

        private void SyncFactionValues()
        {
            _fanLikes = _blueTeamPool != null ? _blueTeamPool.CurrentEnergy : _fanLikes;
            _antiLikes = _redTeamPool != null ? _redTeamPool.CurrentEnergy : _antiLikes;
            _factionValuesChanged.Invoke(_fanLikes, _antiLikes);
        }

        public int GetAntiEnergyMax()
        {
            if (_redTeamPool != null && _redTeamPool.MaxEnergy > 0)
            {
                return _redTeamPool.MaxEnergy;
            }

            var ui = FindFirstObjectByType<SteamRush.Features.UI.FactionTugOfWarUI>();
            if (ui != null && ui.RedMaxValue > 0)
            {
                _antiCarThreshold = ui.RedMaxValue;
                return ui.RedMaxValue;
            }
            return _antiCarThreshold > 0 ? _antiCarThreshold : 1000;
        }

        public int GetFanEnergyMax()
        {
            if (_blueTeamPool != null && _blueTeamPool.MaxEnergy > 0)
            {
                return _blueTeamPool.MaxEnergy;
            }

            var ui = FindFirstObjectByType<SteamRush.Features.UI.FactionTugOfWarUI>();
            if (ui != null && ui.BlueMaxValue > 0)
            {
                return ui.BlueMaxValue;
            }
            return 1000;
        }

        public int GetVehicleEnergyCost(StreamRushLive.Features.Spawning.ObstacleType tier)
        {
            var gm = StreamRushLive.Features.Gifts.GiftManager.Instance ?? FindFirstObjectByType<StreamRushLive.Features.Gifts.GiftManager>();
            if (gm != null)
            {
                var config = gm.GetCarConfig(tier);
                if (config != null) return config.energyCost;
            }
            return _antiCarCost > 0 ? _antiCarCost : 10;
        }

#pragma warning disable CS0618
        [System.Obsolete("VehicleTier is deprecated. Use ObstacleType instead.")]
        public int GetVehicleEnergyCost(StreamRushLive.Features.Spawning.VehicleTier tier) => GetVehicleEnergyCost((StreamRushLive.Features.Spawning.ObstacleType)tier);
#pragma warning restore CS0618

        private void Update()
        {
            if (_autoSpawnOnFullEnergy && _redTeamPool != null && _redTeamPool.IsFull)
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
            AddLikes(faction, 1);
        }

        public void AddLikes(FactionType faction, int amount = 1)
        {
            if (amount <= 0) return;

            if (faction == FactionType.Blue)
            {
                if (_blueTeamPool != null)
                {
                    _blueTeamPool.AddEnergy(amount);
                }
                else
                {
                    int maxFan = GetFanEnergyMax();
                    _fanLikes = Mathf.Clamp(_fanLikes + amount, 0, maxFan);
                    SyncFactionValues();
                }
            }
            else
            {
                if (_redTeamPool != null)
                {
                    _redTeamPool.AddEnergy(amount);
                }
                else
                {
                    int maxAnti = GetAntiEnergyMax();
                    _antiLikes = Mathf.Clamp(_antiLikes + amount, 0, maxAnti);
                    SyncFactionValues();
                    CheckAntiCarThreshold();
                }
            }
        }

        private void CheckAntiCarThreshold()
        {
            bool isFull = _redTeamPool != null ? _redTeamPool.IsFull : (_antiLikes >= GetAntiEnergyMax());
            if (isFull)
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
            bool isFull = _redTeamPool != null ? _redTeamPool.IsFull : (_antiLikes >= maxThreshold);
            if (!isFull)
            {
                return false;
            }

            var spawner = FindFirstObjectByType<StreamRushLive.Features.Spawning.SingleObstacleSpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[FactionTugOfWarManager] SingleObstacleSpawner not found to auto-spawn vehicle on full energy.");
                return false;
            }

            // Determine tier to spawn (uses active vehicle phase if running, otherwise default Sedan / Car)
            StreamRushLive.Features.Spawning.ObstacleType targetTier = spawner.ActiveVehiclePhase ?? StreamRushLive.Features.Spawning.ObstacleType.Car;
            int carCost = GetVehicleEnergyCost(targetTier);

            // Attempt to spawn vehicle on an available lane
            bool spawned = spawner.TriggerSpawnObstacleFromRedEnergy(targetTier);
            if (!spawned)
            {
                // Lanes currently occupied or max concurrent limit reached, will retry next frame
                return false;
            }

            // Successfully spawned! Deduct the required energy cost for this vehicle
            if (_redTeamPool != null)
            {
                _redTeamPool.TryConsumeEnergy(carCost);
            }
            else
            {
                _antiLikes = Mathf.Max(0, _antiLikes - carCost);
                SyncFactionValues();
            }

            // Audio & HUD announcements
            PlayCarSpawnSFX(targetTier);
            var hud = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            hud?.ShowHazardAction("Red Team", $"Full Energy -> {targetTier} (-{carCost})");
            hud?.ShowStatusPopup($"Full Energy -> {targetTier} (-{carCost} Energy)", false);

            Debug.Log($"[FactionTugOfWarManager] Red Energy FULL ({maxThreshold})! Auto-spawned [{targetTier}] and deducted {carCost} energy. Remaining: {RedLikes}");
            return true;
        }

        private void PlayCarSpawnSFX(StreamRushLive.Features.Spawning.ObstacleType tier)
        {
            switch (tier)
            {
                case StreamRushLive.Features.Spawning.ObstacleType.Trains:
                    AudioManager.Instance?.PlaySFX(SFXType.HeavyTruckHorn, 1.0f);
                    break;
                case StreamRushLive.Features.Spawning.ObstacleType.Animals:
                    AudioManager.Instance?.PlaySFX(SFXType.PickupHorn, 0.9f);
                    break;
                case StreamRushLive.Features.Spawning.ObstacleType.Car:
                default:
                    AudioManager.Instance?.PlaySFX(SFXType.AntiCarSpawn, 0.9f);
                    break;
            }
        }

        public bool TryConsumeBlueEnergy(int amount)
        {
            if (_blueTeamPool != null)
            {
                return _blueTeamPool.TryConsumeEnergy(amount);
            }

            if (_fanLikes <= 0) return false;
            int toDeduct = Mathf.Min(amount, _fanLikes);
            _fanLikes -= toDeduct;
            SyncFactionValues();
            return _fanLikes > 0;
        }

        public bool TryConsumeFanEnergy(int amount) => TryConsumeBlueEnergy(amount);

        /// <summary>
        /// Deducts a percentage of maximum Blue energy (0..100%).
        /// E.g. 20% penalty on a 1000 max capacity deducts 200 points.
        /// </summary>
        public bool TryConsumeBlueEnergyPercent(float percent)
        {
            if (percent <= 0f) return true;

            if (_blueTeamPool != null)
            {
                return _blueTeamPool.TryConsumePercent(percent);
            }

            int maxBlue = GetFanEnergyMax();
            int pointsToDeduct = Mathf.RoundToInt((percent / 100f) * maxBlue);
            return TryConsumeBlueEnergy(pointsToDeduct);
        }

        public bool TryConsumeFanEnergyPercent(float percent) => TryConsumeBlueEnergyPercent(percent);

        public bool TrySpendBlueEnergy(int cost)
        {
            if (_blueTeamPool != null)
            {
                return _blueTeamPool.TryConsumeEnergy(cost);
            }

            if (_fanLikes <= 0 || _fanLikes < cost) return false;
            _fanLikes = Mathf.Max(0, _fanLikes - cost);
            SyncFactionValues();
            return true;
        }

        public bool TrySpendFanEnergy(int cost) => TrySpendBlueEnergy(cost);

        public bool TrySpendRedEnergy(int cost)
        {
            var spawner = FindFirstObjectByType<StreamRushLive.Features.Spawning.SingleObstacleSpawner>();
            if (spawner != null && spawner.IsUnlimitedModeActive)
            {
                return true;
            }

            if (_redTeamPool != null)
            {
                return _redTeamPool.TryConsumeEnergy(cost);
            }

            if (_antiLikes <= 0 || _antiLikes < cost) return false;
            _antiLikes = Mathf.Max(0, _antiLikes - cost);
            SyncFactionValues();
            return true;
        }

        public bool TrySpendAntiEnergy(int cost) => TrySpendRedEnergy(cost);

        public void DebugAdjustBlueEnergy(int delta)
        {
            if (_blueTeamPool != null)
            {
                _blueTeamPool.AddEnergy(delta);
            }
            else
            {
                int max = GetFanEnergyMax();
                _fanLikes = Mathf.Clamp(_fanLikes + delta, 0, max);
                SyncFactionValues();
            }
        }

        [System.Obsolete("Use DebugAdjustBlueEnergy instead.")]
        public void DebugAdjustFanEnergy(int delta) => DebugAdjustBlueEnergy(delta);

        public void DebugAdjustRedEnergy(int delta)
        {
            if (_redTeamPool != null)
            {
                _redTeamPool.AddEnergy(delta);
            }
            else
            {
                int max = GetAntiEnergyMax();
                _antiLikes = Mathf.Clamp(_antiLikes + delta, 0, max);
                if (delta > 0) CheckAntiCarThreshold();
                SyncFactionValues();
            }
        }

        [System.Obsolete("Use DebugAdjustRedEnergy instead.")]
        public void DebugAdjustAntiEnergy(int delta) => DebugAdjustRedEnergy(delta);

        public bool OnChatCommand(string userId, string message)
        {
            if (string.IsNullOrEmpty(message)) return false;

            string normalized = message.Trim().ToLowerInvariant().TrimStart('#');

            FactionType target;
            if (normalized == "blue" || normalized == "fan" || normalized == "teamblue" || normalized == "teamfan" || normalized == "doi1" || normalized == "doixanh")
            {
                target = FactionType.Blue;
            }
            else if (normalized == "red" || normalized == "anti" || normalized == "teamred" || normalized == "teamanti" || normalized == "doi2" || normalized == "doido")
            {
                target = FactionType.Red;
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
            if (!_followerGate.CanSendCommand(userId)) return;

            SetFaction(userId, isTrapGift ? FactionType.Red : FactionType.Blue);
        }

        public bool TrySpawnObstacleVehicle(string userId, int laneIndex, StreamRushLive.Features.Spawning.ObstacleType? tier = null)
        {
            if (!_followerGate.CanSendCommand(userId)) return false;

            var spawner = FindFirstObjectByType<StreamRushLive.Features.Spawning.SingleObstacleSpawner>();
            bool isUnlimited = spawner != null && spawner.IsUnlimitedModeActive;

            // Resolve tier using active vehicle phase if present (e.g. Train or Animals phase)
            StreamRushLive.Features.Spawning.ObstacleType resolvedTier = tier 
                ?? (spawner != null && spawner.ActiveVehiclePhase.HasValue ? spawner.ActiveVehiclePhase.Value : StreamRushLive.Features.Spawning.ObstacleType.Car);

            int cost = GetVehicleEnergyCost(resolvedTier);

            if (!isUnlimited)
            {
                if (!TrySpendRedEnergy(cost))
                {
                    return false;
                }
            }

            EventBus.Publish(new RequestCarSpawnEvent(laneIndex, resolvedTier));
            AudioManager.Instance?.PlaySFX(SFXType.AntiCarSpawn);
            return true;
        }

        public bool TrySpawnAntiObstacleCar(string userId, int laneIndex) => TrySpawnObstacleVehicle(userId, laneIndex);

        public void AddBlueEnergy(int amount) => DebugAdjustBlueEnergy(amount);
        [System.Obsolete("Use AddBlueEnergy instead.")]
        public void AddFanEnergy(int amount) => AddBlueEnergy(amount);

        public void AddRedEnergy(int amount) => DebugAdjustRedEnergy(amount);
        [System.Obsolete("Use AddRedEnergy instead.")]
        public void AddAntiEnergy(int amount) => AddRedEnergy(amount);

        public bool TryActivateSupportGift(string userId, int laneIndex = 0, bool isShield = false)
        {
            if (!_followerGate.CanSendCommand(userId)) return false;

            if (isShield)
            {
                int cost = StreamRushLive.Features.Gifts.GiftManager.Instance != null 
                    ? StreamRushLive.Features.Gifts.GiftManager.Instance.ShieldEnergyCost 
                    : _fanItemLaneCost;

                if (cost > 0 && !TrySpendBlueEnergy(cost))
                {
                    return false;
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
                DebugAdjustBlueEnergy(amount);
                return true;
            }
        }

        public bool TryActivateFanGift(string userId, int laneIndex = 0, bool isShield = false)
            => TryActivateSupportGift(userId, laneIndex, isShield);

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
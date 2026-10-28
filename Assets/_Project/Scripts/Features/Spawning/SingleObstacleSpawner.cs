using System.Collections;
using System.Collections.Generic;
using SteamRush.Track;
using SteamRush.Features.Runner;
using SteamRush.Features.UI.Views;
using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Coordinates obstacle spawning workflow (laser warning, lane dispatching, vehicle activation).
    /// Delegated responsibilities:
    /// - ObstacleLaneManager: Concurrency, lane offsets, and lane occupancy tracking.
    /// - ObstacleTierResolver: Prefab resolution and collider/trigger setup.
    /// - ObstacleLaserWarningService: Warning strip instantiation, scaling, and blinking countdown.
    /// 
    /// Special Modes & Hazards:
    /// - Car Storm (Unlimited Cars): Independent modifier mode (bypasses concurrency cap, rapid car spawns).
    ///   Can run simultaneously with any Vehicle Phase.
    /// - Vehicle Phases (Animals vs Train): Hierarchy system for viewer gifts (Train overrides Animals;
    ///   Animals queues behind Train; matching phases extend duration).
    /// </summary>
    public class SingleObstacleSpawner : MonoBehaviour
    {
        [Header("Obstacle Car Settings")]
        [Tooltip("Urban Street Car prefab (fallback).")]
        [SerializeField] private GameObject urbanCarPrefab;

        [Tooltip("List of PolygonCity vehicle models (general fallback).")]
        [SerializeField] private List<GameObject> vehiclePrefabs = new List<GameObject>();

        [Header("Categorized Tiered Obstacles (GDD v1.4)")]
        [Tooltip("Sedan Car (Tier 1): -20% energy, -100m penalty")]
        [SerializeField] private List<GameObject> sedanCarPrefabs = new List<GameObject>();

        [Tooltip("Hunting Beasts (Tier 2): -40% energy, -200m penalty")]
        [SerializeField] private List<GameObject> pickupTruckPrefabs = new List<GameObject>();

        [Tooltip("Train (Tier 3): -60% energy, -400m penalty")]
        [SerializeField] private List<GameObject> heavyTruckPrefabs = new List<GameObject>();

        [Tooltip("Autonomous vehicle drive speed added to world scroll speed (default: 6.5 m/s).")]
        [SerializeField] private float carDrivingSpeed = 6.5f;

        public float CarDrivingSpeed
        {
            get => carDrivingSpeed;
            set => carDrivingSpeed = Mathf.Max(0f, value);
        }

        [Tooltip("Player transform reference for spawn calculation.")]
        [SerializeField] private Transform playerReference;

        [Tooltip("WorldSpeedManager reference; automatically resolved if null.")]
        [SerializeField] private WorldSpeedManager worldSpeedManager;

        [Header("Lane Settings")]
        [Tooltip("Z coordinate of left lane (+Z looking toward camera).")]
        [SerializeField] private float laneOffsetLeft = 3.0f;

        [Tooltip("Z coordinate of middle lane.")]
        [SerializeField] private float laneOffsetCenter = 0.0f;

        [Tooltip("Z coordinate of right lane (-Z looking toward camera).")]
        [SerializeField] private float laneOffsetRight = -3.0f;

        [Header("Spawn Distance")]
        [Tooltip("Spawn distance ahead of player in meters.")]
        [SerializeField] private float spawnDistanceAhead = 35f;

        [Header("Warning Laser Settings")]
        [Tooltip("Prefab laser warning strip spawned ahead of car to warn player.")]
        [SerializeField] private GameObject laserIndicatorPrefab;

        [Tooltip("Laser warning duration before car spawns (in seconds).")]
        [SerializeField] private float laserWarningDuration = 3.5f;

        [Tooltip("Blink frequency interval for warning laser (in seconds).")]
        [SerializeField] private float laserBlinkInterval = 0.15f;

        public GameObject LaserIndicatorPrefab
        {
            get => laserIndicatorPrefab;
            set => laserIndicatorPrefab = value;
        }

        public float LaserWarningDuration
        {
            get => laserWarningDuration;
            set => laserWarningDuration = Mathf.Max(0.1f, value);
        }

        public float LaserBlinkInterval
        {
            get => laserBlinkInterval;
            set => laserBlinkInterval = Mathf.Max(0.01f, value);
        }

        [Header("Obstacle Concurrency Limit")]
        [Tooltip("Maximum concurrent active obstacles to ensure at least one open lane for runner.")]
        [SerializeField] private int maxConcurrentObstacles = 2;

        [Header("Unlimited Mode / Car Storm (Independent Modifier - F7)")]
        [Tooltip("Unlimited Cars mode duration in seconds.")]
        [SerializeField] private float unlimitedModeDuration = 60f;

        private bool _isUnlimitedModeActive;
        private float _unlimitedModeRemainingTime;
        private Coroutine _unlimitedModeCoroutine;

        public bool IsUnlimitedModeActive => _isUnlimitedModeActive;

        [Header("Vehicle Special Phase (Animals vs Train - F4/F5)")]
        [Tooltip("Pickup Truck / Heavy Truck Phase duration in seconds.")]
        [SerializeField] private float vehiclePhaseDuration = 60f;

        private ObstacleType? _activeVehiclePhase;
        private float _vehiclePhaseRemainingTime;
        private Coroutine _vehiclePhaseCoroutine;

        private struct QueuedVehiclePhase
        {
            public ObstacleType phase;
            public float duration;
            public string sender;
        }
        private readonly Queue<QueuedVehiclePhase> _vehiclePhaseQueue = new Queue<QueuedVehiclePhase>();

        /// <summary>Current active special vehicle phase (null = default Sedan / Car).</summary>
        public ObstacleType? ActiveVehiclePhase => _activeVehiclePhase;

        /// <summary>Indicates if a special vehicle phase is currently active.</summary>
        public bool IsVehiclePhaseActive => _activeVehiclePhase.HasValue;

        /// <summary>True while Hunting Beasts, Train or Car Storm is running.</summary>
        public bool IsAnySpecialSkillActive => IsVehiclePhaseActive || _isUnlimitedModeActive;

        /// <summary>Configured phase duration.</summary>
        public float VehiclePhaseDuration => StreamRushLive.Features.Gifts.GiftManager.Instance != null 
            ? StreamRushLive.Features.Gifts.GiftManager.Instance.VehiclePhaseDuration 
            : vehiclePhaseDuration;

        /// <summary>Remaining seconds of active vehicle phase.</summary>
        public float VehiclePhaseRemainingTime => _vehiclePhaseRemainingTime;

        // Victory Celebration lock: halts all subsequent obstacle and vehicle spawning requests.
        private bool _spawningLocked;
        public bool IsSpawningLocked => _spawningLocked;

        // SOLID Subsystems
        private readonly ObstacleLaneManager _laneManager = new ObstacleLaneManager();
        private readonly ObstacleTierResolver _tierResolver = new ObstacleTierResolver();
        private ObstacleLaserWarningService _laserWarningService;

        public int MaxConcurrentObstacles
        {
            get => _laneManager.MaxConcurrentObstacles;
            set
            {
                maxConcurrentObstacles = Mathf.Clamp(value, 1, 2);
                _laneManager.MaxConcurrentObstacles = maxConcurrentObstacles;
            }
        }

        public int ActiveObstacleCount => _laneManager.ActiveCount;

        private void Awake()
        {
            maxConcurrentObstacles = Mathf.Clamp(maxConcurrentObstacles, 1, 2);
            _laneManager.Configure(laneOffsetLeft, laneOffsetCenter, laneOffsetRight, maxConcurrentObstacles);
            _tierResolver.Initialize(urbanCarPrefab, vehiclePrefabs, sedanCarPrefabs, pickupTruckPrefabs, heavyTruckPrefabs);
            _laserWarningService = new ObstacleLaserWarningService(this);
        }

        private void Start()
        {
            if (worldSpeedManager == null)
            {
                worldSpeedManager = FindFirstObjectByType<WorldSpeedManager>();
            }

            EnsureLaserPrefab();
        }

        private void EnsureLaserPrefab()
        {
            if (laserIndicatorPrefab == null)
            {
                laserIndicatorPrefab = Resources.Load<GameObject>("Spawning/LaserIndicator");
            }
        }

        public void SetSpawningLocked(bool locked)
        {
            _spawningLocked = locked;
            if (locked)
            {
                _laneManager.ClearPendingLasers();
                _vehiclePhaseQueue.Clear();
            }
        }

        public int GetActiveObstacleCount() => _laneManager.ActiveCount;

        public bool CanSpawnObstacle() => _laneManager.CanSpawnObstacle(_isUnlimitedModeActive);

        public bool CanSpawnObstacleOnLane(int laneIndex) => _laneManager.CanSpawnObstacleOnLane(laneIndex, _isUnlimitedModeActive);

        public float GetLaneOffsetZ(int laneIndex) => _laneManager.GetLaneOffsetZ(laneIndex);

        /// <summary>
        /// Spawns vehicle of specified type (Car, Animals, Trains).
        /// Randomizes lane if laneIndex <= 0.
        /// When forceSpawn is true, bypasses concurrency cap and displaces lower-tier obstacles if needed.
        /// </summary>
        public bool TriggerSpawnCarTier(ObstacleType tier, int laneIndex = -1, bool forceSpawn = false)
        {
            if (_spawningLocked) return false;

            if (!forceSpawn && !_laneManager.CanSpawnObstacle(_isUnlimitedModeActive))
            {
                Debug.LogWarning($"[SingleObstacleSpawner] Max concurrent obstacles reached ({maxConcurrentObstacles})! Dropped spawn request for {tier}.");
                return false;
            }

            int targetLane = _laneManager.ResolveTargetLane(laneIndex, forceSpawn, _isUnlimitedModeActive);
            if (targetLane < 1 || targetLane > 3)
            {
                Debug.LogWarning($"[SingleObstacleSpawner] No open lane available to spawn {tier}.");
                return false;
            }

            float selectedLane = _laneManager.GetLaneOffsetZ(targetLane);
            SpawnCarOnSelectedLane(targetLane, selectedLane, tier);
            return true;
        }

#pragma warning disable CS0618
        [System.Obsolete("VehicleTier is deprecated. Use ObstacleType instead.")]
        public bool TriggerSpawnCarTier(VehicleTier tier, int laneIndex = -1) => TriggerSpawnCarTier((ObstacleType)tier, laneIndex);
#pragma warning restore CS0618

        public bool TriggerSpawnCarOnLane(int laneIndex, ObstacleType? tier = null)
        {
            if (_spawningLocked) return false;

            if (!_laneManager.CanSpawnObstacleOnLane(laneIndex, _isUnlimitedModeActive))
            {
                Debug.LogWarning($"[SingleObstacleSpawner] Lane {laneIndex} occupied or max concurrent obstacles reached! Dropped spawn request.");
                return false;
            }

            float selectedLane = _laneManager.GetLaneOffsetZ(laneIndex);
            SpawnCarOnSelectedLane(laneIndex, selectedLane, tier);
            return true;
        }

#pragma warning disable CS0618
        [System.Obsolete("VehicleTier is deprecated. Use ObstacleType instead.")]
        public bool TriggerSpawnCarOnLane(int laneIndex, VehicleTier? tier) => TriggerSpawnCarOnLane(laneIndex, (ObstacleType?)tier);
#pragma warning restore CS0618

        public bool TriggerSpawnObstacleFromRedEnergy(ObstacleType? tier = null)
        {
            if (_spawningLocked) return false;

            if (!_laneManager.CanSpawnObstacle(_isUnlimitedModeActive))
            {
                Debug.LogWarning($"[SingleObstacleSpawner] Max concurrent obstacles reached ({maxConcurrentObstacles})! Dropped Red Energy spawn request.");
                return false;
            }

            int targetLane = _laneManager.ResolveTargetLane(-1, false, _isUnlimitedModeActive);
            if (targetLane < 1 || targetLane > 3)
            {
                Debug.LogWarning("[SingleObstacleSpawner] No open lane available to spawn vehicle from Red Energy.");
                return false;
            }

            return TriggerSpawnCarOnLane(targetLane, tier);
        }

#pragma warning disable CS0618
        [System.Obsolete("VehicleTier is deprecated. Use ObstacleType instead.")]
        public bool TriggerSpawnObstacleFromRedEnergy(VehicleTier? tier) => TriggerSpawnObstacleFromRedEnergy((ObstacleType?)tier);

        [System.Obsolete("Use TriggerSpawnObstacleFromRedEnergy instead.")]
        public bool TriggerSpawnCarFromAntiLikes(ObstacleType? tier = null) => TriggerSpawnObstacleFromRedEnergy(tier);

        [System.Obsolete("Use TriggerSpawnObstacleFromRedEnergy instead.")]
        public bool TriggerSpawnCarFromAntiLikes(VehicleTier? tier) => TriggerSpawnObstacleFromRedEnergy((ObstacleType?)tier);
#pragma warning restore CS0618

        #region Unlimited Mode / Car Storm (Independent)

        /// <summary>
        /// Activates Unlimited Cars mode (Car Storm).
        /// Independent from vehicle phases (Animals/Train) — can run concurrently.
        /// If already active, stacks/extends duration.
        /// </summary>
        public void ActivateUnlimitedMode(float customDuration = -1f, string sender = "Red Team")
        {
            float dur = customDuration > 0f ? customDuration : unlimitedModeDuration;

            if (!_isUnlimitedModeActive)
            {
                StartUnlimitedModeInternal(dur);
            }
            else
            {
                _unlimitedModeRemainingTime += dur;
                AntiUnlimitedTimerCircle.Instance?.ActivateTimer(_unlimitedModeRemainingTime);
                TriggerSpawnCarTier(ObstacleType.Car, -1, true);
                Debug.Log($"[SingleObstacleSpawner] Car Storm extended by {dur:F0}s (Total remaining: {_unlimitedModeRemainingTime:F0}s).");
            }
        }

        private void StartUnlimitedModeInternal(float duration)
        {
            if (_unlimitedModeCoroutine != null)
            {
                StopCoroutine(_unlimitedModeCoroutine);
            }
            _unlimitedModeCoroutine = StartCoroutine(UnlimitedModeRoutine(duration));
        }

        public void StopUnlimitedMode()
        {
            if (_unlimitedModeCoroutine != null)
            {
                StopCoroutine(_unlimitedModeCoroutine);
                _unlimitedModeCoroutine = null;
            }
            _isUnlimitedModeActive = false;
            _unlimitedModeRemainingTime = 0f;
            AntiUnlimitedTimerCircle.Instance?.DeactivateTimer();
        }

        private IEnumerator UnlimitedModeRoutine(float duration)
        {
            _isUnlimitedModeActive = true;
            _unlimitedModeRemainingTime = duration;
            Debug.Log($"[SingleObstacleSpawner] Unlimited Mode (Car Storm) activated for {duration}s.");
            AntiUnlimitedTimerCircle.Instance?.ActivateTimer(duration);
            AudioManager.Instance?.PlaySFX(SFXType.WarningSiren);

            while (_isUnlimitedModeActive && _unlimitedModeRemainingTime > 0f)
            {
                _unlimitedModeRemainingTime -= Time.deltaTime;
                _unlimitedModeRemainingTime = Mathf.Max(0f, _unlimitedModeRemainingTime);
                yield return null;
            }

            if (_isUnlimitedModeActive)
            {
                _isUnlimitedModeActive = false;
                _unlimitedModeRemainingTime = 0f;
                _unlimitedModeCoroutine = null;
                AntiUnlimitedTimerCircle.Instance?.DeactivateTimer();
                Debug.Log("[SingleObstacleSpawner] Unlimited Mode (Car Storm) finished.");
            }
        }

        public void SetUnlimitedModeDebug(bool isActive)
        {
            if (_unlimitedModeCoroutine != null)
            {
                StopCoroutine(_unlimitedModeCoroutine);
                _unlimitedModeCoroutine = null;
            }

            _isUnlimitedModeActive = isActive;
            if (isActive)
            {
                AntiUnlimitedTimerCircle.Instance?.ActivateTimer(999f);
            }
            else
            {
                AntiUnlimitedTimerCircle.Instance?.DeactivateTimer();
            }
            Debug.Log($"[SingleObstacleSpawner] Unlimited Mode = {isActive}.");
        }

        #endregion

        #region Vehicle Phase (Animals vs Train)

        /// <summary>
        /// Activates Pickup Truck (Hunting Beasts / Animals) Phase.
        /// Extends duration if already in Animals phase.
        /// Queues behind Train if Train phase is currently active.
        /// </summary>
        public void ActivatePickupTruckPhase(float customDuration = -1f, string sender = "Red Team")
        {
            float dur = customDuration > 0f ? customDuration : VehiclePhaseDuration;

            if (_activeVehiclePhase == null)
            {
                StartVehiclePhaseInternal(ObstacleType.Animals, dur);
                TriggerSpawnCarTier(ObstacleType.Animals, -1, true);
            }
            else if (_activeVehiclePhase.Value == ObstacleType.Animals)
            {
                _vehiclePhaseRemainingTime += dur;
                TriggerSpawnCarTier(ObstacleType.Animals, -1, true);
                Debug.Log($"[SingleObstacleSpawner] Animals Phase duration extended by {dur:F0}s.");
            }
            else if (_activeVehiclePhase.Value == ObstacleType.Trains)
            {
                // Train is higher tier (Whale). Queue Animals to run after Train finishes!
                _vehiclePhaseQueue.Enqueue(new QueuedVehiclePhase
                {
                    phase = ObstacleType.Animals,
                    duration = dur,
                    sender = sender
                });
                Debug.Log($"[SingleObstacleSpawner] Queued Animals Phase ({dur:F0}s) behind active Train.");
            }
        }

        /// <summary>
        /// Activates Heavy Truck (Train) Phase (Whale Gift).
        /// Overrides and crushes lower-tier Animals phase immediately.
        /// Extends duration and spawns another Train if Train phase is already active.
        /// </summary>
        public void ActivateHeavyTruckPhase(float customDuration = -1f, string sender = "Red Team")
        {
            float dur = customDuration > 0f ? customDuration : VehiclePhaseDuration;

            if (_activeVehiclePhase == null || _activeVehiclePhase.Value == ObstacleType.Animals)
            {
                if (_activeVehiclePhase.HasValue && _activeVehiclePhase.Value == ObstacleType.Animals)
                {
                    StopVehiclePhase();
                    Debug.Log("[SingleObstacleSpawner] 👑 WHALE TRAIN OVERRIDES active Animals phase!");
                }

                StartVehiclePhaseInternal(ObstacleType.Trains, dur);
                TriggerSpawnCarTier(ObstacleType.Trains, -1, true);
            }
            else if (_activeVehiclePhase.Value == ObstacleType.Trains)
            {
                _vehiclePhaseRemainingTime += dur;
                TriggerSpawnCarTier(ObstacleType.Trains, -1, true);
                Debug.Log($"[SingleObstacleSpawner] 👑 Train Phase duration extended by {dur:F0}s.");
            }
        }

        private void StartVehiclePhaseInternal(ObstacleType phase, float duration)
        {
            if (_vehiclePhaseCoroutine != null)
            {
                StopCoroutine(_vehiclePhaseCoroutine);
                _vehiclePhaseCoroutine = null;
            }

            _activeVehiclePhase = phase;
            _vehiclePhaseRemainingTime = Mathf.Max(0f, duration);
            _vehiclePhaseCoroutine = StartCoroutine(VehiclePhaseRoutine(phase));
            Debug.Log($"[SingleObstacleSpawner] Vehicle Phase = {_activeVehiclePhase} for {_vehiclePhaseRemainingTime:F0}s.");
        }

        public void StopVehiclePhase()
        {
            if (_vehiclePhaseCoroutine != null)
            {
                StopCoroutine(_vehiclePhaseCoroutine);
                _vehiclePhaseCoroutine = null;
            }
            _activeVehiclePhase = null;
            _vehiclePhaseRemainingTime = 0f;
        }

        private IEnumerator VehiclePhaseRoutine(ObstacleType phase)
        {
            while (_activeVehiclePhase.HasValue &&
                   _activeVehiclePhase.Value == phase &&
                   _vehiclePhaseRemainingTime > 0f)
            {
                _vehiclePhaseRemainingTime -= Time.deltaTime;
                _vehiclePhaseRemainingTime = Mathf.Max(0f, _vehiclePhaseRemainingTime);
                yield return null;
            }

            if (_activeVehiclePhase.HasValue && _activeVehiclePhase.Value == phase)
            {
                _activeVehiclePhase = null;
                _vehiclePhaseRemainingTime = 0f;
                _vehiclePhaseCoroutine = null;
                Debug.Log($"[SingleObstacleSpawner] Vehicle Phase {phase} ended. Returning to default Sedan.");
                OnVehiclePhaseFinished();
            }
        }

        private void OnVehiclePhaseFinished()
        {
            if (_vehiclePhaseQueue.Count > 0)
            {
                var next = _vehiclePhaseQueue.Dequeue();
                Debug.Log($"[SingleObstacleSpawner] Dequeued vehicle phase: {next.phase} ({next.duration:F0}s) from {next.sender}.");
                if (next.phase == ObstacleType.Trains)
                {
                    ActivateHeavyTruckPhase(next.duration, next.sender);
                }
                else
                {
                    ActivatePickupTruckPhase(next.duration, next.sender);
                }
            }
        }

        #endregion

        public bool IsRainHazardActive()
        {
            var wm = SteamRush.Features.Environment.WeatherHazardManager.Instance;
            if (wm == null)
            {
                wm = FindFirstObjectByType<SteamRush.Features.Environment.WeatherHazardManager>();
            }

            if (wm != null)
            {
                return wm.IsHazardActive;
            }

            var rainObj = GameObject.Find("FX_Rain");
            if (rainObj != null && rainObj.activeInHierarchy) return true;

            return false;
        }

        private void SpawnCarOnSelectedLane(int laneIndex, float selectedLane, ObstacleType? tier = null)
        {
            ResolvePlayerReference();

            if (playerReference == null)
            {
                Debug.LogWarning("[SingleObstacleSpawner] Missing Player reference.");
                return;
            }

            EnsureLaserPrefab();

            GameObject laserInstance = _laserWarningService.SpawnLaserIndicator(
                laserIndicatorPrefab,
                playerReference,
                selectedLane,
                spawnDistanceAhead);

            var obstacle = new ActiveObstacle
            {
                LaneIndex = laneIndex,
                LaneZ = selectedLane,
                LaserInstance = laserInstance,
                CarInstance = null,
                PlayerRef = playerReference,
                IsPendingSpawn = true
            };
            _laneManager.RegisterObstacle(obstacle);

            if (laserInstance != null)
            {
                _laserWarningService.StartLaserCountdown(
                    obstacle,
                    laserInstance,
                    playerReference,
                    selectedLane,
                    spawnDistanceAhead,
                    laserWarningDuration,
                    laserBlinkInterval,
                    () => _spawningLocked,
                    () => InstantiateCar(obstacle, selectedLane, tier));
            }
            else
            {
                InstantiateCar(obstacle, selectedLane, tier);
            }
        }

        private ObstacleType GetVehicleTierForSpawn(ObstacleType? requestedTier)
        {
            if (_activeVehiclePhase.HasValue)
            {
                if (requestedTier.HasValue && requestedTier.Value != ObstacleType.Car)
                {
                    return requestedTier.Value;
                }
                return _activeVehiclePhase.Value;
            }

            if (requestedTier.HasValue)
            {
                return requestedTier.Value;
            }

            return ObstacleType.Car;
        }

        private GameObject InstantiateCar(ActiveObstacle obstacle, float selectedLane, ObstacleType? requestedTier)
        {
            ResolvePlayerReference();

            if (playerReference == null)
            {
                if (obstacle != null) obstacle.IsPendingSpawn = false;
                return null;
            }

            ObstacleType chosenTier = GetVehicleTierForSpawn(requestedTier);

            Vector3 carSpawnPosition = new Vector3(
                playerReference.position.x + spawnDistanceAhead,
                0.05f,
                selectedLane);

            GameObject prefabToSpawn = _tierResolver.ResolvePrefab(chosenTier);
            if (prefabToSpawn == null)
            {
                Debug.LogWarning("[SingleObstacleSpawner] No vehicle prefabs available to spawn!");
                if (obstacle != null) obstacle.IsPendingSpawn = false;
                return null;
            }

            Quaternion spawnRot = Quaternion.Euler(0f, -90f, 0f);
            GameObject carInstance = Instantiate(prefabToSpawn, carSpawnPosition, spawnRot);

            carInstance.name = $"ObstacleCar_{chosenTier}_{prefabToSpawn.name}";
            _tierResolver.ConfigurePhysics(carInstance, chosenTier);

            if (obstacle != null)
            {
                obstacle.IsPendingSpawn = false;
                obstacle.CarInstance = carInstance;
                obstacle.PlayerRef = playerReference;
            }

            InitializeCarMovement(carInstance, chosenTier);
            return carInstance;
        }

        public GameObject GetCarPrefab(ObstacleType tier) => _tierResolver.ResolvePrefab(tier);

#pragma warning disable CS0618
        [System.Obsolete("VehicleTier is deprecated. Use ObstacleType instead.")]
        public GameObject GetCarPrefab(VehicleTier tier) => GetCarPrefab((ObstacleType)tier);
#pragma warning restore CS0618

        public bool TriggerActivateSupportGift(int laneIndex = 0, bool isShield = false)
        {
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                if (isShield)
                {
                    return StreamRushLive.Features.Gifts.GiftManager.Instance.ActivateShield("Blue Team");
                }
                else
                {
                    StreamRushLive.Features.Gifts.GiftManager.Instance.AddBlueEnergy("Blue Team");
                    return true;
                }
            }

            var runner = FindFirstObjectByType<ChatLaneRunnerController>();
            if (runner != null)
            {
                if (isShield)
                {
                    var effects = runner.GetComponent<RunnerGiftEffects>() ?? runner.GetComponentInChildren<RunnerGiftEffects>();
                    effects?.ActivateShield(15f);
                    Debug.Log("[SingleObstacleSpawner] Activated protective Shield on Runner (15s).");
                }
                return true;
            }

            return false;
        }

        public bool TriggerActivateFanGift(int laneIndex = 0, bool isShield = false)
            => TriggerActivateSupportGift(laneIndex, isShield);

        public bool TriggerSpawnFanItem(int laneIndex, bool isShield = false)
            => TriggerActivateSupportGift(laneIndex, isShield);

        private void InitializeCarMovement(GameObject carInstance, ObstacleType tier = ObstacleType.Car)
        {
            var dynamicObs = carInstance.GetComponent<DynamicObstacle>();
            if (dynamicObs == null)
            {
                dynamicObs = carInstance.AddComponent<DynamicObstacle>();
            }

            dynamicObs.ConfigureTier(tier);
            dynamicObs.Initialize(worldSpeedManager, dynamicObs.DrivingSpeed);
        }

        private void ResolvePlayerReference()
        {
            if (playerReference == null)
            {
                var runner = FindFirstObjectByType<ChatLaneRunnerController>();
                if (runner != null) playerReference = runner.transform;
            }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using SteamRush.Track;
using SteamRush.Features.Runner;
using SteamRush.Features.UI.Views;
using UnityEngine.InputSystem;
using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    public class SingleObstacleSpawner : MonoBehaviour
    {
        [Header("Obstacle Car Settings")]
        [Tooltip("Prefab xe Urban Street Car (fallback).")]
        [SerializeField] private GameObject urbanCarPrefab;

        [Tooltip("List of PolygonCity vehicle models (general fallback).")]
        [SerializeField] private List<GameObject> vehiclePrefabs = new List<GameObject>();

        [Header("Categorized Tiered Obstacles (GDD v1.4)")]
        [Tooltip("Sedan Car (Tier 1): -20% energy, -100m penalty")]
        [SerializeField] private List<GameObject> sedanCarPrefabs = new List<GameObject>();

        [Tooltip("Thú săn (Tier 2): -40% energy, -200m penalty")]
        [SerializeField] private List<GameObject> pickupTruckPrefabs = new List<GameObject>();

        [Tooltip("Tàu hỏa (Tier 3): -60% energy, -400m penalty")]
        [SerializeField] private List<GameObject> heavyTruckPrefabs = new List<GameObject>();

        [Tooltip("Autonomous vehicle drive speed added to world scroll speed (default: 6.5 m/s).")]
        [SerializeField] private float carDrivingSpeed = 6.5f;

        public float CarDrivingSpeed
        {
            get => carDrivingSpeed;
            set => carDrivingSpeed = Mathf.Max(0f, value);
        }

        [HideInInspector] [SerializeField] private GameObject shieldItemPrefab;
        [HideInInspector] [SerializeField] private GameObject energyBuffItemPrefab;
        [HideInInspector] [SerializeField] private GameObject laserIndicatorPrefab;

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

        [Header("Obstacle Concurrency Limit")]
        [Tooltip("Maximum concurrent active obstacles to ensure at least one open lane for runner.")]
        [SerializeField] private int maxConcurrentObstacles = 2;

        [Header("Unlimited Mode (Anti Faction Gift - F7)")]
        [Tooltip("Unlimited Cars mode duration in seconds.")]
        [SerializeField] private float unlimitedModeDuration = 60f;

        private bool _isUnlimitedModeActive;
        private Coroutine _unlimitedModeCoroutine;

        public bool IsUnlimitedModeActive => _isUnlimitedModeActive;

        [Header("Vehicle Special Phase (Anti Faction Gifts - F4/F5)")]
        [Tooltip("Pickup Truck / Heavy Truck Phase duration in seconds.")]
        [SerializeField] private float vehiclePhaseDuration = 60f;

        private VehicleTier? _activeVehiclePhase;
        private float _vehiclePhaseRemainingTime;
        private Coroutine _vehiclePhaseCoroutine;

        /// <summary>Current active special vehicle phase (null = default Sedan).</summary>
        public VehicleTier? ActiveVehiclePhase => _activeVehiclePhase;

        /// <summary>Indicates if a special vehicle phase is currently active.</summary>
        public bool IsVehiclePhaseActive => _activeVehiclePhase.HasValue;

        /// <summary>Configured phase duration.</summary>
        public float VehiclePhaseDuration => StreamRushLive.Features.Gifts.GiftManager.Instance != null 
            ? StreamRushLive.Features.Gifts.GiftManager.Instance.VehiclePhaseDuration 
            : vehiclePhaseDuration;

        /// <summary>Remaining seconds of active vehicle phase.</summary>
        public float VehiclePhaseRemainingTime => _vehiclePhaseRemainingTime;

        // Spawning lock (GDD v1.4.1 - Victory Celebration): called when Runner crosses finish line archway;
        // completely halts all subsequent obstacle and vehicle spawning requests.
        private bool _spawningLocked;
        public bool IsSpawningLocked => _spawningLocked;

        public void SetSpawningLocked(bool locked)
        {
            _spawningLocked = locked;
            Debug.Log($"[SingleObstacleSpawner] SpawningLocked = {locked}");
        }
        private class ActiveObstacle
        {
            public int LaneIndex;
            public float LaneZ;
            public GameObject LaserInstance;
            public GameObject CarInstance;
            public Transform PlayerRef;
            public bool IsPendingSpawn;

            public bool IsActive
            {
                get
                {
                    if (IsPendingSpawn) return true;
                    if (LaserInstance != null) return true;
                    if (CarInstance != null)
                    {
                        // Vehicle remains an active obstacle until passing behind player
                        float playerX = PlayerRef != null ? PlayerRef.position.x : 0f;
                        return CarInstance.transform.position.x > playerX - 1.5f;
                    }
                    return false;
                }
            }
        }

        private readonly List<ActiveObstacle> _activeObstacles = new List<ActiveObstacle>();

        public int MaxConcurrentObstacles
        {
            get => maxConcurrentObstacles;
            set => maxConcurrentObstacles = Mathf.Max(1, value);
        }

        public int ActiveObstacleCount => GetActiveObstacleCount();

        private void Start()
        {
            if (worldSpeedManager == null)
            {
                worldSpeedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
        }

        private void CleanupInactiveObstacles()
        {
            _activeObstacles.RemoveAll(o => !o.IsActive);
        }

        public int GetActiveObstacleCount()
        {
            CleanupInactiveObstacles();
            return _activeObstacles.Count;
        }

        public bool CanSpawnObstacle()
        {
            CleanupInactiveObstacles();
            return _isUnlimitedModeActive || _activeObstacles.Count < maxConcurrentObstacles;
        }

        public bool CanSpawnObstacleOnLane(int laneIndex)
        {
            CleanupInactiveObstacles();
            if (!_isUnlimitedModeActive && _activeObstacles.Count >= maxConcurrentObstacles)
            {
                return false;
            }

            foreach (var obs in _activeObstacles)
            {
                if (obs.LaneIndex == laneIndex && obs.IsActive)
                {
                    return false;
                }
            }

            return true;
        }

        public float GetLaneOffsetZ(int laneIndex)
        {
            if (laneIndex == 1) return laneOffsetLeft;       // +3.0f (Left lane)
            if (laneIndex == 3) return laneOffsetRight;      // -3.0f (Right lane)
            return laneOffsetCenter;                         //  0.0f (Middle lane)
        }

        /// <summary>
        /// Spawns vehicle of specified tier (SedanCar, PickupTruck, HeavyTruck).
        /// Randomizes lane if laneIndex <= 0.
        /// </summary>
        public bool TriggerSpawnCarTier(VehicleTier tier, int laneIndex = -1)
        {
            if (_spawningLocked) return false;

            CleanupInactiveObstacles();

            if (!_isUnlimitedModeActive && _activeObstacles.Count >= maxConcurrentObstacles)
            {
                Debug.LogWarning($"[SingleObstacleSpawner] Max concurrent obstacles reached ({maxConcurrentObstacles})! Dropped spawn request for {tier}.");
                return false;
            }

            int targetLane = laneIndex;
            if (targetLane <= 0 || targetLane > 3)
            {
                List<int> availableLanes = new List<int>();
                for (int i = 1; i <= 3; i++)
                {
                    if (CanSpawnObstacleOnLane(i)) availableLanes.Add(i);
                }
                if (availableLanes.Count == 0)
                {
                    Debug.LogWarning($"[SingleObstacleSpawner] No open lane available to spawn {tier}.");
                    return false;
                }
                targetLane = availableLanes[Random.Range(0, availableLanes.Count)];
            }
            else if (!CanSpawnObstacleOnLane(targetLane))
            {
                Debug.LogWarning($"[SingleObstacleSpawner] Lane {targetLane} already has an active obstacle! Skipped duplicate spawn.");
                return false;
            }

            float selectedLane = GetLaneOffsetZ(targetLane);
            SpawnCarOnSelectedLane(targetLane, selectedLane, tier);
            return true;
        }

        public bool TriggerSpawnCarOnLane(int laneIndex, VehicleTier? tier = null)
        {
            if (_spawningLocked) return false;

            CleanupInactiveObstacles();

            if (!_isUnlimitedModeActive && _activeObstacles.Count >= maxConcurrentObstacles)
            {
                Debug.LogWarning($"[SingleObstacleSpawner] Max concurrent obstacles reached ({maxConcurrentObstacles})! Dropped spawn request for Lane {laneIndex}.");
                return false;
            }

            // Check if target lane is already occupied
            foreach (var obs in _activeObstacles)
            {
                if (obs.LaneIndex == laneIndex && obs.IsActive)
                {
                    Debug.LogWarning($"[SingleObstacleSpawner] Lane {laneIndex} already has an active obstacle. Dropped duplicate spawn request.");
                    return false;
                }
            }

            float selectedLane = GetLaneOffsetZ(laneIndex);
            SpawnCarOnSelectedLane(laneIndex, selectedLane, tier);
            return true;
        }

        public bool TriggerSpawnCarFromAntiLikes(VehicleTier? tier = null)
        {
            if (_spawningLocked) return false;

            CleanupInactiveObstacles();

            if (!_isUnlimitedModeActive && _activeObstacles.Count >= maxConcurrentObstacles)
            {
                Debug.LogWarning($"[SingleObstacleSpawner] Max concurrent obstacles reached ({maxConcurrentObstacles})! Dropped Anti-Like spawn request.");
                return false;
            }

            List<int> availableLanes = new List<int>();
            for (int i = 1; i <= 3; i++)
            {
                if (CanSpawnObstacleOnLane(i))
                {
                    availableLanes.Add(i);
                }
            }

            if (availableLanes.Count == 0)
            {
                Debug.LogWarning("[SingleObstacleSpawner] No open lane available to spawn vehicle from Anti-Like.");
                return false;
            }

            int chosenLane = availableLanes[Random.Range(0, availableLanes.Count)];
            return TriggerSpawnCarOnLane(chosenLane, tier);
        }

        /// <summary>
        /// Activates Unlimited Cars mode, bypassing maxConcurrentObstacles limit.
        /// </summary>
        public void ActivateUnlimitedMode()
        {
            if (_unlimitedModeCoroutine != null)
            {
                StopCoroutine(_unlimitedModeCoroutine);
            }
            _unlimitedModeCoroutine = StartCoroutine(UnlimitedModeRoutine());
        }

        private IEnumerator UnlimitedModeRoutine()
        {
            _isUnlimitedModeActive = true;
            Debug.Log($"[SingleObstacleSpawner] Unlimited Mode activated for {unlimitedModeDuration}s.");
            AntiUnlimitedTimerCircle.Instance?.ActivateTimer(unlimitedModeDuration);
            AudioManager.Instance?.PlaySFX(SFXType.WarningSiren);

            yield return new WaitForSeconds(unlimitedModeDuration);

            _isUnlimitedModeActive = false;
            _unlimitedModeCoroutine = null;
            AntiUnlimitedTimerCircle.Instance?.DeactivateTimer();
            Debug.Log("[SingleObstacleSpawner] Unlimited Mode finished.");
        }

        // ============================================================
        // VEHICLE SPECIAL PHASE - PICKUP / HEAVY
        // ============================================================

        /// <summary>
        /// Activates Pickup Truck Phase. Future spawned vehicles during this phase spawn as Pickup.
        /// </summary>
        public void ActivatePickupTruckPhase()
        {
            ActivateVehiclePhase(VehicleTier.PickupTruck);
        }

        /// <summary>
        /// Activates Heavy Truck Phase. Future spawned vehicles during this phase spawn as Heavy Truck.
        /// </summary>
        public void ActivateHeavyTruckPhase()
        {
            ActivateVehiclePhase(VehicleTier.HeavyTruck);
        }

        /// <summary>
        /// Activates or transitions vehicle phase. Resets phase duration timer.
        /// </summary>
        private void ActivateVehiclePhase(VehicleTier phase)
        {
            if (_vehiclePhaseCoroutine != null)
            {
                StopCoroutine(_vehiclePhaseCoroutine);
                _vehiclePhaseCoroutine = null;
            }

            _activeVehiclePhase = phase;
            _vehiclePhaseRemainingTime = Mathf.Max(0f, VehiclePhaseDuration);

            _vehiclePhaseCoroutine = StartCoroutine(VehiclePhaseRoutine(phase));

            Debug.Log(
                $"[SingleObstacleSpawner] Vehicle Phase = {_activeVehiclePhase} " +
                $"trong {_vehiclePhaseRemainingTime:F0}s.");
        }

        /// <summary>
        /// Vehicle phase countdown coroutine. Returns to default Sedan upon completion.
        /// </summary>
        private IEnumerator VehiclePhaseRoutine(VehicleTier phase)
        {
            while (_activeVehiclePhase.HasValue &&
                   _activeVehiclePhase.Value == phase &&
                   _vehiclePhaseRemainingTime > 0f)
            {
                _vehiclePhaseRemainingTime -= Time.deltaTime;
                _vehiclePhaseRemainingTime = Mathf.Max(0f, _vehiclePhaseRemainingTime);

                yield return null;
            }

            if (_activeVehiclePhase.HasValue &&
                _activeVehiclePhase.Value == phase)
            {
                _activeVehiclePhase = null;
                _vehiclePhaseRemainingTime = 0f;
                _vehiclePhaseCoroutine = null;

                Debug.Log($"[SingleObstacleSpawner] Vehicle Phase {phase} ended. Returning to default Sedan.");
            }
        }

        // Debug toggle for Unlimited Cars mode
        /// <summary>
        /// [DEBUG] Toggles Unlimited Mode without time limit for QA testing.
        /// </summary>
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
            Debug.Log($"[SingleObstacleSpawner] [DEBUG] Unlimited Mode = {isActive} (test key 0).");
        }
        // ===== [Dhuy] END =====

        /// <summary>
        /// Checks whether the rain weather hazard / environment gift is currently active.
        /// </summary>
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

            // Fallback for isolated test environments without WeatherHazardManager
            var rainObj = GameObject.Find("FX_Rain");
            if (rainObj != null && rainObj.activeInHierarchy) return true;

            return false;
        }

        private void SpawnCarOnSelectedLane(int laneIndex, float selectedLane, VehicleTier? tier = null)
        {
            if (playerReference == null)
            {
                var runner = FindFirstObjectByType<ChatLaneRunnerController>();
                if (runner != null) playerReference = runner.transform;
            }

            if (playerReference == null)
            {
                Debug.LogWarning("[SingleObstacleSpawner] Missing Player reference.");
                return;
            }

            var obstacle = new ActiveObstacle
            {
                LaneIndex = laneIndex,
                LaneZ = selectedLane,
                LaserInstance = null,
                CarInstance = null,
                PlayerRef = playerReference,
                IsPendingSpawn = false
            };
            _activeObstacles.Add(obstacle);

            InstantiateCar(obstacle, selectedLane, tier);
        }

        /// <summary>
        /// Resolves obstacle vehicle tier based on active phase and request parameters.
        /// </summary>
        private VehicleTier GetVehicleTierForSpawn(VehicleTier? requestedTier)
        {
            if (requestedTier.HasValue)
            {
                return requestedTier.Value;
            }

            if (_activeVehiclePhase.HasValue)
            {
                return _activeVehiclePhase.Value;
            }

            return VehicleTier.SedanCar;
        }

        private GameObject InstantiateCar(ActiveObstacle obstacle, float selectedLane, VehicleTier? requestedTier)
        {
            if (playerReference == null)
            {
                var runner = FindFirstObjectByType<ChatLaneRunnerController>();
                if (runner != null) playerReference = runner.transform;
            }

            if (playerReference == null)
            {
                if (obstacle != null) obstacle.IsPendingSpawn = false;
                return null;
            }

            // Resolve tier at actual instantiation time
            VehicleTier chosenTier = GetVehicleTierForSpawn(requestedTier);

            // Instantiate vehicle at spawnDistanceAhead ahead of player
            Vector3 carSpawnPosition = new Vector3(
                playerReference.position.x + spawnDistanceAhead,
                0.05f,
                selectedLane);

            GameObject prefabToSpawn = GetCarPrefab(chosenTier);
            if (prefabToSpawn == null)
            {
                Debug.LogWarning("[SingleObstacleSpawner] No vehicle prefabs available to spawn!");
                if (obstacle != null) obstacle.IsPendingSpawn = false;
                return null;
            }

            // Rotate vehicle facing incoming runner (-X)
            Quaternion spawnRot = Quaternion.Euler(0f, -90f, 0f);

            GameObject carInstance = Instantiate(
                prefabToSpawn,
                carSpawnPosition,
                spawnRot);

            carInstance.name = $"ObstacleCar_{chosenTier}_{prefabToSpawn.name}";
            try { carInstance.tag = "Obstacle"; } catch { }

            Collider[] existingColliders = carInstance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < existingColliders.Length; i++)
            {
                if (existingColliders[i] != null)
                {
                    existingColliders[i].enabled = false;
                }
            }

            var box = carInstance.GetComponent<BoxCollider>();
            if (box == null) box = carInstance.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.enabled = true;

            switch (chosenTier)
            {
                case VehicleTier.HeavyTruck:
                    box.size = new Vector3(2.6f, 3.2f, 7.5f);
                    box.center = new Vector3(0f, 1.6f, 0f);
                    break;
                case VehicleTier.PickupTruck:
                    box.size = new Vector3(2.5f, 2.2f, 5.5f);
                    box.center = new Vector3(0f, 1.1f, 0f);
                    break;
                default: // SedanCar
                    box.size = new Vector3(2.2f, 1.6f, 4.8f);
                    box.center = new Vector3(0f, 0.8f, 0f);
                    break;
            }

            var rb = carInstance.GetComponent<Rigidbody>();
            if (rb == null) rb = carInstance.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (obstacle != null)
            {
                obstacle.IsPendingSpawn = false;
                obstacle.CarInstance = carInstance;
                obstacle.PlayerRef = playerReference;
            }

            InitializeCarMovement(carInstance, chosenTier);
            Debug.Log($"[SingleObstacleSpawner] Spawned [{chosenTier}] '{prefabToSpawn.name}' on lane Z={selectedLane:F1}, {spawnDistanceAhead}m ahead.");
            return carInstance;
        }

        public GameObject GetCarPrefab(VehicleTier tier)
        {
            List<GameObject> list = tier switch
            {
                VehicleTier.HeavyTruck => heavyTruckPrefabs,
                VehicleTier.PickupTruck => pickupTruckPrefabs,
                _ => sedanCarPrefabs
            };

            if (list != null && list.Count > 0)
            {
                var valid = list.FindAll(p => p != null);
                if (valid.Count > 0)
                {
                    return valid[Random.Range(0, valid.Count)];
                }
            }

            return GetCarPrefab();
        }

        private GameObject GetCarPrefab()
        {
            if (vehiclePrefabs != null && vehiclePrefabs.Count > 0)
            {
                var valid = vehiclePrefabs.FindAll(p => p != null);
                if (valid.Count > 0)
                {
                    return valid[Random.Range(0, valid.Count)];
                }
            }
            return urbanCarPrefab;
        }

        /// <summary>
        /// Activates direct gift actions on Runner/Fan team.
        /// </summary>
        public bool TriggerActivateFanGift(int laneIndex = 0, bool isShield = false)
        {
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                if (isShield)
                {
                    return StreamRushLive.Features.Gifts.GiftManager.Instance.ActivateShield("Phe Fan");
                }
                else
                {
                    StreamRushLive.Features.Gifts.GiftManager.Instance.AddBlueEnergy("Phe Fan");
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

        /// <summary>
        /// Backward compatibility alias.
        /// </summary>
        public bool TriggerSpawnFanItem(int laneIndex, bool isShield = false)
            => TriggerActivateFanGift(laneIndex, isShield);

        private void InitializeCarMovement(GameObject carInstance, VehicleTier tier = VehicleTier.SedanCar)
        {
            var drivingCar = carInstance.GetComponent<DrivingObstacleCar>();
            if (drivingCar == null)
            {
                drivingCar = carInstance.AddComponent<DrivingObstacleCar>();
            }

            drivingCar.ConfigureTier(tier);
            drivingCar.Initialize(worldSpeedManager, drivingCar.DrivingSpeed);
        }

        private void InitializeWorldMovement(GameObject instance)
        {
            MovingWorldObject movingObject = instance.GetComponent<MovingWorldObject>();
            if (movingObject == null)
            {
                movingObject = instance.AddComponent<MovingWorldObject>();
            }

            movingObject.Initialize(worldSpeedManager);
        }
    }
}
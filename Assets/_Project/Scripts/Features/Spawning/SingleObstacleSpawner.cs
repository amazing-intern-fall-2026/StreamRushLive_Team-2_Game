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

        [Tooltip("Danh sách các model xe PolygonCity (fallback chung).")]
        [SerializeField] private List<GameObject> vehiclePrefabs = new List<GameObject>();

        [Header("Categorized Tiered Vehicles (GDD v1.4)")]
        [Tooltip("Xe Con (Tier 1): Giảm 20% năng lượng, đẩy lùi 100m")]
        [SerializeField] private List<GameObject> sedanCarPrefabs = new List<GameObject>();

        [Tooltip("Xe Bán Tải (Tier 2): Giảm 40% năng lượng, đẩy lùi 200m")]
        [SerializeField] private List<GameObject> pickupTruckPrefabs = new List<GameObject>();

        [Tooltip("Xe Tải Hạng Nặng (Tier 3): Giảm 60% năng lượng, đẩy lùi 400m")]
        [SerializeField] private List<GameObject> heavyTruckPrefabs = new List<GameObject>();

        [Tooltip("Tốc độ xe tự chạy trên mặt đường (m/s) bổ sung vào tốc độ cuộn của thế giới. Mặc định = 6.5 m/s.")]
        [SerializeField] private float carDrivingSpeed = 6.5f;

        public float CarDrivingSpeed
        {
            get => carDrivingSpeed;
            set => carDrivingSpeed = Mathf.Max(0f, value);
        }

        [Header("Fan Item Settings")]
        [Tooltip("Prefab Khiên bảo hộ tặng cho Runner.")]
        [SerializeField] private GameObject shieldItemPrefab;

        [Tooltip("Prefab Năng lượng/Bình tăng tốc tặng cho Runner.")]
        [SerializeField] private GameObject energyBuffItemPrefab;

        [Tooltip("Prefab hiệu ứng tia laser đỏ nhấp nháy.")]
        [SerializeField] private GameObject laserIndicatorPrefab;

        [Tooltip("Tham chiếu vị trí Player để tính điểm spawn.")]
        [SerializeField] private Transform playerReference;

        [Tooltip("Quản lý tốc độ cuộn của thế giới; tự tìm nếu để trống.")]
        [SerializeField] private WorldSpeedManager worldSpeedManager;

        [Header("Lane Settings")]
        [Tooltip("Tọa độ Z của làn bên trái (+Z theo hướng nhìn camera).")]
        [SerializeField] private float laneOffsetLeft = 3.0f;

        [Tooltip("Tọa độ Z của làn giữa.")]
        [SerializeField] private float laneOffsetCenter = 0.0f;

        [Tooltip("Tọa độ Z của làn bên phải (-Z theo hướng nhìn camera).")]
        [SerializeField] private float laneOffsetRight = -3.0f;

        [Header("Spawn Distance & Warning")]
        [Tooltip("Khoảng cách X phía trước Player nơi xe xuất hiện.")]
        [SerializeField] private float spawnDistanceAhead = 35f;

        [Tooltip("Thời gian cảnh báo laser trước khi xe xuất hiện.")]
        [SerializeField] private float laserWarningDuration = 2.0f;

        [Tooltip("Khoảng thời gian bật/tắt laser.")]
        [SerializeField] private float laserBlinkInterval = 0.15f;

        [Header("Obstacle Concurrency Limit")]
        [Tooltip("Số lượng chướng ngại vật (laser cảnh báo + xe) tối đa cùng lúc trên đường chạy. Mặc định là 2 để luôn đảm bảo có ít nhất 1 làn trống cho Runner né.")]
        [SerializeField] private int maxConcurrentObstacles = 2;

        [Header("Unlimited Mode (Anti Faction Gift - F7)")]
        [Tooltip("Thời lượng chế độ thả xe không giới hạn khi kích hoạt (giây).")]
        [SerializeField] private float unlimitedModeDuration = 60f;

        private bool _isUnlimitedModeActive;
        private Coroutine _unlimitedModeCoroutine;

        public bool IsUnlimitedModeActive => _isUnlimitedModeActive;

        private class ActiveObstacle
        {
            public int LaneIndex;
            public float LaneZ;
            public GameObject LaserInstance;
            public GameObject CarInstance;
            public Transform PlayerRef;

            public bool IsActive
            {
                get
                {
                    if (LaserInstance != null) return true;
                    if (CarInstance != null)
                    {
                        // Nếu xe chưa vượt qua phía sau người chơi (hoặc cách sau người chơi dưới 1.5m) thì vẫn là vật cản đang cản đường
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
            if (laneIndex == 1) return laneOffsetLeft;       // +3.0f (Làn trái)
            if (laneIndex == 3) return laneOffsetRight;      // -3.0f (Làn phải)
            return laneOffsetCenter;                         //  0.0f (Làn giữa)
        }

        /// <summary>
        /// Sinh xe theo phân cấp cụ thể (GDD v1.4):
        /// - SedanCar (Xe Con Húc): -20% NL, -100m cự ly
        /// - PickupTruck (Xe Bán Tải): -40% NL, -200m cự ly
        /// - HeavyTruck (Xe Tải Hạng Nặng): -60% NL, -400m cự ly
        /// Nếu không chỉ định làn (laneIndex <= 0), tự động chọn ngẫu nhiên 1 làn hợp lệ.
        /// </summary>
        public bool TriggerSpawnCarTier(VehicleTier tier, int laneIndex = -1)
        {
            CleanupInactiveObstacles();

            if (!_isUnlimitedModeActive && _activeObstacles.Count >= maxConcurrentObstacles)
            {
                Debug.LogWarning($"[SingleObstacleSpawner] Đã đạt giới hạn tối đa {maxConcurrentObstacles} chướng ngại vật cùng lúc! Bỏ qua yêu cầu spawn {tier}.");
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
                    Debug.LogWarning($"[SingleObstacleSpawner] Không còn làn trống để spawn {tier}.");
                    return false;
                }
                targetLane = availableLanes[Random.Range(0, availableLanes.Count)];
            }
            else if (!CanSpawnObstacleOnLane(targetLane))
            {
                Debug.LogWarning($"[SingleObstacleSpawner] Làn {targetLane} hiện đang có vật cản! Bỏ qua spawn trùng làn.");
                return false;
            }

            float selectedLane = GetLaneOffsetZ(targetLane);
            SpawnCarOnSelectedLane(targetLane, selectedLane, tier);
            return true;
        }

        public bool TriggerSpawnCarOnLane(int laneIndex, VehicleTier? tier = null)
        {
            CleanupInactiveObstacles();

            if (!_isUnlimitedModeActive && _activeObstacles.Count >= maxConcurrentObstacles)
            {
                Debug.LogWarning($"[SingleObstacleSpawner] Đã đạt giới hạn tối đa {maxConcurrentObstacles} chướng ngại vật cùng lúc! Bỏ qua yêu cầu spawn Làn {laneIndex}.");
                return false;
            }

            // Kiểm tra làn đã có chướng ngại vật chưa
            foreach (var obs in _activeObstacles)
            {
                if (obs.LaneIndex == laneIndex && obs.IsActive)
                {
                    Debug.LogWarning($"[SingleObstacleSpawner] Làn {laneIndex} hiện đã có chướng ngại vật đang hoạt động. Bỏ qua yêu cầu spawn trùng làn.");
                    return false;
                }
            }

            float selectedLane = GetLaneOffsetZ(laneIndex);
            SpawnCarOnSelectedLane(laneIndex, selectedLane, tier);
            return true;
        }

        public bool TriggerSpawnCarFromAntiLikes(VehicleTier? tier = null)
        {
            CleanupInactiveObstacles();

            if (!_isUnlimitedModeActive && _activeObstacles.Count >= maxConcurrentObstacles)
            {
                Debug.LogWarning($"[SingleObstacleSpawner] Đã đạt giới hạn {maxConcurrentObstacles} chướng ngại vật cùng lúc! Bỏ qua yêu cầu từ Anti-Like.");
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
                Debug.LogWarning("[SingleObstacleSpawner] Không còn làn trống để spawn xe từ Anti-Like.");
                return false;
            }

            int chosenLane = availableLanes[Random.Range(0, availableLanes.Count)];
            return TriggerSpawnCarOnLane(chosenLane, tier);
        }

        /// <summary>
        /// Kích hoạt chế độ thả xe không giới hạn trong unlimitedModeDuration giây:
        /// bỏ qua giới hạn số xe tối đa cùng lúc (maxConcurrentObstacles).
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
            Debug.Log($"[SingleObstacleSpawner] Unlimited Mode kích hoạt trong {unlimitedModeDuration}s.");
            AntiUnlimitedTimerCircle.Instance?.ActivateTimer(unlimitedModeDuration);

            yield return new WaitForSeconds(unlimitedModeDuration);

            _isUnlimitedModeActive = false;
            _unlimitedModeCoroutine = null;
            AntiUnlimitedTimerCircle.Instance?.DeactivateTimer();
            Debug.Log("[SingleObstacleSpawner] Unlimited Mode kết thúc.");
        }

        // ===== [Dhuy] BEGIN - Debug phím "0": bật/tắt Unlimited Mode tự do để QA test,
        // KHÔNG dùng Coroutine 60s như ActivateUnlimitedMode() (dành cho gameplay F7 thật). =====
        /// <summary>
        /// [DEBUG] Bật/tắt Unlimited Mode ngay lập tức, không giới hạn thời gian.
        /// Dùng riêng cho phím tắt test (phím "0"), không phải luồng gameplay chính thức.
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
            Debug.Log($"[SingleObstacleSpawner] [DEBUG] Unlimited Mode = {isActive} (phím 0, không giới hạn thời gian).");
        }
        // ===== [Dhuy] END =====

        private void SpawnCarOnSelectedLane(int laneIndex, float selectedLane, VehicleTier? tier = null)
        {
            if (playerReference == null)
            {
                var runner = FindFirstObjectByType<ChatLaneRunnerController>();
                if (runner != null) playerReference = runner.transform;
            }

            if (playerReference == null || laserIndicatorPrefab == null)
            {
                Debug.LogWarning("[SingleObstacleSpawner] Thiếu Player reference hoặc prefab laser.");
                return;
            }

            VehicleTier chosenTier = tier ?? PickRandomTier();

            // Đặt laser cảnh báo trên làn được chọn phía trước Runner
            Vector3 warningPosition = new Vector3(
                playerReference.position.x + (spawnDistanceAhead * 0.5f),
                0.05f,
                selectedLane);

            GameObject laserInstance = Instantiate(
                laserIndicatorPrefab,
                warningPosition,
                Quaternion.identity);

            // Scale laser dài ra dọc theo trục chạy X để làm dải cảnh báo rõ ràng trên làn
            laserInstance.transform.localScale = new Vector3(spawnDistanceAhead, 0.05f, 2.2f);

            var obstacle = new ActiveObstacle
            {
                LaneIndex = laneIndex,
                LaneZ = selectedLane,
                LaserInstance = laserInstance,
                CarInstance = null,
                PlayerRef = playerReference
            };
            _activeObstacles.Add(obstacle);

            // Chạy cảnh báo nhấp nháy 3.5s trước khi sinh xe ở vị trí cách Runner 25m
            StartCoroutine(BlinkLaserThenSpawnCar(obstacle, laserInstance, selectedLane, chosenTier));
        }

        private VehicleTier PickRandomTier()
        {
            float roll = Random.value;
            if (roll < 0.50f) return VehicleTier.SedanCar;     // 50% Xe Con
            if (roll < 0.80f) return VehicleTier.PickupTruck;  // 30% Xe Bán Tải
            return VehicleTier.HeavyTruck;                     // 20% Xe Tải Nặng
        }

        private IEnumerator BlinkLaserThenSpawnCar(ActiveObstacle obstacle, GameObject laserInstance, float selectedLane, VehicleTier tier)
        {
            Renderer[] renderers = laserInstance != null ? laserInstance.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            float elapsed = 0f;
            bool isVisible = true;
            float blinkInterval = Mathf.Max(0.01f, laserBlinkInterval);

            // Nhấp nháy renderer trong đúng thời lượng cảnh báo 3.5s, bám vị trí trước mặt người chơi
            while (elapsed < laserWarningDuration)
            {
                if (laserInstance == null) yield break;

                if (playerReference != null)
                {
                    laserInstance.transform.position = new Vector3(
                        playerReference.position.x + (spawnDistanceAhead * 0.5f),
                        0.05f,
                        selectedLane);
                }

                SetRenderersEnabled(renderers, isVisible);
                isVisible = !isVisible;

                float waitTime = Mathf.Min(blinkInterval, laserWarningDuration - elapsed);
                yield return new WaitForSeconds(waitTime);
                elapsed += waitTime;
            }

            if (laserInstance != null)
            {
                Destroy(laserInstance);
            }

            if (obstacle != null)
            {
                obstacle.LaserInstance = null;
            }

            if (playerReference == null)
            {
                yield break;
            }

            // Sinh xe ở đúng khoảng cách spawnDistanceAhead (25m) phía trước mặt Player tại thời điểm cảnh báo kết thúc
            Vector3 carSpawnPosition = new Vector3(
                playerReference.position.x + spawnDistanceAhead,
                0.05f,
                selectedLane);

            GameObject prefabToSpawn = GetCarPrefab(tier);
            if (prefabToSpawn == null)
            {
                Debug.LogWarning("[SingleObstacleSpawner] Không tìm thấy prefab xe nào để sinh!");
                yield break;
            }

            // Xoay đầu xe hướng về phía Runner đang chạy tới (-X)
            Quaternion spawnRot = Quaternion.Euler(0f, -90f, 0f);

            GameObject carInstance = Instantiate(
                prefabToSpawn,
                carSpawnPosition,
                spawnRot);

            carInstance.name = $"ObstacleCar_{tier}_{prefabToSpawn.name}";
            try { carInstance.tag = "Obstacle"; } catch { }

            // 1. Tắt toàn bộ Collider con có sẵn trên prefab để tránh lỗi MeshCollider non-convex vượt giới hạn 256 polygon
            Collider[] existingColliders = carInstance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < existingColliders.Length; i++)
            {
                if (existingColliders[i] != null)
                {
                    existingColliders[i].enabled = false;
                }
            }

            // 2. Gán BoxCollider Trigger chuẩn trên root GameObject theo đúng kích thước phân cấp xe
            var box = carInstance.GetComponent<BoxCollider>();
            if (box == null) box = carInstance.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.enabled = true;

            switch (tier)
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

            // 3. Thêm Kinematic Rigidbody để tối ưu hóa chuyển động Trigger trong PhysX
            var rb = carInstance.GetComponent<Rigidbody>();
            if (rb == null) rb = carInstance.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (obstacle != null)
            {
                obstacle.CarInstance = carInstance;
                obstacle.PlayerRef = playerReference;
            }

            InitializeCarMovement(carInstance, tier);
            Debug.Log($"[SingleObstacleSpawner] Đã sinh [{tier}] '{prefabToSpawn.name}' trên làn Z={selectedLane:F1} cách Player {spawnDistanceAhead}m.");
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
        /// Sinh vật phẩm trợ giúp (Khiên / Bình Năng Lượng) cho Runner của Phe Fan trên làn chỉ định.
        /// </summary>
        public bool TriggerSpawnFanItem(int laneIndex, bool isShield = false)
        {
            if (playerReference == null)
            {
                var runner = FindFirstObjectByType<ChatLaneRunnerController>();
                if (runner != null) playerReference = runner.transform;
            }

            if (playerReference == null)
            {
                Debug.LogWarning("[SingleObstacleSpawner] Thiếu Player reference để spawn item cho Phe Fan.");
                return false;
            }

            float selectedLane = GetLaneOffsetZ(laneIndex);
            GameObject itemPrefab = isShield ? shieldItemPrefab : (energyBuffItemPrefab != null ? energyBuffItemPrefab : shieldItemPrefab);

            if (itemPrefab == null)
            {
                Debug.LogWarning("[SingleObstacleSpawner] Chưa gán prefab Item cho Phe Fan.");
                return false;
            }

            Vector3 spawnPos = new Vector3(
                playerReference.position.x + spawnDistanceAhead,
                0.8f,
                selectedLane);

            GameObject itemInstance = Instantiate(itemPrefab, spawnPos, itemPrefab.transform.rotation);
            itemInstance.name = $"FanItem_{(isShield ? "Shield" : "EnergyBuff")}_Lane{laneIndex}";

            InitializeWorldMovement(itemInstance);
            Debug.Log($"[SingleObstacleSpawner] Phe Fan đã thả {(isShield ? "Khiên" : "Bình Năng Lượng")} trên làn {laneIndex} (Z={selectedLane:F1}) cách Player {spawnDistanceAhead}m.");
            return true;
        }

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

        private static void SetRenderersEnabled(Renderer[] renderers, bool isEnabled)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = isEnabled;
            }
        }
    }
}
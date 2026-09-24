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

        [Tooltip("Danh sách các model xe PolygonCity (Sedan, Taxi, Police, Muscle, Van...). Bốc ngẫu nhiên mỗi lần Anti spawn xe.")]
        [SerializeField] private List<GameObject> vehiclePrefabs = new List<GameObject>();

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
        [SerializeField] private float spawnDistanceAhead = 25f;

        [Tooltip("Thời gian cảnh báo laser trước khi xe xuất hiện.")]
        [SerializeField] private float laserWarningDuration = 3.5f;

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

        public bool TriggerSpawnCarOnLane(int laneIndex)
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
            SpawnCarOnSelectedLane(laneIndex, selectedLane);
            return true;
        }

        public bool TriggerSpawnCarFromAntiLikes()
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
            return TriggerSpawnCarOnLane(chosenLane);
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
            ActiveEffectTimerUI.ShowTimer("unlimited_car", "THẢ XE KHÔNG GIỚI HẠN", unlimitedModeDuration, new Color(1f, 0.28f, 0.2f, 1f));
            AntiUnlimitedTimerCircle.Instance?.ActivateTimer(unlimitedModeDuration);

            yield return new WaitForSeconds(unlimitedModeDuration);

            _isUnlimitedModeActive = false;
            _unlimitedModeCoroutine = null;
            ActiveEffectTimerUI.CancelTimer("unlimited_car");
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
                ActiveEffectTimerUI.ShowTimer("unlimited_car", "THẢ XE KHÔNG GIỚI HẠN (DEBUG)", 999f, new Color(1f, 0.28f, 0.2f, 1f));
                AntiUnlimitedTimerCircle.Instance?.ActivateTimer(999f);
            }
            else
            {
                ActiveEffectTimerUI.CancelTimer("unlimited_car");
                AntiUnlimitedTimerCircle.Instance?.DeactivateTimer();
            }
            Debug.Log($"[SingleObstacleSpawner] [DEBUG] Unlimited Mode = {isActive} (phím 0, không giới hạn thời gian).");
        }
        // ===== [Dhuy] END =====

        private void SpawnCarOnSelectedLane(int laneIndex, float selectedLane)
        {
            if (playerReference == null)
            {
                var runner = FindFirstObjectByType<ChatLaneRunnerController>();
                if (runner != null) playerReference = runner.transform;
            }

            if (playerReference == null || laserIndicatorPrefab == null || urbanCarPrefab == null)
            {
                Debug.LogWarning("[SingleObstacleSpawner] Thiếu Player reference hoặc prefab laser/xe.");
                return;
            }

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
            StartCoroutine(BlinkLaserThenSpawnCar(obstacle, laserInstance, selectedLane));
        }

        private IEnumerator BlinkLaserThenSpawnCar(ActiveObstacle obstacle, GameObject laserInstance, float selectedLane)
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

            GameObject prefabToSpawn = GetCarPrefab();
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

            carInstance.name = $"ObstacleCar_{prefabToSpawn.name}";
            try { carInstance.tag = "Obstacle"; } catch { }

            // Gán tag "Obstacle" và chuyển toàn bộ collider con sang Trigger
            // để phát hiện va chạm mượt mà mà tuyệt đối không đẩy/kéo vật lý người chơi
            Collider[] colliders = carInstance.GetComponentsInChildren<Collider>();
            if (colliders == null || colliders.Length == 0)
            {
                var box = carInstance.AddComponent<BoxCollider>();
                box.size = new Vector3(2.2f, 1.6f, 4.5f);
                box.center = new Vector3(0f, 0.8f, 0f);
                box.isTrigger = true;
            }
            else
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] is MeshCollider meshCol)
                    {
                        meshCol.convex = true;
                    }
                    colliders[i].isTrigger = true;
                    try { colliders[i].gameObject.tag = "Obstacle"; } catch { }
                }
            }

            if (obstacle != null)
            {
                obstacle.CarInstance = carInstance;
                obstacle.PlayerRef = playerReference;
            }

            InitializeCarMovement(carInstance);
            Debug.Log($"[SingleObstacleSpawner] Đã sinh xe '{prefabToSpawn.name}' (Tốc độ tự lái: {carDrivingSpeed} m/s) trên làn Z={selectedLane:F1} cách Player {spawnDistanceAhead}m.");
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

        private void InitializeCarMovement(GameObject carInstance)
        {
            var drivingCar = carInstance.GetComponent<DrivingObstacleCar>();
            if (drivingCar == null)
            {
                drivingCar = carInstance.AddComponent<DrivingObstacleCar>();
            }

            drivingCar.Initialize(worldSpeedManager, carDrivingSpeed);
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
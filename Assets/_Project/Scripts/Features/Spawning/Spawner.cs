using System.Collections.Generic;
using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Chịu trách nhiệm tạo các chướng ngại vật (Obstacles) và vật phẩm (Items) trong game.
    /// Quản lý danh sách Prefab cho từng loại (spawn ngẫu nhiên trong danh sách cùng loại).
    /// Hỗ trợ cả 2 hệ thống enum riêng biệt: ObstacleType và ItemType.
    /// Giữ nguyên Rotation gốc của Prefab khi Instantiate.
    /// Hỗ trợ spawn tại vị trí truyền vào hoặc lấy trực tiếp tại vị trí của GameObject Spawner (hoặc SpawnPoint).
    /// Tự động gắn MovingWorldObject để vật thể trôi theo thế giới.
    /// </summary>
    // ================================================================
    // [DHUY] Bổ sung: Obstacle Queue (Safe Distance) — xem chi tiết ở
    // các block code có comment "// [DHUY - ADDED]" bên dưới.
    // Mục đích: đảm bảo khoảng cách tối thiểu 15m giữa 2 obstacle liên
    // tiếp được spawn, theo yêu cầu task "Spawner Safe Distance".
    // ================================================================
    public class Spawner : MonoBehaviour
    {
        [Header("Obstacle Prefabs (Lists - Random Spawn)")]
        [SerializeField] private List<GameObject> lowBarrierPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> highBarrierPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> stopSignPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> trafficLightPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> fallingHazardPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> bouncingBoulderPrefabs = new List<GameObject>();

        [Header("Item Prefabs (Lists - Random Spawn)")]
        [SerializeField] private List<GameObject> buffItemPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> shieldItemPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> highJumpItemPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> hyperDashItemPrefabs = new List<GameObject>();

        [Header("Spawn Position Settings")]
        [Tooltip("Spawn point transform. Uses this spawner if null.")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("Always spawn at spawnPoint position instead of passed Vector3.")]
        [SerializeField] private bool useGameObjectPosition = false;

        [Tooltip("Offset added to spawn position.")]
        [SerializeField] private Vector3 spawnOffset = Vector3.zero;

        [Header("Type Y-Offset Settings")]
        [Tooltip("Apply specific Y height per spawn type.")]
        [SerializeField] private bool useTypeYOffset = false;
        [SerializeField] private float lowBarrierY = 0.5f;
        [SerializeField] private float highBarrierY = 1.8f;
        [SerializeField] private float stopSignY = 0.17f;
        [SerializeField] private float trafficLightY = 0.17f;
        [SerializeField] private float fallingHazardY = 4.5f;
        [SerializeField] private float bouncingBoulderY = 0.5f;
        [SerializeField] private float buffItemY = 1.0f;
        [SerializeField] private float shieldItemY = 1.0f;
        [SerializeField] private float highJumpItemY = 1.0f;
        [SerializeField] private float hyperDashItemY = 1.0f;

        [Header("Roadside Z-Offset Settings")]
        [SerializeField] private float stopSignZ = 1.8f;
        [SerializeField] private float trafficLightZ = 1.8f;

        [Header("Settings")]
        [SerializeField] private WorldSpeedManager worldSpeedManager;

        // [DHUY - ADDED] ---- Bắt đầu: field phục vụ Obstacle Queue (Safe Distance) ----
        [Header("Obstacle Queue (Safe Distance) — Added by Dhuy")]
        [Tooltip("Khoảng cách tối thiểu (mét) giữa 2 obstacle liên tiếp được spawn.")]
        [SerializeField] private float minSafeDistance = 15f;

        // Hàng chờ obstacle: được nạp vào qua EnqueueObstacle()/EnqueueObstacles(),
        // lấy dần ra để spawn khi đã đủ khoảng cách an toàn với obstacle spawn trước đó.
        private readonly Queue<ObstacleType> _obstacleQueue = new Queue<ObstacleType>();

        // Tham chiếu obstacle vừa spawn gần nhất, dùng để đo khoảng cách mỗi frame.
        private Transform _lastSpawnedObstacle;
        // [DHUY - ADDED] ---- Kết thúc field ----

        public List<GameObject> LowBarrierPrefabs => lowBarrierPrefabs;
        public List<GameObject> HighBarrierPrefabs => highBarrierPrefabs;
        public List<GameObject> StopSignPrefabs => stopSignPrefabs;
        public List<GameObject> TrafficLightPrefabs => trafficLightPrefabs;
        public List<GameObject> FallingHazardPrefabs => fallingHazardPrefabs;
        public List<GameObject> BouncingBoulderPrefabs => bouncingBoulderPrefabs;
        public List<GameObject> BuffItemPrefabs => buffItemPrefabs;
        public List<GameObject> ShieldItemPrefabs => shieldItemPrefabs;
        public List<GameObject> HighJumpItemPrefabs => highJumpItemPrefabs;
        public List<GameObject> HyperDashItemPrefabs => hyperDashItemPrefabs;

        public Transform SpawnPoint
        {
            get => spawnPoint != null ? spawnPoint : transform;
            set => spawnPoint = value;
        }

        public bool UseGameObjectPosition
        {
            get => useGameObjectPosition;
            set => useGameObjectPosition = value;
        }

        public Vector3 SpawnOffset
        {
            get => spawnOffset;
            set => spawnOffset = value;
        }

        // [DHUY - ADDED] Số obstacle còn đang chờ trong hàng đợi (dùng để debug/hiển thị nếu cần).
        public int QueuedObstacleCount => _obstacleQueue.Count;

        private void Awake()
        {
            if (worldSpeedManager == null)
            {
                worldSpeedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
        }

        // [DHUY - ADDED] ---- Bắt đầu: Update() mới, file gốc chưa có hàm này ----
        // Mỗi frame kiểm tra hàng chờ, tự động spawn obstacle tiếp theo khi đủ khoảng cách an toàn.
        private void Update()
        {
            TryDequeueAndSpawn();
        }
        // [DHUY - ADDED] ---- Kết thúc Update() ----

        // ==========================================
        // [DHUY - ADDED] OBSTACLE QUEUE (SAFE DISTANCE)
        // Toàn bộ region này là code mới, phục vụ đúng yêu cầu task
        // "Spawner Safe Distance": đảm bảo 2 obstacle liên tiếp trên
        // đường cách nhau tối thiểu minSafeDistance (mặc định 15m).
        // ==========================================

        /// <summary>
        /// [DHUY - ADDED] Đưa 1 loại obstacle vào hàng chờ, sẽ được spawn khi đủ khoảng cách an toàn.
        /// Gọi hàm này thay vì gọi thẳng SpawnObstacle() nếu muốn áp dụng ràng buộc 15m.
        /// </summary>
        public void EnqueueObstacle(ObstacleType obstacleType)
        {
            _obstacleQueue.Enqueue(obstacleType);
        }

        /// <summary>
        /// [DHUY - ADDED] Đưa nhiều loại obstacle vào hàng chờ cùng lúc, theo đúng thứ tự sẽ được spawn.
        /// </summary>
        public void EnqueueObstacles(IEnumerable<ObstacleType> obstacleTypes)
        {
            foreach (ObstacleType type in obstacleTypes)
            {
                _obstacleQueue.Enqueue(type);
            }
        }

        /// <summary>
        /// [DHUY - ADDED] Logic chính: nếu hàng chờ còn obstacle, kiểm tra khoảng cách giữa
        /// SpawnPoint và obstacle spawn gần nhất — đủ minSafeDistance mới lấy obstacle tiếp
        /// theo trong Queue ra để spawn. Nếu obstacle trước đã bị Destroy (đi quá xa, despawn),
        /// _lastSpawnedObstacle sẽ null, coi như đã đủ xa, cho spawn ngay không cần chờ thêm.
        /// </summary>
        private void TryDequeueAndSpawn()
        {
            if (_obstacleQueue.Count == 0)
            {
                return;
            }

            float distanceSinceLast = -1f;

            if (_lastSpawnedObstacle != null)
            {
                distanceSinceLast = Vector3.Distance(SpawnPoint.position, _lastSpawnedObstacle.position);
                if (distanceSinceLast < minSafeDistance)
                {
                    return;
                }
            }

            ObstacleType nextType = _obstacleQueue.Dequeue();
            GameObject instance = SpawnObstacle(nextType);
            _lastSpawnedObstacle = instance != null ? instance.transform : null;
        }
        // [DHUY - ADDED] ---- Kết thúc region Obstacle Queue ----

        // ==========================================
        // SPAWN HỆ THỐNG OBSTACLE (VẬT CẢN)
        // ==========================================

        /// <summary>
        /// Spawn vật cản tại vị trí GameObject Spawner (hoặc SpawnPoint).
        /// </summary>
        public GameObject SpawnObstacle(ObstacleType obstacleType)
        {
            return SpawnObstacle(obstacleType, GetObstacleSpawnPosition(obstacleType));
        }

        /// <summary>
        /// Spawn vật cản tại vị trí chỉ định.
        /// </summary>
        public GameObject SpawnObstacle(ObstacleType obstacleType, Vector3 position)
        {
            if (useGameObjectPosition)
            {
                position = GetObstacleSpawnPosition(obstacleType);
            }
            else
            {
                // Dù truyền custom position, StopSign và TrafficLight vẫn bắt buộc ở lề đường Z = 1.8f
                if (obstacleType == ObstacleType.StopSign || obstacleType == ObstacleType.TrafficLight)
                {
                    position.z = (obstacleType == ObstacleType.StopSign)
                        ? (stopSignZ > 0.5f ? stopSignZ : 1.8f)
                        : (trafficLightZ > 0.5f ? trafficLightZ : 1.8f);
                    position.y = (obstacleType == ObstacleType.StopSign)
                        ? (stopSignY > 0f ? stopSignY : 0.17f)
                        : (trafficLightY > 0f ? trafficLightY : 0.17f);
                }
            }

            GameObject prefab = GetObstaclePrefab(obstacleType);
            if (prefab == null)
            {
                Debug.LogWarning($"[Spawner] Không có prefab nào trong danh sách Obstacle: {obstacleType}");
                return null;
            }

            // Xoay mặt trước hướng thẳng về camera (Y = 180)
            Quaternion rotation = prefab.transform.rotation;
            switch (obstacleType)
            {
                case ObstacleType.StopSign:
                    rotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case ObstacleType.TrafficLight:
                    rotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
            }

            GameObject instance = Instantiate(prefab, position, rotation);

            // Giữ nguyên Trigger cho các chướng ngại vật sử dụng vùng kích hoạt (TrafficLight, StopSign bên lề đường)
            bool preserveTriggers = obstacleType == ObstacleType.TrafficLight || obstacleType == ObstacleType.StopSign;
            if (!preserveTriggers)
            {
                SetCollidersTrigger(instance, isTrigger: false);
            }

            SetupWorldMovement(instance);
            return instance;
        }

        // ==========================================
        // SPAWN HỆ THỐNG ITEM (VẬT PHẨM)
        // ==========================================

        /// <summary>
        /// Spawn vật phẩm tại vị trí GameObject Spawner (hoặc SpawnPoint).
        /// </summary>
        public GameObject SpawnItem(ItemType itemType)
        {
            return SpawnItem(itemType, GetItemSpawnPosition(itemType));
        }

        /// <summary>
        /// Spawn vật phẩm tại vị trí chỉ định.
        /// </summary>
        public GameObject SpawnItem(ItemType itemType, Vector3 position)
        {
            if (useGameObjectPosition)
            {
                position = GetItemSpawnPosition(itemType);
            }

            GameObject prefab = GetItemPrefab(itemType);
            if (prefab == null)
            {
                Debug.LogWarning($"[Spawner] Không có prefab nào trong danh sách Item: {itemType}");
                return null;
            }

            GameObject instance = InstantiatePrefab(prefab, position);

            // Vật phẩm luôn là Trigger để Player chạy xuyên qua nhặt
            SetCollidersTrigger(instance, isTrigger: true);

            SetupWorldMovement(instance);
            return instance;
        }

        // ==========================================
        // TƯƠNG THÍCH NGƯỢC (SPAWNTYPE & HÀM TIỆN ÍCH)
        // ==========================================

        public GameObject Spawn(SpawnType spawnType)
        {
            switch (spawnType)
            {
                case SpawnType.LowBarrier:
                    return SpawnObstacle(ObstacleType.LowBarrier);
                case SpawnType.HighBarrier:
                    return SpawnObstacle(ObstacleType.HighBarrier);
                case SpawnType.BuffItem:
                    return SpawnItem(ItemType.EnergyBuff);
                default:
                    return null;
            }
        }

        public GameObject Spawn(SpawnType spawnType, Vector3 position)
        {
            switch (spawnType)
            {
                case SpawnType.LowBarrier:
                    return SpawnObstacle(ObstacleType.LowBarrier, position);
                case SpawnType.HighBarrier:
                    return SpawnObstacle(ObstacleType.HighBarrier, position);
                case SpawnType.BuffItem:
                    return SpawnItem(ItemType.EnergyBuff, position);
                default:
                    return null;
            }
        }

        public GameObject SpawnLowBarrier() => SpawnObstacle(ObstacleType.LowBarrier);
        public GameObject SpawnHighBarrier() => SpawnObstacle(ObstacleType.HighBarrier);
        public GameObject SpawnBuffItem() => SpawnItem(ItemType.EnergyBuff);

        public GameObject SpawnRandomObstacle()
        {
            ObstacleType randomType = (ObstacleType)Random.Range(
                0,
                System.Enum.GetValues(typeof(ObstacleType)).Length
            );

            return SpawnObstacle(randomType);
        }

        public GameObject SpawnRandomItem()
        {
            ItemType randomType = (ItemType)Random.Range(
                0,
                System.Enum.GetValues(typeof(ItemType)).Length
            );

            return SpawnItem(randomType);
        }

        // ==========================================
        // HELPER FUNCTIONS
        // ==========================================

        private GameObject InstantiatePrefab(GameObject prefab, Vector3 position)
        {
            Quaternion originalRotation = prefab.transform.rotation;
            return Instantiate(prefab, position, originalRotation);
        }

        private void SetCollidersTrigger(GameObject instance, bool isTrigger)
        {
            bool hasDynamicRb = instance.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic;
            Collider[] colliders = instance.GetComponentsInChildren<Collider>();

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] is MeshCollider meshCol)
                {
                    if (isTrigger || hasDynamicRb)
                    {
                        meshCol.convex = true;
                    }
                }

                colliders[i].isTrigger = isTrigger;
            }
        }

        private void SetupWorldMovement(GameObject instance)
        {
            MovingWorldObject mover = instance.GetComponent<MovingWorldObject>();

            if (mover == null)
            {
                mover = instance.AddComponent<MovingWorldObject>();
            }

            if (worldSpeedManager != null)
            {
                mover.Initialize(worldSpeedManager);
            }
        }

        public Vector3 GetObstacleSpawnPosition(ObstacleType type)
        {
            Transform point = spawnPoint != null ? spawnPoint : transform;
            Vector3 pos = point.position + spawnOffset;

            if (useTypeYOffset)
            {
                switch (type)
                {
                    case ObstacleType.LowBarrier:
                        pos.y = lowBarrierY;
                        break;

                    case ObstacleType.HighBarrier:
                        pos.y = highBarrierY;
                        break;
                    case ObstacleType.StopSign:
                        pos.y = stopSignY;
                        break;
                    case ObstacleType.TrafficLight:
                        pos.y = trafficLightY;
                        break;
                    case ObstacleType.FallingHazard:
                        pos.y = fallingHazardY;
                        break;
                    case ObstacleType.BouncingBoulder:
                        pos.y = bouncingBoulderY;
                        break;
                }
            }

            // StopSign và TrafficLight luôn luôn spawn bên lề đường (mép vỉa hè giáp đường chạy)
            switch (type)
            {
                case ObstacleType.StopSign:
                    pos.z = stopSignZ > 0.5f ? stopSignZ : 1.8f;
                    if (useTypeYOffset && stopSignY > 0f) pos.y = stopSignY;
                    else if (pos.y < 0.15f || !useTypeYOffset) pos.y = 0.17f;
                    break;
                case ObstacleType.TrafficLight:
                    pos.z = trafficLightZ > 0.5f ? trafficLightZ : 1.8f;
                    if (useTypeYOffset && trafficLightY > 0f) pos.y = trafficLightY;
                    else if (pos.y < 0.15f || !useTypeYOffset) pos.y = 0.17f;
                    break;
            }

            return pos;
        }

        public Vector3 GetItemSpawnPosition(ItemType type)
        {
            Transform point = spawnPoint != null ? spawnPoint : transform;
            Vector3 pos = point.position + spawnOffset;

            if (useTypeYOffset)
            {
                switch (type)
                {
                    case ItemType.EnergyBuff:
                        pos.y = buffItemY;
                        break;

                    case ItemType.Shield:
                        pos.y = shieldItemY;
                        break;

                    case ItemType.HighJump:
                        pos.y = highJumpItemY;
                        break;

                    case ItemType.HyperDash:
                        pos.y = hyperDashItemY;
                        break;
                }
            }

            return pos;
        }

        public GameObject GetObstaclePrefab(ObstacleType type)
        {
            switch (type)
            {
                case ObstacleType.LowBarrier:
                    return GetRandomPrefab(lowBarrierPrefabs);

                case ObstacleType.HighBarrier:
                    return GetRandomPrefab(highBarrierPrefabs);
                case ObstacleType.StopSign:
                    return GetRandomPrefab(stopSignPrefabs);
                case ObstacleType.TrafficLight:
                    return GetRandomPrefab(trafficLightPrefabs);
                case ObstacleType.FallingHazard:
                    return GetRandomPrefab(fallingHazardPrefabs);
                case ObstacleType.BouncingBoulder:
                    return GetRandomPrefab(bouncingBoulderPrefabs);
                default:
                    return null;
            }
        }

        public GameObject GetItemPrefab(ItemType type)
        {
            switch (type)
            {
                case ItemType.EnergyBuff:
                    return GetRandomPrefab(buffItemPrefabs);

                case ItemType.Shield:
                    return GetRandomPrefab(shieldItemPrefabs);

                case ItemType.HighJump:
                    return GetRandomPrefab(highJumpItemPrefabs);

                case ItemType.HyperDash:
                    return GetRandomPrefab(hyperDashItemPrefabs);

                default:
                    return null;
            }
        }

        private GameObject GetRandomPrefab(List<GameObject> prefabs)
        {
            if (prefabs == null || prefabs.Count == 0)
            {
                return null;
            }

            List<GameObject> validPrefabs = prefabs.FindAll(p => p != null);

            if (validPrefabs.Count == 0)
            {
                return null;
            }

            int randomIndex = Random.Range(0, validPrefabs.Count);
            return validPrefabs[randomIndex];
        }
    }
}
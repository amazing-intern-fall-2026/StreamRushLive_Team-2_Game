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
    public class Spawner : MonoBehaviour
    {
        [Header("Obstacle Prefabs (Lists - Random Spawn)")]
        [SerializeField] private List<GameObject> lowBarrierPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> highBarrierPrefabs = new List<GameObject>();

        [Header("Item Prefabs (Lists - Random Spawn)")]
        [SerializeField] private List<GameObject> buffItemPrefabs = new List<GameObject>();

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
        [SerializeField] private float buffItemY = 1.0f;

        [Header("Settings")]
        [SerializeField] private WorldSpeedManager worldSpeedManager;

        public List<GameObject> LowBarrierPrefabs => lowBarrierPrefabs;
        public List<GameObject> HighBarrierPrefabs => highBarrierPrefabs;
        public List<GameObject> BuffItemPrefabs => buffItemPrefabs;

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

        private void Awake()
        {
            if (worldSpeedManager == null)
            {
                worldSpeedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
        }

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

            GameObject prefab = GetObstaclePrefab(obstacleType);
            if (prefab == null)
            {
                Debug.LogWarning($"[Spawner] Không có prefab nào trong danh sách Obstacle: {obstacleType}");
                return null;
            }

            GameObject instance = InstantiatePrefab(prefab, position);

            // Vật cản là Solid Collider (không phải Trigger)
            SetCollidersTrigger(instance, isTrigger: false);

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
                }
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
using System.Collections.Generic;
using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Spawns obstacles and items in the game.
    /// Manages prefab lists for each category (random spawn within category).
    /// Preserves original prefab rotations and attaches MovingWorldObject.
    /// </summary>
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
        [SerializeField] private float stopSignY = 0.5f;
        [SerializeField] private float trafficLightY = 0.5f;
        [SerializeField] private float fallingHazardY = 4.5f;
        [SerializeField] private float bouncingBoulderY = 0.5f;
        [SerializeField] private float buffItemY = 1.0f;
        [SerializeField] private float shieldItemY = 1.0f;
        [SerializeField] private float highJumpItemY = 1.0f;
        [SerializeField] private float hyperDashItemY = 1.0f;

        [Header("Settings")]
        [SerializeField] private WorldSpeedManager worldSpeedManager;

        [Header("Obstacle Queue (Safe Distance)")]
        [Tooltip("Minimum safe distance (meters) between consecutive spawned obstacles.")]
        [SerializeField] private float minSafeDistance = 15f;

        // Obstacle queue: enqueued via EnqueueObstacle()/EnqueueObstacles(),
        // dequeued and spawned when minSafeDistance is reached.
        private readonly Queue<ObstacleType> _obstacleQueue = new Queue<ObstacleType>();

        // Reference to the most recently spawned obstacle, used to compute spacing.
        private Transform _lastSpawnedObstacle;

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

        // Remaining obstacles waiting in queue
        public int QueuedObstacleCount => _obstacleQueue.Count;

        private void Awake()
        {
            if (worldSpeedManager == null)
            {
                worldSpeedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
        }

        private void Update()
        {
            TryDequeueAndSpawn();
        }

        // Obstacle Queue (Safe Distance)

        /// <summary>
        /// Enqueues an obstacle type to be spawned once safe distance is reached.
        /// </summary>
        public void EnqueueObstacle(ObstacleType obstacleType)
        {
            _obstacleQueue.Enqueue(obstacleType);
        }

        /// <summary>
        /// Enqueues multiple obstacle types into the spawn queue.
        /// </summary>
        public void EnqueueObstacles(IEnumerable<ObstacleType> obstacleTypes)
        {
            foreach (ObstacleType type in obstacleTypes)
            {
                _obstacleQueue.Enqueue(type);
            }
        }

        /// <summary>
        /// Checks queue and spawns next obstacle when minSafeDistance has elapsed.
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

            Debug.Log($"[Spawner Queue] Spawned {nextType}, distanceSinceLast={distanceSinceLast:F2}m (min required: {minSafeDistance}m), remaining in queue: {_obstacleQueue.Count}");

            _lastSpawnedObstacle = instance != null ? instance.transform : null;
        }
        // End of Obstacle Queue region

        // Obstacle Spawning

        /// <summary>
        /// Spawns an obstacle at the Spawner GameObject position (or SpawnPoint).
        /// </summary>
        public GameObject SpawnObstacle(ObstacleType obstacleType)
        {
            return SpawnObstacle(obstacleType, GetObstacleSpawnPosition(obstacleType));
        }

        /// <summary>
        /// Spawns an obstacle at the specified position.
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
                Debug.LogWarning($"[Spawner] No prefab found in Obstacle list: {obstacleType}");
                return null;
            }

            GameObject instance = InstantiatePrefab(prefab, position);

            // Obstacles are solid colliders (non-trigger)
            SetCollidersTrigger(instance, isTrigger: false);

            SetupWorldMovement(instance);
            return instance;
        }

        // Item Spawning

        /// <summary>
        /// Spawns an item at the Spawner GameObject position (or SpawnPoint).
        /// </summary>
        public GameObject SpawnItem(ItemType itemType)
        {
            return SpawnItem(itemType, GetItemSpawnPosition(itemType));
        }

        /// <summary>
        /// Spawns an item at the specified position.
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
                Debug.LogWarning($"[Spawner] No prefab found in Item list: {itemType}");
                return null;
            }

            GameObject instance = InstantiatePrefab(prefab, position);

            // Items are triggers so player can collect them
            SetCollidersTrigger(instance, isTrigger: true);

            SetupWorldMovement(instance);
            return instance;
        }

        // Legacy SpawnType Compatibility

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

        // Helper Functions

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
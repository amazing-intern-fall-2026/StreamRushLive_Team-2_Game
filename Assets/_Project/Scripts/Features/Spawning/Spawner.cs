using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Chịu trách nhiệm tạo các obstacle và item trong game.
    /// Tự động gắn MovingWorldObject để vật thể trôi theo thế giới.
    /// </summary>
    public class Spawner : MonoBehaviour
    {
        [Header("Spawnable Prefabs")]
        [SerializeField] private GameObject lowBarrierPrefab;
        [SerializeField] private GameObject highBarrierPrefab;
        [SerializeField] private GameObject buffItemPrefab;

        [Header("Settings")]
        [SerializeField] private WorldSpeedManager worldSpeedManager;

        private void Awake()
        {
            if (worldSpeedManager == null)
            {
                worldSpeedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
        }

        /// <summary>
        /// Tạo object dựa trên loại được yêu cầu tại vị trí chỉ định.
        /// </summary>
        public GameObject Spawn(SpawnType spawnType, Vector3 position)
        {
            GameObject prefab = GetPrefab(spawnType);

            if (prefab == null)
            {
                Debug.LogWarning($"No prefab assigned for spawn type: {spawnType}");
                return null;
            }

            GameObject instance = Instantiate(prefab, position, Quaternion.identity);

            // Gắn component để vật thể di chuyển lùi cùng thế giới
            MovingWorldObject mover = instance.GetComponent<MovingWorldObject>();
            if (mover == null)
            {
                mover = instance.AddComponent<MovingWorldObject>();
            }

            if (worldSpeedManager != null)
            {
                mover.Initialize(worldSpeedManager);
            }

            return instance;
        }

        private GameObject GetPrefab(SpawnType spawnType)
        {
            switch (spawnType)
            {
                case SpawnType.LowBarrier:
                    return lowBarrierPrefab;

                case SpawnType.HighBarrier:
                    return highBarrierPrefab;

                case SpawnType.BuffItem:
                    return buffItemPrefab;

                default:
                    return null;
            }
        }
    }
}
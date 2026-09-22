using UnityEngine;

namespace SteamRush.Track
{
    public class BuildingSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject[] _buildingPrefabs;
        [SerializeField] private WorldSpeedManager _speedManager;

        [Header("Speed Mode Settings")]
        [Tooltip("Move buildings according to WorldSpeedManager if true.")]
        [SerializeField] private bool _useWorldSpeed = true;

        [SerializeField] private float _customSpeed = 5f;

        [Header("Spawn Settings")]
        [SerializeField] private float _despawnXThreshold = -15f;
        [SerializeField] private float _minSpawnInterval = 1f;
        [SerializeField] private float _maxSpawnInterval = 3f;

        private float _timeUntilNextSpawn;

        public bool UseWorldSpeed
        {
            get => _useWorldSpeed;
            set => _useWorldSpeed = value;
        }

        public float CustomSpeed
        {
            get => _customSpeed;
            set => _customSpeed = Mathf.Max(0f, value);
        }

        public float WorldSpeed => _speedManager != null ? _speedManager.CurrentSpeed : 0f;

        private void Start()
        {
            ScheduleNextSpawn();
        }

        private void Update()
        {
            _timeUntilNextSpawn -= Time.deltaTime;

            if (_timeUntilNextSpawn <= 0f)
            {
                SpawnRandomBuilding();
                ScheduleNextSpawn();
            }
        }

        private void ScheduleNextSpawn()
        {
            _timeUntilNextSpawn = Random.Range(_minSpawnInterval, _maxSpawnInterval);
        }

        private void SpawnRandomBuilding()
        {
            if (_buildingPrefabs == null || _buildingPrefabs.Length == 0)
            {
                Debug.LogWarning("BuildingSpawner: no building prefabs assigned.", this);
                return;
            }

            GameObject prefab = _buildingPrefabs[Random.Range(0, _buildingPrefabs.Length)];
            Vector3 spawnPosition = transform.position;

            GameObject instance = Instantiate(prefab, spawnPosition, prefab.transform.rotation);
            MovingBuilding movingBuilding = instance.GetComponent<MovingBuilding>();

            if (movingBuilding == null)
            {
                movingBuilding = instance.AddComponent<MovingBuilding>();
            }

            // Khởi tạo tòa nhà với tùy chọn dùng WorldSpeed hoặc CustomSpeed từ Spawner
            movingBuilding.Initialize(this, _despawnXThreshold, _useWorldSpeed, _customSpeed);
        }
    }
}
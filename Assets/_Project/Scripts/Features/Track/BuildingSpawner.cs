using UnityEngine;

namespace SteamRush.Track
{
    public class BuildingSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject[] _buildingPrefabs;
        [SerializeField] private WorldSpeedManager _speedManager;

        [SerializeField] private float _spawnXPosition = 40f;
        [SerializeField] private float _rowY = 0f;
        [SerializeField] private float _rowZ = 0f;
        [SerializeField] private float _despawnXThreshold = -15f;

        [SerializeField] private float _minSpawnInterval = 1f;
        [SerializeField] private float _maxSpawnInterval = 3f;

        private float _timeUntilNextSpawn;

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
            Vector3 spawnPosition = new Vector3(_spawnXPosition, _rowY, _rowZ);

            GameObject instance = Instantiate(prefab, spawnPosition, Quaternion.identity);
            MovingBuilding movingBuilding = instance.GetComponent<MovingBuilding>();

            if (movingBuilding == null)
            {
                movingBuilding = instance.AddComponent<MovingBuilding>();
            }

            movingBuilding.Initialize(this, _despawnXThreshold);
        }
    }
}
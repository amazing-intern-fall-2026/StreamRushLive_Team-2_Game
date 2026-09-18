using UnityEngine;

namespace SteamRush.Track
{
    /// <summary>
    /// Spawner tạo luồng giao thông hai chiều trên đại lộ hậu cảnh (Road Traffic).
    /// - Làn gần (Lane 1): Xe chạy cùng chiều với runner (+X).
    /// - Làn xa (Lane 2): Xe chạy ngược chiều với runner (-X).
    /// - Tích hợp kiểm tra khoảng trống (Spawn Clearance Check) chống spawn đè xe.
    /// - Tự động vô hiệu hóa Collider trên xe hậu cảnh để tránh xung đột vật lý với Runner.
    /// </summary>
    public class RoadTrafficSpawner : MonoBehaviour
    {
        [Header("Vehicle Prefabs")]
        [SerializeField] private GameObject[] _vehiclePrefabs;

        [Header("References")]
        [SerializeField] private WorldSpeedManager _speedManager;

        [Header("Lane 1 Settings (Làn gần - Chạy sang phải +X)")]
        [SerializeField] private float _lane1Z = 4.92f;
        [SerializeField] private float _lane1SpawnX = -25f;
        [Tooltip("Tốc độ tối thiểu của xe làn 1 (m/s). Runner chạy 10-18m/s nên xe cần chạy 24-30m/s để vượt lên tự nhiên.")]
        [SerializeField] private float _lane1MinSpeed = 24f;
        [SerializeField] private float _lane1MaxSpeed = 30f;
        [SerializeField] private float _lane1MinInterval = 3.5f;
        [SerializeField] private float _lane1MaxInterval = 6.5f;

        [Header("Lane 2 Settings (Làn xa - Chạy sang trái -X)")]
        [SerializeField] private float _lane2Z = 9.92f;
        [SerializeField] private float _lane2SpawnX = 85f;
        [Tooltip("Tốc độ của xe ngược chiều làn 2 (m/s).")]
        [SerializeField] private float _lane2MinSpeed = 15f;
        [SerializeField] private float _lane2MaxSpeed = 20f;
        [SerializeField] private float _lane2MinInterval = 3.5f;
        [SerializeField] private float _lane2MaxInterval = 6.5f;

        [Header("General Settings")]
        [SerializeField] private float _minDespawnX = -35f;
        [SerializeField] private float _maxDespawnX = 95f;
        [SerializeField] private float _spawnY = 0.05f;
        [SerializeField] private float _minSpawnClearance = 20f; // Khoảng cách trống tối thiểu để được phép spawn xe mới
        [SerializeField] private bool _prewarm = true;

        private float _lane1Timer = 0f;
        private float _lane2Timer = 0f;
        private float _nextLane1Interval = 3f;
        private float _nextLane2Interval = 3f;

        private void Awake()
        {
            if (_speedManager == null)
            {
                _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
        }

        private void Start()
        {
            _nextLane1Interval = Random.Range(_lane1MinInterval, _lane1MaxInterval);
            _nextLane2Interval = Random.Range(_lane2MinInterval, _lane2MaxInterval);

            if (_prewarm && _vehiclePrefabs != null && _vehiclePrefabs.Length > 0)
            {
                PrewarmTraffic();
            }
        }

        private void Update()
        {
            if (_vehiclePrefabs == null || _vehiclePrefabs.Length == 0) return;

            // Lane 1 Spawning (+X)
            _lane1Timer += Time.deltaTime;
            if (_lane1Timer >= _nextLane1Interval)
            {
                if (IsSpawnClear(_lane1SpawnX, _lane1Z, _minSpawnClearance))
                {
                    _lane1Timer = 0f;
                    _nextLane1Interval = Random.Range(_lane1MinInterval, _lane1MaxInterval);
                    SpawnVehicle(_lane1SpawnX, _lane1Z, 1f, Random.Range(_lane1MinSpeed, _lane1MaxSpeed));
                }
                else
                {
                    // Điểm spawn đang bị xe khác chiếm giữ, hoãn 0.5s rồi kiểm tra lại
                    _lane1Timer = _nextLane1Interval - 0.5f;
                }
            }

            // Lane 2 Spawning (-X)
            _lane2Timer += Time.deltaTime;
            if (_lane2Timer >= _nextLane2Interval)
            {
                if (IsSpawnClear(_lane2SpawnX, _lane2Z, _minSpawnClearance))
                {
                    _lane2Timer = 0f;
                    _nextLane2Interval = Random.Range(_lane2MinInterval, _lane2MaxInterval);
                    SpawnVehicle(_lane2SpawnX, _lane2Z, -1f, Random.Range(_lane2MinSpeed, _lane2MaxSpeed));
                }
                else
                {
                    // Điểm spawn đang bị xe khác chiếm giữ, hoãn 0.5s rồi kiểm tra lại
                    _lane2Timer = _nextLane2Interval - 0.5f;
                }
            }
        }

        /// <summary>
        /// Kiểm tra xem điểm spawn đã đủ khoảng cách an toàn với các xe đang chạy chưa.
        /// </summary>
        private bool IsSpawnClear(float spawnX, float laneZ, float clearance)
        {
            var active = MovingVehicle.ActiveVehicles;
            for (int i = 0; i < active.Count; i++)
            {
                var v = active[i];
                if (v == null) continue;

                // Cùng làn đường
                if (Mathf.Abs(v.transform.position.z - laneZ) < 1.8f)
                {
                    if (Mathf.Abs(v.transform.position.x - spawnX) < clearance)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void PrewarmTraffic()
        {
            // Rải trước xe với khoảng cách xa nhau (ít nhất 30m - 40m) để hoàn toàn không bị chồng lấn
            // Lane 1 (+X)
            SpawnVehicle(10f, _lane1Z, 1f, Random.Range(_lane1MinSpeed, _lane1MaxSpeed));
            SpawnVehicle(45f, _lane1Z, 1f, Random.Range(_lane1MinSpeed, _lane1MaxSpeed));

            // Lane 2 (-X)
            SpawnVehicle(25f, _lane2Z, -1f, Random.Range(_lane2MinSpeed, _lane2MaxSpeed));
            SpawnVehicle(68f, _lane2Z, -1f, Random.Range(_lane2MinSpeed, _lane2MaxSpeed));
        }

        private void SpawnVehicle(float x, float z, float direction, float speed)
        {
            if (_vehiclePrefabs == null || _vehiclePrefabs.Length == 0) return;

            int idx = Random.Range(0, _vehiclePrefabs.Length);
            GameObject prefab = _vehiclePrefabs[idx];
            if (prefab == null) return;

            Vector3 spawnPos = new Vector3(x, _spawnY, z);
            // Xe mặc định hướng mũi xe về +Z:
            // - Chạy sang phải (+X): xoay Y = 90 độ
            // - Chạy sang trái (-X): xoay Y = 270 độ (hoặc -90)
            Quaternion rot = Quaternion.Euler(0f, direction > 0f ? 90f : 270f, 0f);

            GameObject instance = Instantiate(prefab, spawnPos, rot);
            instance.transform.SetParent(transform, true);

            // Vô hiệu hóa Collider trên xe cảnh nền để tuyệt đối không va chạm với Runner
            Collider[] colliders = instance.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            MovingVehicle mover = instance.AddComponent<MovingVehicle>();
            mover.Initialize(_speedManager, speed, direction, _minDespawnX, _maxDespawnX);
        }
    }
}

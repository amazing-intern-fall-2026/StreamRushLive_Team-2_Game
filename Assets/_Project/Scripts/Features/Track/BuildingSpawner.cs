using UnityEngine;

namespace SteamRush.Track
{
    /// <summary>
    /// Spawner tạo cảnh quan thành phố (Background Buildings) cho Endless Runner.
    /// Hỗ trợ:
    /// - Parallax Scrolling đa tầng: Càng gần camera tốc độ bằng thế giới (1.0x), càng xa càng chậm (0.3x - 0.65x).
    /// - Distance-based Spacing: Tự động đo bề ngang tòa nhà để tạo khoảng cách chuẩn, không bao giờ bị đè chồng hình.
    /// - Prewarm on Start: Tự động rải sẵn các khối nhà phủ kín tầm nhìn ngay khi game bắt đầu.
    /// </summary>
    public class BuildingSpawner : MonoBehaviour
    {
        [Header("Building Prefabs")]
        [SerializeField] private GameObject[] _buildingPrefabs;

        [Header("References")]
        [SerializeField] private WorldSpeedManager _speedManager;

        [Header("Parallax & Speed Settings")]
        [Tooltip("Sử dụng vận tốc từ WorldSpeedManager (nhân với hệ số Parallax).")]
        [SerializeField] private bool _useWorldSpeed = true;

        [Tooltip("Hệ số Parallax: 1.0 = bằng tốc độ thế giới (ở gần), 0.65 = trung cảnh, 0.3 = viễn cảnh chân trời.")]
        [Range(0.05f, 2.0f)]
        [SerializeField] private float _parallaxMultiplier = 1.0f;

        [Tooltip("Vận tốc tùy chỉnh nếu không dùng WorldSpeed.")]
        [SerializeField] private float _customSpeed = 5f;

        [Header("Spacing & Layout Settings")]
        [Tooltip("Khoảng cách tối thiểu giữa 2 tòa nhà liên tiếp.")]
        [SerializeField] private float _spacingBetweenBuildings = 2.0f;

        [Tooltip("Tọa độ X khi vượt qua sẽ bị hủy (sau lưng camera).")]
        [SerializeField] private float _despawnXThreshold = -35f;

        [Tooltip("Góc quay bổ sung khi spawn tòa nhà.")]
        [SerializeField] private Vector3 _rotationOffset = Vector3.zero;

        [Tooltip("Ngẫu nhiên lật 180 độ trục Y để tạo độ đa dạng thị giác.")]
        [SerializeField] private bool _randomYFlip = false;

        [Header("Prewarm Settings")]
        [Tooltip("Tự động rải sẵn các tòa nhà phủ kín màn hình ngay khi bắt đầu game.")]
        [SerializeField] private bool _prewarmOnStart = true;

        [Tooltip("Tọa độ X bắt đầu rải trước khi vào game.")]
        [SerializeField] private float _prewarmStartX = -30f;

        private Transform _lastSpawnedBuilding;
        private float _lastBuildingWidth = 10f;
        private int _nextPrefabIndex = 0;
        private float _nextBuildingWidth = 10f;

        public bool UseWorldSpeed
        {
            get => _useWorldSpeed;
            set => _useWorldSpeed = value;
        }

        public float ParallaxMultiplier
        {
            get => _parallaxMultiplier;
            set => _parallaxMultiplier = Mathf.Max(0.01f, value);
        }

        public float CustomSpeed
        {
            get => _customSpeed;
            set => _customSpeed = Mathf.Max(0f, value);
        }

        public float WorldSpeed => _speedManager != null ? _speedManager.CurrentSpeed : 0f;
        public GameObject[] BuildingPrefabs
        {
            get => _buildingPrefabs;
            set => _buildingPrefabs = value;
        }

        private void Awake()
        {
            if (_speedManager == null)
            {
                _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
        }

        private void Start()
        {
            if (_prewarmOnStart && _buildingPrefabs != null && _buildingPrefabs.Length > 0)
            {
                PrewarmBuildings();
            }
            PrepareNextBuilding();
        }

        private void Update()
        {
            if (_buildingPrefabs == null || _buildingPrefabs.Length == 0) return;

            // Nếu chưa có tòa nhà nào hoặc tòa nhà trước đã đi xa đủ khoảng cách yêu cầu
            if (_lastSpawnedBuilding == null)
            {
                SpawnNextBuilding(transform.position.x);
            }
            else
            {
                float distMoved = transform.position.x - _lastSpawnedBuilding.position.x;
                float requiredDist = (_lastBuildingWidth * 0.5f) + _spacingBetweenBuildings + (_nextBuildingWidth * 0.5f);

                if (distMoved >= requiredDist)
                {
                    // Spawn tại vị trí chuẩn xác nối tiếp để đảm bảo khoảng cách đều đặn
                    float spawnX = _lastSpawnedBuilding.position.x + requiredDist;
                    if (spawnX < transform.position.x - 5f)
                    {
                        spawnX = transform.position.x;
                    }
                    SpawnNextBuilding(spawnX);
                }
            }
        }

        /// <summary>
        /// Rải trước các tòa nhà phủ kín màn hình từ _prewarmStartX tới transform.position.x.
        /// </summary>
        private void PrewarmBuildings()
        {
            float currentX = _prewarmStartX;
            float targetEndX = transform.position.x;

            while (currentX < targetEndX)
            {
                int prefabIdx = Random.Range(0, _buildingPrefabs.Length);
                GameObject prefab = _buildingPrefabs[prefabIdx];
                if (prefab == null) continue;

                float width = GetPrefabWidth(prefab);
                currentX += (width * 0.5f);

                if (currentX <= targetEndX + 10f)
                {
                    Vector3 spawnPos = new Vector3(currentX, transform.position.y, transform.position.z);
                    GameObject instance = CreateBuildingInstance(prefab, spawnPos);
                    _lastSpawnedBuilding = instance.transform;
                    _lastBuildingWidth = width;
                }

                currentX += (width * 0.5f) + _spacingBetweenBuildings;
            }
        }

        private void PrepareNextBuilding()
        {
            if (_buildingPrefabs == null || _buildingPrefabs.Length == 0) return;
            _nextPrefabIndex = Random.Range(0, _buildingPrefabs.Length);
            GameObject prefab = _buildingPrefabs[_nextPrefabIndex];
            _nextBuildingWidth = prefab != null ? GetPrefabWidth(prefab) : 10f;
        }

        private void SpawnNextBuilding(float spawnX)
        {
            if (_buildingPrefabs == null || _buildingPrefabs.Length == 0) return;

            GameObject prefab = _buildingPrefabs[_nextPrefabIndex];
            if (prefab == null)
            {
                PrepareNextBuilding();
                return;
            }

            Vector3 spawnPos = new Vector3(spawnX, transform.position.y, transform.position.z);
            GameObject instance = CreateBuildingInstance(prefab, spawnPos);

            _lastSpawnedBuilding = instance.transform;
            _lastBuildingWidth = _nextBuildingWidth;

            PrepareNextBuilding();
        }

        private GameObject CreateBuildingInstance(GameObject prefab, Vector3 position)
        {
            Quaternion rot = prefab.transform.rotation * Quaternion.Euler(_rotationOffset);
            if (_randomYFlip && Random.value > 0.5f)
            {
                rot *= Quaternion.Euler(0f, 180f, 0f);
            }

            GameObject instance = Instantiate(prefab, position, rot);
            instance.transform.SetParent(transform, true);

            MovingBuilding mover = instance.GetComponent<MovingBuilding>();
            if (mover == null)
            {
                mover = instance.AddComponent<MovingBuilding>();
            }

            mover.Initialize(this, _despawnXThreshold, _useWorldSpeed, _customSpeed, _parallaxMultiplier);
            return instance;
        }

        private float GetPrefabWidth(GameObject prefab)
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                if (renderers.Length == 1)
                {
                    return renderers[0].bounds.size.x;
                }

                Bounds combined = new Bounds();
                bool hasBounds = false;
                foreach (var r in renderers)
                {
                    Vector3 localCenter = prefab.transform.InverseTransformPoint(r.bounds.center);
                    Bounds localB = new Bounds(localCenter, r.bounds.size);
                    if (!hasBounds)
                    {
                        combined = localB;
                        hasBounds = true;
                    }
                    else
                    {
                        combined.Encapsulate(localB);
                    }
                }

                if (hasBounds && combined.size.x > 1f)
                {
                    return combined.size.x;
                }
            }

            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>();
            if (filters != null && filters.Length > 0)
            {
                float maxX = 0f;
                foreach (var mf in filters)
                {
                    if (mf.sharedMesh != null)
                    {
                        float w = mf.sharedMesh.bounds.size.x * Mathf.Abs(mf.transform.localScale.x);
                        if (w > maxX) maxX = w;
                    }
                }
                if (maxX > 1f) return maxX;
            }

            return 10f;
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace SteamRush.Track
{
    /// <summary>
    /// Spawner tạo cảnh quan thành phố (Background Buildings) cho Endless Runner.
    /// Hỗ trợ:
    /// - Parallax Scrolling đa tầng: Càng gần camera tốc độ bằng thế giới (1.0x), càng xa càng chậm (0.3x - 0.65x).
    /// - Distance-based Spacing: Tự động đo bề ngang tòa nhà để tạo khoảng cách chuẩn, không bao giờ bị đè chồng hình.
    /// - Prewarm on Start: Tự động rải sẵn các khối nhà phủ kín tầm nhìn ngay khi game bắt đầu.
    /// - Bi-directional Continuous Coverage: Tự động spawn bù cả bên phải (khi chạy tới) và bên trái (khi bị knockback lùi lại),
    ///   đảm bảo chân trời thành phố luôn kín đặc 100%, không bao giờ lộ khoảng trống xám vô tận.
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

        [Tooltip("Góc quay bổ sung khi spawn tòa nhà.")]
        [SerializeField] private Vector3 _rotationOffset = Vector3.zero;

        [Tooltip("Ngẫu nhiên lật 180 độ trục Y để tạo độ đa dạng thị giác.")]
        [SerializeField] private bool _randomYFlip = false;

        [Header("Coverage & Bounds Settings")]
        [Tooltip("Tọa độ X bên trái luôn cần có nhà phủ kín (đảm bảo khi bị lùi knockback không bao giờ hở khoảng trống).")]
        [SerializeField] private float _leftCoverageX = -95f;

        [Tooltip("Tọa độ X bên phải cần phủ kín nhà.")]
        [SerializeField] private float _rightCoverageX = 110f;

        [Tooltip("Tọa độ X bên trái khi vượt qua sẽ bị hủy.")]
        [SerializeField] private float _despawnLeftX = -130f;

        [Tooltip("Tọa độ X bên phải khi vượt qua (khi lùi quá xa) sẽ bị hủy.")]
        [SerializeField] private float _despawnRightX = 160f;

        [Header("Prewarm Settings")]
        [Tooltip("Tự động rải sẵn các tòa nhà phủ kín màn hình ngay khi bắt đầu game.")]
        [SerializeField] private bool _prewarmOnStart = true;

        private readonly List<MovingBuilding> _activeBuildings = new List<MovingBuilding>();

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

            // Đảm bảo biên phủ an toàn tuyệt đối, tránh scene cũ lưu giá trị hẹp
            if (_leftCoverageX > -80f) _leftCoverageX = -95f;
            if (_rightCoverageX < 100f) _rightCoverageX = Mathf.Max(transform.position.x + 15f, 110f);
            if (_despawnLeftX > _leftCoverageX - 20f) _despawnLeftX = _leftCoverageX - 30f;
            if (_despawnRightX < _rightCoverageX + 20f) _despawnRightX = _rightCoverageX + 30f;
        }

        private void Start()
        {
            if (_prewarmOnStart && _buildingPrefabs != null && _buildingPrefabs.Length > 0)
            {
                PrewarmBuildings();
            }
        }

        private void Update()
        {
            if (_buildingPrefabs == null || _buildingPrefabs.Length == 0) return;

            // Dọn dẹp các tòa nhà đã bị Destroy (vượt quá despawn threshold)
            _activeBuildings.RemoveAll(b => b == null);

            if (_activeBuildings.Count == 0)
            {
                PrewarmBuildings();
                return;
            }

            // Tìm tòa nhà ngoài cùng bên trái và ngoài cùng bên phải
            MovingBuilding leftmost = _activeBuildings[0];
            MovingBuilding rightmost = _activeBuildings[0];
            float minX = leftmost.transform.position.x;
            float maxX = rightmost.transform.position.x;

            for (int i = 1; i < _activeBuildings.Count; i++)
            {
                MovingBuilding b = _activeBuildings[i];
                float x = b.transform.position.x;
                if (x < minX)
                {
                    minX = x;
                    leftmost = b;
                }
                if (x > maxX)
                {
                    maxX = x;
                    rightmost = b;
                }
            }

            // 1. Phủ bên phải khi chạy tới bình thường (hoặc khi cạnh phải hụt vào trong)
            float rightEdge = rightmost.transform.position.x + (rightmost.BuildingWidth * 0.5f) + _spacingBetweenBuildings;
            int rightSafetyLimit = 15;
            while (rightEdge < _rightCoverageX && rightSafetyLimit-- > 0)
            {
                GameObject prefab = GetRandomPrefab();
                if (prefab == null) break;

                float newWidth = GetPrefabWidth(prefab);
                float spawnX = rightEdge + (newWidth * 0.5f);
                Vector3 spawnPos = new Vector3(spawnX, transform.position.y, transform.position.z);

                MovingBuilding newBuilding = CreateBuildingInstance(prefab, spawnPos, newWidth);
                _activeBuildings.Add(newBuilding);
                rightmost = newBuilding;
                rightEdge = spawnX + (newWidth * 0.5f) + _spacingBetweenBuildings;
            }

            // 2. Phủ bên trái khi bị đẩy lùi / Reverse Knockback (hoặc khi cạnh trái hụt vào trong)
            float leftEdge = leftmost.transform.position.x - (leftmost.BuildingWidth * 0.5f) - _spacingBetweenBuildings;
            int leftSafetyLimit = 15;
            while (leftEdge > _leftCoverageX && leftSafetyLimit-- > 0)
            {
                GameObject prefab = GetRandomPrefab();
                if (prefab == null) break;

                float newWidth = GetPrefabWidth(prefab);
                float spawnX = leftEdge - (newWidth * 0.5f);
                Vector3 spawnPos = new Vector3(spawnX, transform.position.y, transform.position.z);

                MovingBuilding newBuilding = CreateBuildingInstance(prefab, spawnPos, newWidth);
                _activeBuildings.Add(newBuilding);
                leftmost = newBuilding;
                leftEdge = spawnX - (newWidth * 0.5f) - _spacingBetweenBuildings;
            }
        }

        /// <summary>
        /// Rải trước các tòa nhà phủ kín toàn bộ dải nhìn từ _leftCoverageX đến _rightCoverageX.
        /// </summary>
        private void PrewarmBuildings()
        {
            float currentX = _leftCoverageX;

            while (currentX < _rightCoverageX)
            {
                GameObject prefab = GetRandomPrefab();
                if (prefab == null) break;

                float width = GetPrefabWidth(prefab);
                float spawnX = currentX + (width * 0.5f);

                if (spawnX <= _rightCoverageX + 15f)
                {
                    Vector3 spawnPos = new Vector3(spawnX, transform.position.y, transform.position.z);
                    MovingBuilding mover = CreateBuildingInstance(prefab, spawnPos, width);
                    _activeBuildings.Add(mover);
                }

                currentX += width + _spacingBetweenBuildings;
            }
        }

        private GameObject GetRandomPrefab()
        {
            if (_buildingPrefabs == null || _buildingPrefabs.Length == 0) return null;
            return _buildingPrefabs[Random.Range(0, _buildingPrefabs.Length)];
        }

        private MovingBuilding CreateBuildingInstance(GameObject prefab, Vector3 position, float width)
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

            mover.BuildingWidth = width;
            mover.Initialize(this, _despawnLeftX, _despawnRightX, _useWorldSpeed, _customSpeed, _parallaxMultiplier);
            return mover;
        }

        private float GetPrefabWidth(GameObject prefab)
        {
            if (prefab == null) return 10f;

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                if (renderers.Length == 1)
                {
                    return Mathf.Max(2f, renderers[0].bounds.size.x);
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
                    return Mathf.Max(2f, combined.size.x);
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
                if (maxX > 1f) return Mathf.Max(2f, maxX);
            }

            return 10f;
        }
    }
}
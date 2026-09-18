using UnityEngine;

namespace SteamRush.Track
{
    public class MovingBuilding : MonoBehaviour
    {
        [Header("Speed & Parallax Mode")]
        [Tooltip("Use global speed from WorldSpeedManager if true, otherwise custom speed.")]
        [SerializeField] private bool _useWorldSpeed = true;

        [Tooltip("Hệ số nhân tốc độ parallax: 1.0 = bằng tốc độ thế giới (ở gần), 0.3 = chậm hơn (ở xa).")]
        [SerializeField] private float _parallaxMultiplier = 1.0f;

        [SerializeField] private float _customSpeed = 5f;

        private BuildingSpawner _spawner;
        private float _despawnXThreshold = -15f;

        public bool UseWorldSpeedMode => _useWorldSpeed;
        public float CustomSpeed => _customSpeed;
        public float ParallaxMultiplier
        {
            get => _parallaxMultiplier;
            set => _parallaxMultiplier = Mathf.Max(0f, value);
        }

        public void Initialize(BuildingSpawner spawner, float despawnXThreshold, bool useWorldSpeed = true, float customSpeed = 5f, float parallaxMultiplier = 1.0f)
        {
            _spawner = spawner;
            _despawnXThreshold = despawnXThreshold;
            _useWorldSpeed = useWorldSpeed;
            _customSpeed = customSpeed;
            _parallaxMultiplier = parallaxMultiplier;
        }

        /// <summary>
        /// Hàm xử lý gán tốc độ di chuyển riêng cho tòa nhà (chuyển sang chế độ Custom Speed).
        /// </summary>
        public void SetCustomSpeed(float speed)
        {
            _customSpeed = Mathf.Max(0f, speed);
            _useWorldSpeed = false;
        }

        /// <summary>
        /// Chuyển về chế độ sử dụng tốc độ chung của thế giới (WorldSpeed).
        /// </summary>
        public void EnableWorldSpeed()
        {
            _useWorldSpeed = true;
        }

        /// <summary>
        /// Hàm xử lý và tính toán tốc độ hiện tại:
        /// - Nếu dùng WorldSpeed: lấy từ Spawner (WorldSpeedManager) nhân với hệ số Parallax
        /// - Nếu không: lấy CustomSpeed riêng
        /// </summary>
        public float GetCurrentSpeed()
        {
            if (_useWorldSpeed && _spawner != null)
            {
                return _spawner.WorldSpeed * _parallaxMultiplier;
            }

            return _customSpeed;
        }

        private void Update()
        {
            float speed = GetCurrentSpeed();
            float step = speed * Time.deltaTime;
            transform.position += Vector3.left * step;

            if (transform.position.x <= _despawnXThreshold)
            {
                Destroy(gameObject);
            }
        }
    }
}
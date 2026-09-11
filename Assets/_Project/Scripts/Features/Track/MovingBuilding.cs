using UnityEngine;

namespace SteamRush.Track
{
    public class MovingBuilding : MonoBehaviour
    {
        [Header("Speed Mode")]
        [Tooltip("Bật để sử dụng tốc độ chung từ WorldSpeedManager, tắt để dùng tốc độ riêng.")]
        [SerializeField] private bool _useWorldSpeed = true;

        [Tooltip("Tốc độ di chuyển riêng của tòa nhà này.")]
        [SerializeField] private float _customSpeed = 5f;

        private BuildingSpawner _spawner;
        private float _despawnXThreshold = -15f;

        public bool UseWorldSpeedMode => _useWorldSpeed;
        public float CustomSpeed => _customSpeed;

        public void Initialize(BuildingSpawner spawner, float despawnXThreshold, bool useWorldSpeed = true, float customSpeed = 5f)
        {
            _spawner = spawner;
            _despawnXThreshold = despawnXThreshold;
            _useWorldSpeed = useWorldSpeed;
            _customSpeed = customSpeed;
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
        /// - Nếu dùng WorldSpeed: lấy từ Spawner (WorldSpeedManager)
        /// - Nếu không: lấy CustomSpeed riêng
        /// </summary>
        public float GetCurrentSpeed()
        {
            if (_useWorldSpeed && _spawner != null)
            {
                return _spawner.WorldSpeed;
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
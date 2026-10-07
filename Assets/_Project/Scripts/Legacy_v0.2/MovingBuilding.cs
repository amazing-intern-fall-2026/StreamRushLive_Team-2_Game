using UnityEngine;

namespace SteamRush.Track
{
    public class MovingBuilding : MonoBehaviour
    {
        [Header("Speed Mode")]
        [Tooltip("Use global speed from WorldSpeedManager if true, otherwise custom speed.")]
        [SerializeField] private bool _useWorldSpeed = true;

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
        /// Assigns custom movement speed to building (switches to Custom Speed mode).
        /// </summary>
        public void SetCustomSpeed(float speed)
        {
            _customSpeed = Mathf.Max(0f, speed);
            _useWorldSpeed = false;
        }

        /// <summary>
        /// Switches to global WorldSpeed mode.
        /// </summary>
        public void EnableWorldSpeed()
        {
            _useWorldSpeed = true;
        }

        /// <summary>
        /// Calculates current speed based on active mode (WorldSpeed vs CustomSpeed).
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
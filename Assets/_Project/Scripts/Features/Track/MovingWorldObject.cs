using UnityEngine;

namespace SteamRush.Track
{
    /// <summary>
    /// Gắn vào bất kỳ vật thể nào di chuyển cùng thế giới (chướng ngại vật, buff item...)
    /// để trôi ngược về phía Player theo trục -X với tốc độ từ WorldSpeedManager.
    /// </summary>
    public class MovingWorldObject : MonoBehaviour
    {
        [SerializeField] private float _despawnXThreshold = -15f;

        private WorldSpeedManager _speedManager;

        public void Initialize(WorldSpeedManager speedManager, float despawnXThreshold = -15f)
        {
            _speedManager = speedManager;
            _despawnXThreshold = despawnXThreshold;
        }

        private void Start()
        {
            if (_speedManager == null)
            {
                _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
        }

        private void Update()
        {
            float speed = _speedManager != null ? _speedManager.CurrentSpeed : 0f;
            float step = speed * Time.deltaTime;
            transform.position += Vector3.left * step;

            if (transform.position.x <= _despawnXThreshold)
            {
                Destroy(gameObject);
            }
        }
    }
}

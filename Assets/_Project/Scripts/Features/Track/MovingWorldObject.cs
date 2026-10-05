using UnityEngine;

namespace SteamRush.Track
{
    /// <summary>
    /// Attached to objects that move with the world (obstacles, items, etc.)
    /// to scroll along -X based on WorldSpeedManager speed.
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

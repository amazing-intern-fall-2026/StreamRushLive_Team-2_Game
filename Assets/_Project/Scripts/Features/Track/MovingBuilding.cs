using UnityEngine;

namespace SteamRush.Track
{
    public class MovingBuilding : MonoBehaviour
    {
        private BuildingSpawner _spawner;
        private float _despawnXThreshold;

        public void Initialize(BuildingSpawner spawner, float despawnXThreshold)
        {
            _spawner = spawner;
            _despawnXThreshold = despawnXThreshold;
        }

        private void Update()
        {
            if (_spawner == null)
            {
                return;
            }

            float step = _spawner.WorldSpeed * Time.deltaTime;
            transform.position += Vector3.left * step;

            if (transform.position.x <= _despawnXThreshold)
            {
                Destroy(gameObject);
            }
        }
    }
}
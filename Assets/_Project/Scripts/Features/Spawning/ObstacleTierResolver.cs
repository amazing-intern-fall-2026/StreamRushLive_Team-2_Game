using System.Collections.Generic;
using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Single Responsibility: Resolves obstacle prefabs based on tier (Sedan, Animals, Trains)
    /// and configures physics colliders / triggers on newly instantiated vehicles.
    /// </summary>
    public class ObstacleTierResolver
    {
        private GameObject _urbanCarPrefab;
        private List<GameObject> _vehiclePrefabs = new List<GameObject>();
        private List<GameObject> _sedanCarPrefabs = new List<GameObject>();
        private List<GameObject> _pickupTruckPrefabs = new List<GameObject>();
        private List<GameObject> _heavyTruckPrefabs = new List<GameObject>();

        public void Initialize(
            GameObject urbanCarPrefab,
            List<GameObject> vehiclePrefabs,
            List<GameObject> sedanCarPrefabs,
            List<GameObject> pickupTruckPrefabs,
            List<GameObject> heavyTruckPrefabs)
        {
            _urbanCarPrefab = urbanCarPrefab;
            _vehiclePrefabs = vehiclePrefabs ?? new List<GameObject>();
            _sedanCarPrefabs = sedanCarPrefabs ?? new List<GameObject>();
            _pickupTruckPrefabs = pickupTruckPrefabs ?? new List<GameObject>();
            _heavyTruckPrefabs = heavyTruckPrefabs ?? new List<GameObject>();
        }

        public GameObject ResolvePrefab(ObstacleType tier)
        {
            List<GameObject> list = tier switch
            {
                ObstacleType.Trains => _heavyTruckPrefabs,
                ObstacleType.Animals => _pickupTruckPrefabs,
                _ => _sedanCarPrefabs
            };

            if (list != null && list.Count > 0)
            {
                var valid = list.FindAll(p => p != null);
                if (valid.Count > 0)
                {
                    return valid[Random.Range(0, valid.Count)];
                }
            }

            return GetFallbackPrefab();
        }

        private GameObject GetFallbackPrefab()
        {
            if (_vehiclePrefabs != null && _vehiclePrefabs.Count > 0)
            {
                var valid = _vehiclePrefabs.FindAll(p => p != null);
                if (valid.Count > 0)
                {
                    return valid[Random.Range(0, valid.Count)];
                }
            }
            return _urbanCarPrefab;
        }

        /// <summary>
        /// Configures trigger box colliders and kinematic rigidbodies on instantiated vehicles.
        /// </summary>
        public void ConfigurePhysics(GameObject carInstance, ObstacleType tier)
        {
            if (carInstance == null) return;

            try { carInstance.tag = "Obstacle"; } catch { }

            Collider[] existingColliders = carInstance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < existingColliders.Length; i++)
            {
                if (existingColliders[i] != null)
                {
                    existingColliders[i].enabled = false;
                }
            }

            var box = carInstance.GetComponent<BoxCollider>();
            if (box == null) box = carInstance.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.enabled = true;

            switch (tier)
            {
                case ObstacleType.Trains:
                    box.size = new Vector3(2.6f, 3.2f, 7.5f);
                    box.center = new Vector3(0f, 1.6f, 0f);
                    break;
                case ObstacleType.Animals:
                    box.size = new Vector3(2.5f, 2.2f, 5.5f);
                    box.center = new Vector3(0f, 1.1f, 0f);
                    break;
                default: // ObstacleType.Car
                    box.size = new Vector3(2.2f, 1.6f, 4.8f);
                    box.center = new Vector3(0f, 0.8f, 0f);
                    break;
            }

            var rb = carInstance.GetComponent<Rigidbody>();
            if (rb == null) rb = carInstance.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }
}

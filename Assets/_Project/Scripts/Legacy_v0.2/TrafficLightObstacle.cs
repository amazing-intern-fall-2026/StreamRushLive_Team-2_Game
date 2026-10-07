using System.Collections;
using UnityEngine;
using SteamRush.Track;
using SteamRush.Features.Runner;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Traffic Light & Crossing Car obstacle (GDD v1.2 Section 2):
    /// Turns red to warn runner warningDuration (0.8s) before spawning a crossing car.
    /// </summary>
    public class TrafficLightObstacle : MonoBehaviour
    {
        [Header("Traffic Light Settings")]
        [Tooltip("Warning light duration before the car dashes across.")]
        [SerializeField] private float warningDuration = 0.8f;

        [Tooltip("Prefab of the car crossing the street.")]
        [SerializeField] private GameObject crossingCarPrefab;

        [Tooltip("Spawn position for the crossing car (calculated automatically if null).")]
        [SerializeField] private Transform carSpawnPoint;

        [Tooltip("Light renderer to tint red during warning.")]
        [SerializeField] private Renderer lightRenderer;

        [SerializeField] private Color redColor = Color.red;

        private bool _hasTriggered;
        private WorldSpeedManager _speedManager;
        private MovingWorldObject _movingWorldObject;

        private void Awake()
        {
            _movingWorldObject = GetComponent<MovingWorldObject>();
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
            // Only move manually if MovingWorldObject is absent to prevent double speed
            if (_movingWorldObject == null)
            {
                float speed = _speedManager != null ? _speedManager.CurrentSpeed : 0f;
                transform.position += Vector3.left * (speed * Time.deltaTime);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasTriggered) return;

            bool isPlayer = other.CompareTag("Player") || other.GetComponentInParent<RunnerController>() != null;
            if (!isPlayer) return;

            _hasTriggered = true;
            StartCoroutine(SpawnCrossingCar());
        }

        private IEnumerator SpawnCrossingCar()
        {
            if (lightRenderer != null)
            {
                lightRenderer.material.color = redColor;
            }

            yield return new WaitForSeconds(warningDuration);

            if (crossingCarPrefab != null)
            {
                Vector3 spawnPos = carSpawnPoint != null ? carSpawnPoint.position : transform.position + new Vector3(0f, 0.5f, 15f);
                Quaternion spawnRot = carSpawnPoint != null ? carSpawnPoint.rotation : Quaternion.Euler(0f, 180f, 0f);
                Instantiate(crossingCarPrefab, spawnPos, spawnRot);
            }
        }
    }
}
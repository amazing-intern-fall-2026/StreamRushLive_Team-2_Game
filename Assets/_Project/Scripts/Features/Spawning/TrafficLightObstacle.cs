using System.Collections;
using UnityEngine;
using SteamRush.Track;
namespace SteamRush.Features.Spawning
{
    public class TrafficLightObstacle : MonoBehaviour
    {
        [SerializeField] private float warningDuration = 0.8f;
        [SerializeField] private GameObject crossingCarPrefab;
        [SerializeField] private Transform carSpawnPoint;
        [SerializeField] private Renderer lightRenderer;
        [SerializeField] private Color redColor = Color.red;

        private bool _hasTriggered;
        private WorldSpeedManager _speedManager;

        private void Start()
        {
            _speedManager = FindFirstObjectByType<WorldSpeedManager>();
        }

        private void Update()
        {
            float speed = _speedManager != null ? _speedManager.CurrentSpeed : 0f;
            transform.position += Vector3.left * speed * Time.deltaTime;
        }
        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"TrafficLight OnTriggerEnter with: {other.gameObject.name}, tag: {other.tag}");

            if (_hasTriggered || !other.CompareTag("Player"))
            {
                return;
            }

            _hasTriggered = true;
            StartCoroutine(SpawnCrossingCar());
        }
        private IEnumerator SpawnCrossingCar()
{
    Debug.Log("SpawnCrossingCar: bắt đầu chờ warningDuration");

    if (lightRenderer != null)
    {
        lightRenderer.material.color = redColor;
    }

    yield return new WaitForSeconds(warningDuration);

    Debug.Log($"SpawnCrossingCar: hết chờ, crossingCarPrefab={crossingCarPrefab}, carSpawnPoint={carSpawnPoint}");

    if (crossingCarPrefab != null && carSpawnPoint != null)
    {
        Instantiate(crossingCarPrefab, carSpawnPoint.position, carSpawnPoint.rotation);
        Debug.Log("SpawnCrossingCar: đã Instantiate xe");
    }
}
    }
}
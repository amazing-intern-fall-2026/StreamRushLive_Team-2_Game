using System.Collections;
using UnityEngine;
using SteamRush.Track;
using SteamRush.Features.Runner;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Chướng ngại vật Đèn Đỏ & Xe Cắt Ngang (Traffic Light & Crossing Car - GDD v1.2 Mục 2):
    /// Khi Runner tiến gần, đèn bật đỏ cảnh báo trước warningDuration (0.8s), sau đó
    /// sinh xe CrossingCar phóng cắt ngang đường chạy theo trục Z.
    /// </summary>
    public class TrafficLightObstacle : MonoBehaviour
    {
        [Header("Traffic Light Settings")]
        [Tooltip("Thời gian đèn đỏ cảnh báo trước khi xe lao ra.")]
        [SerializeField] private float warningDuration = 0.8f;

        [Tooltip("Prefab xe ô tô phóng cắt ngang đường.")]
        [SerializeField] private GameObject crossingCarPrefab;

        [Tooltip("Vị trí xuất phát của xe ô tô (nếu null sẽ tự tính bên lề đường).")]
        [SerializeField] private Transform carSpawnPoint;

        [Tooltip("Renderer phần đèn để đổi màu đỏ khi cảnh báo.")]
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
            // Chỉ tự dịch chuyển nếu chưa có MovingWorldObject quản lý để tránh di chuyển x2 tốc độ
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
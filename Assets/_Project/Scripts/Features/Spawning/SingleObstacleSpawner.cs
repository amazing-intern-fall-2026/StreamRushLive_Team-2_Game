using System.Collections;
using SteamRush.Track;
using UnityEngine.InputSystem;
using UnityEngine;


namespace StreamRushLive.Features.Spawning
{
    public class SingleObstacleSpawner : MonoBehaviour
    {
        [Tooltip("Prefab xe Urban Street Car.")]
        [SerializeField] private GameObject urbanCarPrefab;

        [Tooltip("Prefab hiệu ứng tia laser đỏ nhấp nháy.")]
        [SerializeField] private GameObject laserIndicatorPrefab;

        [Tooltip("Tham chiếu vị trí Player để tính điểm spawn.")]
        [SerializeField] private Transform playerReference;

        [Tooltip("Quản lý tốc độ cuộn của thế giới; tự tìm nếu để trống.")]
        [SerializeField] private WorldSpeedManager worldSpeedManager;

        [Tooltip("Tọa độ Z của làn bên trái.")]
        [SerializeField] private float laneOffsetLeft = -3.0f;

        [Tooltip("Tọa độ Z của làn giữa.")]
        [SerializeField] private float laneOffsetCenter = 0.0f;

        [Tooltip("Tọa độ Z của làn bên phải.")]
        [SerializeField] private float laneOffsetRight = 3.0f;

        [Tooltip("Khoảng cách X phía trước Player nơi xe xuất hiện.")]
        [SerializeField] private float spawnDistanceAhead = 25f;

        [Tooltip("Thời gian cảnh báo laser trước khi xe xuất hiện.")]
        [SerializeField] private float laserWarningDuration = 3.5f;

        [Tooltip("Khoảng thời gian bật/tắt laser.")]
        [SerializeField] private float laserBlinkInterval = 0.15f;

        private void Start()
        {
            if (worldSpeedManager == null)
            {
                worldSpeedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
        }

        public void TriggerSpawnCarFromAntiLikes()
        {
            Debug.Log("[SingleObstacleSpawner] Nhận tín hiệu spawn xe từ Anti-Like.");

            if (playerReference == null || laserIndicatorPrefab == null || urbanCarPrefab == null)
            {
                Debug.LogWarning("[SingleObstacleSpawner] Thiếu Player reference hoặc prefab laser/xe.");
                return;
            }

            float[] laneOffsets = { laneOffsetLeft, laneOffsetCenter, laneOffsetRight };
            float selectedLane = laneOffsets[Random.Range(0, 3)];
            Vector3 spawnPosition = new Vector3(
                playerReference.position.x + spawnDistanceAhead,
                0f,
                selectedLane);

            // Tạo laser tại làn được chọn và cho laser cuộn theo thế giới.
            GameObject laserInstance = Instantiate(
                laserIndicatorPrefab,
                spawnPosition,
                laserIndicatorPrefab.transform.rotation);
            InitializeWorldMovement(laserInstance);

            // Chạy cảnh báo nhấp nháy trước khi thay laser bằng xe.
            StartCoroutine(BlinkLaserThenSpawnCar(laserInstance));
        }

        private void Update()
        {
            // TODO: chỉ để test tạm bằng phím Space, xóa hoặc gate bằng #if UNITY_EDITOR trước khi tích hợp tín hiệu Anti-Like thật
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TriggerSpawnCarFromAntiLikes();
            }
        }

        private IEnumerator BlinkLaserThenSpawnCar(GameObject laserInstance)
        {
            Renderer[] renderers = laserInstance.GetComponentsInChildren<Renderer>(true);
            float elapsed = 0f;
            bool isVisible = true;
            float blinkInterval = Mathf.Max(0.01f, laserBlinkInterval);

            // Nhấp nháy renderer trong đúng thời lượng cảnh báo.
            while (elapsed < laserWarningDuration)
            {
                SetRenderersEnabled(renderers, isVisible);
                isVisible = !isVisible;

                float waitTime = Mathf.Min(blinkInterval, laserWarningDuration - elapsed);
                yield return new WaitForSeconds(waitTime);
                elapsed += waitTime;
            }

            if (laserInstance == null)
            {
                yield break;
            }

            // Dùng vị trí hiện tại của laser sau khi đã cuộn theo thế giới.
            Vector3 currentPosition = laserInstance.transform.position;
            GameObject carInstance = Instantiate(
                urbanCarPrefab,
                currentPosition,
                urbanCarPrefab.transform.rotation);
            InitializeWorldMovement(carInstance);
            Destroy(laserInstance);
        }

        private void InitializeWorldMovement(GameObject instance)
        {
            MovingWorldObject movingObject = instance.GetComponent<MovingWorldObject>();
            if (movingObject == null)
            {
                movingObject = instance.AddComponent<MovingWorldObject>();
            }

            movingObject.Initialize(worldSpeedManager);
        }

        private static void SetRenderersEnabled(Renderer[] renderers, bool isEnabled)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = isEnabled;
            }
        }
    }
}
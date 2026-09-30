using System;
using UnityEngine;
using SteamRush.Features.Runner;

namespace SteamRush.Track
{
    // GDD v1.4.1 muc 7: sinh Cong Ve Dich bac ngang 3 lan SOM khi con lai <= earlyAppearDistanceMeters
    // truoc dich (thay vi doi du 100% moi hien) - de nguoi xem thay no tu xa tien lai gan dan, giong
    // cam giac tien den vach dich thuc te. Vi TrackTileLooper cong AddDistance() bang chinh
    // WorldSpeed*deltaTime giong het cach MovingWorldObject cuon vat the, dat spawn X = playerX +
    // remainingMeters la du de cong "den dung luc" khi TotalDistanceMeters cham Goal - khong can
    // dong bo lai gi them.
    // Phat hien thoi diem Runner bang qua bang polling vi tri X (dong bo style ActiveObstacle.IsActive
    // trong SingleObstacleSpawner.cs) thay vi Trigger Collider.
    public class FinishLineArchway : MonoBehaviour
    {
        [SerializeField] private GameObject archwayPrefab;
        [SerializeField] private TrackProgressTracker progressTracker;
        [SerializeField] private Transform playerReference;
        [SerializeField] private WorldSpeedManager worldSpeedManager;

        [Tooltip("Xuat hien som khi quang duong con lai <= gia tri nay (m), de thay Cong Ve Dich tu xa tien lai gan dan thay vi bat ngo hien dung luc du so.")]
        [SerializeField] private float earlyAppearDistanceMeters = 200f;

        public event Action RunnerCrossedFinishLine;

        private GameObject _archwayInstance;
        private bool _spawned;
        private bool _crossed;

        private void Awake()
        {
            if (progressTracker == null)
            {
                progressTracker = FindFirstObjectByType<TrackProgressTracker>();
            }
            if (playerReference == null)
            {
                var runner = FindFirstObjectByType<ChatLaneRunnerController>();
                if (runner != null) playerReference = runner.transform;
            }
            if (worldSpeedManager == null)
            {
                worldSpeedManager = FindFirstObjectByType<WorldSpeedManager>() ?? WorldSpeedManager.Instance;
            }
        }

        private void OnEnable()
        {
            if (progressTracker != null)
            {
                progressTracker.ProgressChanged.AddListener(HandleProgressChanged);
            }
        }

        private void OnDisable()
        {
            if (progressTracker != null)
            {
                progressTracker.ProgressChanged.RemoveListener(HandleProgressChanged);
            }
        }

        private void HandleProgressChanged(float currentLegDistanceMeters, float totalDistanceMeters, float goalProgress)
        {
            if (_spawned || progressTracker == null) return;

            float remaining = progressTracker.GoalDistanceMeters - totalDistanceMeters;
            if (remaining <= earlyAppearDistanceMeters)
            {
                SpawnArchway(Mathf.Max(remaining, 0f));
            }
        }

        private void SpawnArchway(float remainingMeters)
        {
            if (_spawned || archwayPrefab == null || playerReference == null) return;
            _spawned = true;

            Vector3 spawnPos = new Vector3(playerReference.position.x + remainingMeters, 0f, 0f);
            _archwayInstance = Instantiate(archwayPrefab, spawnPos, archwayPrefab.transform.rotation);
            _archwayInstance.name = "FinishLineArchway_Instance";

            // despawnXThreshold rat am - Cong Ve Dich phai o lai lam backdrop cho le an mung,
            // khong tu huy nhu xe can duong/item thong thuong.
            var mover = _archwayInstance.GetComponent<MovingWorldObject>();
            if (mover == null) mover = _archwayInstance.AddComponent<MovingWorldObject>();
            mover.Initialize(worldSpeedManager, -9999f);

            Debug.Log($"[FinishLineArchway] Da sinh Cong Ve Dich tai X={spawnPos.x:F1} (con lai {remainingMeters:F0}m truoc dich).");
        }

        private void Update()
        {
            if (_archwayInstance == null || _crossed) return;

            if (_archwayInstance.transform.position.x <= playerReference.position.x)
            {
                _crossed = true;
                Debug.Log("[FinishLineArchway] Runner da bang qua Cong Ve Dich!");
                RunnerCrossedFinishLine?.Invoke();
            }
        }

        [ContextMenu("Debug Spawn Archway (50m ahead)")]
        public void DebugSpawnArchway(float distanceAhead = 50f)
        {
            if (!_spawned)
            {
                SpawnArchway(distanceAhead);
            }
        }
    }
}

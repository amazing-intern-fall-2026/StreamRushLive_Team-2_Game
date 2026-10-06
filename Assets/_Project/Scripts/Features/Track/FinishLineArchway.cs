using System;
using UnityEngine;
using SteamRush.Features.Runner;

namespace SteamRush.Track
{
    // GDD v1.4.1 Section 7: Spawns the 3-lane finish line archway early when remaining distance
    // <= earlyAppearDistanceMeters, allowing viewers to see it approach naturally.
    // Detects when the runner crosses via polling X position matching SingleObstacleSpawner style.
    public class FinishLineArchway : MonoBehaviour
    {
        [SerializeField] private GameObject archwayPrefab;
        [SerializeField] private TrackProgressTracker progressTracker;
        [SerializeField] private Transform playerReference;
        [SerializeField] private WorldSpeedManager worldSpeedManager;

        [Tooltip("Spawns archway early when remaining distance <= this value (meters) so viewers see it approach smoothly.")]
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

            // Set very negative despawnXThreshold so archway remains as a backdrop during victory celebration
            var mover = _archwayInstance.GetComponent<MovingWorldObject>();
            if (mover == null) mover = _archwayInstance.AddComponent<MovingWorldObject>();
            mover.Initialize(worldSpeedManager, -9999f);

            Debug.Log($"[FinishLineArchway] Spawned Finish Line Archway at X={spawnPos.x:F1} ({remainingMeters:F0}m before goal).");
        }

        private void Update()
        {
            if (_archwayInstance == null || _crossed) return;

            if (_archwayInstance.transform.position.x <= playerReference.position.x)
            {
                _crossed = true;
                Debug.Log("[FinishLineArchway] Runner crossed finish line!");
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

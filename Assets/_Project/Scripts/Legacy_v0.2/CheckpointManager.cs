#pragma warning disable CS0618
using UnityEngine;
using SteamRush.Core;
using SteamRush.Track;

namespace SteamRush.Features.Runner
{
    // Saves a safe checkpoint every 200m (GDD v1.3 section 7). When the Runner dies (PlayerDeathEvent via
    // EventBus - SteamRush.Core), teleports the Runner back to the closest checkpoint and requests heart restore
    // via EventBus. Decoupled from HP/Heart modules.
    //
    // Reads travel distance through WorldSpeedManager (SteamRush.Track) - shared infrastructure for both
    // gameplay loops, maintaining decoupling.
    public class CheckpointManager : MonoBehaviour
    {
        [SerializeField] private Transform _runnerTransform;
        [SerializeField] private WorldSpeedManager _worldSpeedManager;
        [SerializeField] private float _checkpointIntervalMeters = 200f;
        [SerializeField] private int _heartCount = 3;

        private float _distanceSinceLastCheckpoint;
        private float _totalDistanceTraveled;
        private Vector3 _lastCheckpointPosition;
        private float _lastCheckpointDistance;
        private bool _hasCheckpoint;

        private void Awake()
        {
            if (_runnerTransform != null)
            {
                _lastCheckpointPosition = _runnerTransform.position;
                _hasCheckpoint = true;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
        }

        private void Update()
        {
            if (_worldSpeedManager == null)
            {
                return;
            }

            float step = _worldSpeedManager.CurrentSpeed * Time.deltaTime;
            _distanceSinceLastCheckpoint += step;
            _totalDistanceTraveled += step;

            if (_distanceSinceLastCheckpoint >= _checkpointIntervalMeters)
            {
                _distanceSinceLastCheckpoint -= _checkpointIntervalMeters;
                SaveCheckpoint();
            }
        }

        private void SaveCheckpoint()
        {
            if (_runnerTransform == null)
            {
                return;
            }

            _lastCheckpointPosition = _runnerTransform.position;
            _lastCheckpointDistance = _totalDistanceTraveled;
            _hasCheckpoint = true;
        }

        // Logs checkpoint respawn information.
        private void OnPlayerDeath(PlayerDeathEvent deathEvent)
        {
            if (_runnerTransform != null && _hasCheckpoint)
            {
                _runnerTransform.position = _lastCheckpointPosition;
            }

            Debug.Log($"[CheckpointManager] Runner respawned at closest checkpoint (saved distance: {_lastCheckpointDistance:F0}m).");

            EventBus.Publish(new RestoreHeartsRequestEvent(_heartCount));
        }
    }
}

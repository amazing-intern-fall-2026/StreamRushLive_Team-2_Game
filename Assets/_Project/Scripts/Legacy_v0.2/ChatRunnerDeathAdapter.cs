using UnityEngine;
using SteamRush.Core;
using SteamRush.Features.Runner;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Legacy adapter connecting death and revive events between RunnerHealthSystem and CheckpointManager via EventBus.
    /// </summary>
    public class ChatRunnerDeathAdapter : MonoBehaviour
    {
        [SerializeField] private RunnerHealthSystem _healthSystem;

        private void Awake()
        {
            if (_healthSystem == null)
            {
                _healthSystem = GetComponent<RunnerHealthSystem>();
                if (_healthSystem == null)
                {
                    _healthSystem = GetComponentInParent<RunnerHealthSystem>();
                }
                if (_healthSystem == null)
                {
                    _healthSystem = FindFirstObjectByType<RunnerHealthSystem>();
                }
            }
        }

        private void OnEnable()
        {
            if (_healthSystem != null)
            {
                _healthSystem.OnPlayerDeath += HandlePlayerDeath;
            }

            EventBus.Subscribe<RestoreHeartsRequestEvent>(HandleRestoreHearts);
        }

        private void OnDisable()
        {
            if (_healthSystem != null)
            {
                _healthSystem.OnPlayerDeath -= HandlePlayerDeath;
            }

            EventBus.Unsubscribe<RestoreHeartsRequestEvent>(HandleRestoreHearts);
        }

        private void HandlePlayerDeath()
        {
            Debug.Log("[ChatRunnerDeathAdapter] Runner out of hearts -> Publishing PlayerDeathEvent to CheckpointManager.");
            EventBus.Publish(new PlayerDeathEvent());
        }

        private void HandleRestoreHearts(RestoreHeartsRequestEvent evt)
        {
            Debug.Log($"[ChatRunnerDeathAdapter] Received RestoreHeartsRequestEvent({evt.HeartCount}) -> Resetting runner health.");
            if (_healthSystem != null)
            {
                _healthSystem.ResetHealth();
            }
        }
    }
}

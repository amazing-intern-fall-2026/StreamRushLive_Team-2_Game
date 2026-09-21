using UnityEngine;
using SteamRush.Core;
using SteamRush.Features.Runner;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Adapter nối sự kiện chết và hồi sinh giữa RunnerHealthSystem và CheckpointManager qua EventBus.
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
            Debug.Log("[ChatRunnerDeathAdapter] Runner hết tim -> Phát PlayerDeathEvent tới CheckpointManager.");
            EventBus.Publish(new PlayerDeathEvent());
        }

        private void HandleRestoreHearts(RestoreHeartsRequestEvent evt)
        {
            Debug.Log($"[ChatRunnerDeathAdapter] Nhận RestoreHeartsRequestEvent({evt.HeartCount}) -> Reset máu Runner về đầy.");
            if (_healthSystem != null)
            {
                _healthSystem.ResetHealth();
            }
        }
    }
}

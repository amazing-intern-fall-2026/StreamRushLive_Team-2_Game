using UnityEngine;
using TMPro;
using SteamRush.Features.Runner;
using SteamRush.Track;

namespace SteamRush.Features.UI
{
    /// <summary>
    /// Displays real-time status HUD:
    /// - Active Runner Name
    /// - Queued Followers Count
    /// - Leg Progress (m / 100m)
    /// - Current World Scroll Speed (m/s)
    /// - Controls & Chat Command Quick Reference
    /// </summary>
    public class ChatRunnerStatusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ChatRunnerQueueManager _queueManager;
        [SerializeField] private WorldSpeedManager _speedManager;
        [SerializeField] private TMP_Text _runnerInfoLabel;
        [SerializeField] private TMP_Text _controlsGuideLabel;

        private void Awake()
        {
            if (_queueManager == null)
            {
                _queueManager = FindFirstObjectByType<ChatRunnerQueueManager>();
            }

            if (_speedManager == null)
            {
                _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
        }

        private void Start()
        {
            if (PreGameConfig.PreGameConfigManager.Instance != null && PreGameConfig.PreGameConfigManager.Instance.CurrentConfig != null)
            {
                gameObject.SetActive(PreGameConfig.PreGameConfigManager.Instance.CurrentConfig.enableDebugUI);
            }
        }

        private void Update()
        {
            if (_queueManager == null)
            {
                _queueManager = FindFirstObjectByType<ChatRunnerQueueManager>();
            }

            if (_speedManager == null)
            {
                _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            }

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (_runnerInfoLabel != null && _queueManager != null)
            {
                string runner = string.IsNullOrEmpty(_queueManager.CurrentRunnerId) ? "None" : _queueManager.CurrentRunnerId;
                string runnerDisplay = _queueManager.CurrentRunnerIsVip
                    ? $"<color=#FFD700>{runner}</color>"
                    : $"<color=#00BFFF>{runner}</color>";

                var nextRunner = _queueManager.PeekNextRunner();
                string nextDisplay = nextRunner.HasValue
                    ? (nextRunner.Value.isVip ? $"<color=#FFD700>{nextRunner.Value.name}</color>" : $"<color=#FFFFFF>{nextRunner.Value.name}</color>")
                    : "<color=#888888>(Empty)</color>";

                int queue = _queueManager.QueuedCount;
                float progress = _queueManager.LegProgress;
                float legMax = _queueManager.LegDistanceMeters;
                float speed = _speedManager != null ? _speedManager.CurrentSpeed : 0f;

                string statusText = _queueManager.IsWaitingForFollower ? "<color=#FF4444>[WAITING]</color>" : "<color=#44FF44>[RUNNING]</color>";
                string speedTag = "";
                if (speed > 10.0f)
                {
                    speedTag = " <color=#FF4500><b>[BOOST!]</b></color>";
                }

                _runnerInfoLabel.text = $"<b>{statusText}</b> {runnerDisplay} | <b>Next:</b> {nextDisplay} | <b>Queue:</b> {queue} | <b>Leg:</b> {progress:F0}/{legMax:F0}m | <b>Speed:</b> {speed:F1} m/s{speedTag}";
            }

            if (_controlsGuideLabel != null)
            {
                _controlsGuideLabel.text = "<b>Cmds:</b> <color=#80D0FF>1/2/3/jump</color> - <color=#00BFFF>blue</color> - <color=#FF6347>red</color> | <b>[F1]</b> Shield | <b>[Shift+F1]</b> Free 30s | <b>[F2]</b> Sprint | <b>[Shift+F3/F4]</b> +Energy";
            }
        }
    }
}

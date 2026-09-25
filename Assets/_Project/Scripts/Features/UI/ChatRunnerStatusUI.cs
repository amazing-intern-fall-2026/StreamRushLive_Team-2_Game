using UnityEngine;
using TMPro;
using SteamRush.Features.Runner;
using SteamRush.Track;

namespace SteamRush.Features.UI
{
    /// <summary>
    /// Hiển thị thông tin trực quan theo thời gian thực cho Prototype Chatland:
    /// - Tên Runner đang chạy
    /// - Số lượng Follower đang chờ trong hàng đợi
    /// - Tiến độ chặng đường (m / 100m)
    /// - Tốc độ cuộn thế giới hiện tại (m/s)
    /// - Hướng dẫn nhanh phím tắt / lệnh chat
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
                int queue = _queueManager.QueuedCount;
                float progress = _queueManager.LegProgress;
                float legMax = _queueManager.LegDistanceMeters;
                float speed = _speedManager != null ? _speedManager.CurrentSpeed : 0f;

                string statusText = _queueManager.IsWaitingForFollower ? "<color=#FF4444>[CHỜ FOLLOWER]</color>" : "<color=#44FF44>[CHẠY]</color>";
                string speedTag = "";
                if (speed > 10.0f)
                {
                    speedTag = " <color=#FF4500><b>[BỨT TỐC!]</b></color>";
                }

                _runnerInfoLabel.text = $"<b>{statusText}</b> <color=#FFD700>{runner}</color> | <b>Đợi:</b> {queue} | <b>Chặng:</b> {progress:F0}/{legMax:F0}m | <b>Tốc độ:</b> {speed:F1} m/s{speedTag}";
            }

            if (_controlsGuideLabel != null)
            {
                _controlsGuideLabel.text = "<b>Lệnh chat:</b> <color=#80D0FF>1/2/3</color> (làn) - <color=#FFA500>fast</color> - <color=#00BFFF>#fan</color> - <color=#FF6347>#anti</color> (300 tim thả xe) | <b>[F1-F5]</b> Test Quà";
            }
        }
    }
}

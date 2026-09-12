using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SteamRush.Track;
using SteamRush.Features.UI;

namespace SteamRush.Relay
{
    public class RelayQueueManager : MonoBehaviour
    {
        [SerializeField] private TrackProgressTracker _progressTracker;
        [SerializeField] private HUDManager _hudManager;
        [Header("Runner Settings")]
        [Tooltip("Initial runner's display name.")]
        [SerializeField] private string _initialRunnerName = "Runner_Start";

        [Header("Demo Queue Settings")]
        [Tooltip("Predefined demo follower names queued up for baton pass.")]
        [SerializeField] private List<string> _demoFollowers = new List<string>
        {
            "Viewer_Alex",
            "Viewer_Bao",
            "Viewer_Chi",
            "Viewer_Dung",
            "Viewer_Emma"
        };

        private readonly Queue<string> _followers = new Queue<string>();

        public int Count => _followers.Count;

        [SerializeField] private UnityEvent<string> _followerNameChanged = new UnityEvent<string>();
        public UnityEvent<string> FollowerNameChanged => _followerNameChanged;

        private void Awake()
        {
            if (_progressTracker == null) _progressTracker = FindFirstObjectByType<TrackProgressTracker>();
            if (_hudManager == null) _hudManager = FindFirstObjectByType<HUDManager>();

            // Khởi tạo hàng đợi từ danh sách cấu hình trên Inspector
            if (_demoFollowers != null && _demoFollowers.Count > 0)
            {
                for (int i = 0; i < _demoFollowers.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_demoFollowers[i]))
                    {
                        _followers.Enqueue(_demoFollowers[i]);
                    }
                }
            }
        }

        private void Start()
        {
            // Cập nhật tên runner khởi đầu lên HUD
            if (_hudManager != null)
            {
                _hudManager.UpdateRunnerInfo(_initialRunnerName, null);
            }
        }

        private void OnEnable()
        {
            if (_progressTracker != null)
            {
                _progressTracker.RelayCompleted.AddListener(HandleRelayCompleted);
            }
        }

        private void OnDisable()
        {
            if (_progressTracker != null)
            {
                _progressTracker.RelayCompleted.RemoveListener(HandleRelayCompleted);
            }
        }

        public void EnqueueFollower(string followerId)
        {
            _followers.Enqueue(followerId);
            _hudManager?.ShowStatusPopup($"+1 Đăng ký: {followerId}", true);
        }

        private void HandleRelayCompleted(int relayNumber)
        {
            if (_followers.Count == 0)
            {
                _hudManager?.ShowStatusPopup($"Hoàn thành chặng {relayNumber}!", true);
                return;
            }

            string followerId = _followers.Dequeue();
            _followerNameChanged.Invoke(followerId);

            if (_hudManager != null)
            {
                _hudManager.UpdateRunnerInfo(followerId, null);
                _hudManager.ShowStatusPopup($"Chuyển gậy: {followerId}!", true);
            }
        }
    }
}
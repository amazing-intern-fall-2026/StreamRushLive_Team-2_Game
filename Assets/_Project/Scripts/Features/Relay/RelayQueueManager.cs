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
        [SerializeField] private string _initialRunnerName = "Runner_Start";

        private readonly Queue<string> _followers = new Queue<string>();

        public int Count => _followers.Count;

        [SerializeField] private UnityEvent<string> _followerNameChanged = new UnityEvent<string>();
        public UnityEvent<string> FollowerNameChanged => _followerNameChanged;

        private void Awake()
        {
            if (_progressTracker == null) _progressTracker = FindFirstObjectByType<TrackProgressTracker>();
            if (_hudManager == null) _hudManager = FindFirstObjectByType<HUDManager>();

            // Khởi tạo một số follower giả lập để test
            _followers.Enqueue("Viewer_Alex");
            _followers.Enqueue("Viewer_Bao");
            _followers.Enqueue("Viewer_Chi");
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
            Debug.Log($"[RelayQueue] Follower {followerId} đã vào hàng đợi. Tổng: {_followers.Count}");
        }

        private void HandleRelayCompleted(int relayNumber)
        {
            if (_followers.Count == 0)
            {
                Debug.Log($"[RelayQueue] Chặng {relayNumber} hoàn thành, nhưng hàng đợi rỗng. Runner tiếp tục chạy chặng mới.");
                return;
            }

            string followerId = _followers.Dequeue();
            Debug.Log($"[RelayQueue] Chặng {relayNumber} hoàn thành. Runner tiếp theo: {followerId}");

            _followerNameChanged.Invoke(followerId);

            if (_hudManager != null)
            {
                _hudManager.UpdateRunnerInfo(followerId, null);
            }
        }
    }
}
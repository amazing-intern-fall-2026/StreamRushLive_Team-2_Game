using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SteamRush.Track;
using SteamRush.Features.StreamIntegration;
using SteamRush.Features.UI;

namespace SteamRush.Features.Runner
{
    // Quan ly hang doi Follower cho ChatRunner (GDD v1.3 muc 1 & 3). Chi Follower qua
    // FollowerGate moi duoc vao hang doi. Het 1 chang (100m) ma hang doi rong -> dung the gioi
    // (WorldScrollSpeed = 0) va bao UI qua UnityEvent (Empty Queue Hold); co Follower moi vao
    // luc dang cho -> khoi phuc toc do, an bang, chon Runner ke tiep, xuat phat chang moi.
    public class ChatRunnerQueueManager : MonoBehaviour
    {
        [SerializeField] private WorldSpeedManager _worldSpeedManager;
        [SerializeField] private float _legDistanceMeters = 100f;

        [Header("HUD (facade co san - xem HUDManager.cs)")]
        [SerializeField] private HUDManager _hudManager;
        [SerializeField] private Transform _runnerTransform;

        [Serializable] public class WaitingStateChangedEvent : UnityEvent<bool> { }
        [SerializeField] private WaitingStateChangedEvent _waitingStateChanged = new WaitingStateChangedEvent();
        public WaitingStateChangedEvent WaitingStateChanged => _waitingStateChanged;

        private readonly Queue<string> _followerQueue = new Queue<string>();
        private readonly FollowerGate _followerGate = new FollowerGate();

        private float _distanceSinceLastLeg;
        private bool _isWaitingForFollower;
        private float _speedBeforeWait;

        public string CurrentRunnerId { get; private set; }
        public bool IsWaitingForFollower => _isWaitingForFollower;

        // Goi tu StreamIntegration khi co su kien Follow moi - chi nhan neu qua duoc FollowerGate.
        public void TryEnqueueFollower(string userId)
        {
            if (!_followerGate.CanJoinQueue(userId))
            {
                return;
            }

            _followerQueue.Enqueue(userId);

            if (_isWaitingForFollower)
            {
                ResumeFromWaiting();
            }
        }

        private void Update()
        {
            if (_isWaitingForFollower || _worldSpeedManager == null)
            {
                return;
            }

            _distanceSinceLastLeg += _worldSpeedManager.CurrentSpeed * Time.deltaTime;

            if (_distanceSinceLastLeg >= _legDistanceMeters)
            {
                _distanceSinceLastLeg -= _legDistanceMeters;
                AdvanceToNextRunner();
            }
        }

        private void AdvanceToNextRunner()
        {
            if (_followerQueue.Count == 0)
            {
                EnterWaitingState();
                return;
            }

            CurrentRunnerId = _followerQueue.Dequeue();
            UpdateRunnerHud();
        }

        // Gan ten/avatar len bang ten cua Runner hien tai qua facade HUDManager co san.
        // Avatar that (Sprite) se do StreamIntegration cung cap sau - tam thoi null.
        private void UpdateRunnerHud()
        {
            if (_hudManager == null)
            {
                return;
            }

            _hudManager.UpdateRunnerInfo(CurrentRunnerId, null);
            _hudManager.UpdateRunnerTarget(_runnerTransform);
        }

        // Empty Queue Hold: dung the gioi + bao UI hien bang cho.
        private void EnterWaitingState()
        {
            _isWaitingForFollower = true;
            _speedBeforeWait = _worldSpeedManager != null ? _worldSpeedManager.CurrentSpeed : 0f;

            if (_worldSpeedManager != null)
            {
                _worldSpeedManager.CurrentSpeed = 0f;
            }

            _waitingStateChanged.Invoke(true);
        }

        // Co Follower moi vao luc dang cho: khoi phuc toc do, an bang, chon Runner ke tiep.
        private void ResumeFromWaiting()
        {
            _isWaitingForFollower = false;

            if (_worldSpeedManager != null)
            {
                _worldSpeedManager.CurrentSpeed = _speedBeforeWait;
            }

            _waitingStateChanged.Invoke(false);

            CurrentRunnerId = _followerQueue.Dequeue();
            UpdateRunnerHud();
        }
    }
}

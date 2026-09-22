using System;
using UnityEngine;
using UnityEngine.Events;
using SteamRush.Features.UI;

namespace SteamRush.Track
{
    public class TrackProgressTracker : MonoBehaviour
    {
        [Header("Relay & Goal Distance Settings")]
        [Tooltip("Distance in meters required per baton relay / pass (e.g. 100m).")]
        [SerializeField] private float _relayDistanceMeters = 100f;

        [Tooltip("Total goal distance in kilometers (e.g. 100km).")]
        [SerializeField] private float _goalDistanceKm = 100f;

        public float RelayDistanceMeters => _relayDistanceMeters;
        public float GoalDistanceKm => _goalDistanceKm;
        public float GoalDistanceMeters => _goalDistanceKm * 1000f;

        [Serializable] public class ProgressChangedEvent : UnityEvent<float, float, float> { }

        [SerializeField] private ProgressChangedEvent _progressChanged = new ProgressChangedEvent();
        public ProgressChangedEvent ProgressChanged => _progressChanged;

        [SerializeField] private UnityEvent<int> _relayCompleted = new UnityEvent<int>();
        public UnityEvent<int> RelayCompleted => _relayCompleted;

        [SerializeField] private HUDManager _hudManager;

        public float CurrentLegDistanceMeters { get; private set; }
        public float TotalDistanceMeters { get; private set; }
        public float GoalProgress => TotalDistanceMeters / GoalDistanceMeters;

        private int _completedRelayCount;

        private void Start()
        {
            if (_hudManager == null)
            {
                _hudManager = FindFirstObjectByType<HUDManager>();
            }

            if (_hudManager != null)
            {
                _hudManager.UpdateProgress(TotalDistanceMeters / 1000f);
            }
        }

        public void AddDistance(float distanceDelta)
        {
            if (distanceDelta <= 0f)
            {
                return;
            }

            CurrentLegDistanceMeters += distanceDelta;
            TotalDistanceMeters = Mathf.Min(TotalDistanceMeters + distanceDelta, GoalDistanceMeters);

            while (CurrentLegDistanceMeters >= RelayDistanceMeters)
            {
                CurrentLegDistanceMeters -= RelayDistanceMeters;
                _completedRelayCount++;
                _relayCompleted.Invoke(_completedRelayCount);
            }

            _progressChanged.Invoke(CurrentLegDistanceMeters, TotalDistanceMeters, GoalProgress);

            if (_hudManager != null)
            {
                _hudManager.UpdateProgress(TotalDistanceMeters / 1000f);
            }
        }

        /// <summary>
        /// Giảm quãng đường hiện tại (dùng khi người chơi va chạm phải chướng ngại vật có hình phạt trừ quãng đường).
        /// </summary>
        public void ReduceDistance(float distanceDelta)
        {
            if (distanceDelta <= 0f) return;

            CurrentLegDistanceMeters = Mathf.Max(0f, CurrentLegDistanceMeters - distanceDelta);
            TotalDistanceMeters = Mathf.Max(0f, TotalDistanceMeters - distanceDelta);

            _progressChanged.Invoke(CurrentLegDistanceMeters, TotalDistanceMeters, GoalProgress);

            if (_hudManager != null)
            {
                _hudManager.UpdateProgress(TotalDistanceMeters / 1000f);
                _hudManager.ShowStatusPopup($"-{distanceDelta:F0}m Quãng đường!", false);
            }
        }
    }
}
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

        public void SetGoalDistanceMeters(float meters)
        {
            if (meters >= 900000000f)
            {
                _goalDistanceKm = meters / 1000f;
            }
            else
            {
                if (TotalDistanceMeters > 0f && TotalDistanceMeters >= meters)
                {
                    _goalDistanceKm = (TotalDistanceMeters + meters) / 1000f;
                }
                else
                {
                    _goalDistanceKm = Mathf.Max(1f, meters) / 1000f;
                }
            }

            _goalReachedFired = false;

            var archway = FindFirstObjectByType<FinishLineArchway>();
            if (archway != null)
            {
                archway.ResetArchway();
            }

            if (_hudManager != null)
            {
                _hudManager.UpdateLegProgress(TotalDistanceMeters, GoalDistanceMeters);
            }
        }

        [Serializable] public class ProgressChangedEvent : UnityEvent<float, float, float> { }

        [SerializeField] private ProgressChangedEvent _progressChanged = new ProgressChangedEvent();
        public ProgressChangedEvent ProgressChanged => _progressChanged;

        [SerializeField] private UnityEvent<int> _relayCompleted = new UnityEvent<int>();
        public UnityEvent<int> RelayCompleted => _relayCompleted;

        // Fired once when TotalDistanceMeters reaches GoalDistanceMeters (GDD v1.4.1 Section 7).
        [SerializeField] private UnityEvent _goalReached = new UnityEvent();
        public UnityEvent GoalReached => _goalReached;
        private bool _goalReachedFired;

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
                _hudManager.UpdateLegProgress(TotalDistanceMeters, GoalDistanceMeters);
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
                _hudManager.UpdateLegProgress(TotalDistanceMeters, GoalDistanceMeters);
            }

            if (!_goalReachedFired && TotalDistanceMeters >= GoalDistanceMeters)
            {
                _goalReachedFired = true;
                _goalReached.Invoke();
            }
        }

        /// <summary>
        /// Deducts distance from progress on obstacle collision penalty.
        /// </summary>
        public void ReduceDistance(float distanceDelta)
        {
            if (distanceDelta <= 0f) return;

            CurrentLegDistanceMeters = Mathf.Max(0f, CurrentLegDistanceMeters - distanceDelta);
            TotalDistanceMeters = Mathf.Max(0f, TotalDistanceMeters - distanceDelta);

            _progressChanged.Invoke(CurrentLegDistanceMeters, TotalDistanceMeters, GoalProgress);

            if (_hudManager != null)
            {
                _hudManager.UpdateLegProgress(TotalDistanceMeters, GoalDistanceMeters);
                _hudManager.ShowStatusPopup($"-{distanceDelta:F0}m Distance!", false);
            }
        }

        [ContextMenu("Debug Jump Near Finish Line (50m)")]
        public void DebugJumpNearGoal(float metersBeforeGoal = 50f)
        {
            TotalDistanceMeters = Mathf.Max(0f, GoalDistanceMeters - metersBeforeGoal);
            _progressChanged.Invoke(CurrentLegDistanceMeters, TotalDistanceMeters, GoalProgress);

            if (_hudManager != null)
            {
                _hudManager.UpdateLegProgress(TotalDistanceMeters, GoalDistanceMeters);
            }

            if (!_goalReachedFired && TotalDistanceMeters >= GoalDistanceMeters)
            {
                _goalReachedFired = true;
                _goalReached.Invoke();
            }
        }
    }
}
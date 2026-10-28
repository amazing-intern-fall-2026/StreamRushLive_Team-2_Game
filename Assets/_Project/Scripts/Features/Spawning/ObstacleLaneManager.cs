using System;
using System.Collections.Generic;
using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Single Responsibility: Tracks and manages lane concurrency, lane availability,
    /// and lane coordinate offsets for obstacles (Sedans, Animals, Trains).
    /// </summary>
    [Serializable]
    public class ObstacleLaneManager
    {
        [Header("Lane Offsets")]
        [SerializeField] private float _laneOffsetLeft = 3.0f;
        [SerializeField] private float _laneOffsetCenter = 0.0f;
        [SerializeField] private float _laneOffsetRight = -3.0f;

        [Header("Concurrency Limit")]
        [SerializeField] private int _maxConcurrentObstacles = 2;

        private readonly List<ActiveObstacle> _activeObstacles = new List<ActiveObstacle>();

        public int MaxConcurrentObstacles
        {
            get => _maxConcurrentObstacles;
            set => _maxConcurrentObstacles = Mathf.Clamp(value, 1, 2);
        }

        public int ActiveCount
        {
            get
            {
                CleanupInactive();
                return _activeObstacles.Count;
            }
        }

        public void Configure(float leftZ, float centerZ, float rightZ, int maxConcurrent)
        {
            _laneOffsetLeft = leftZ;
            _laneOffsetCenter = centerZ;
            _laneOffsetRight = rightZ;
            _maxConcurrentObstacles = Mathf.Clamp(maxConcurrent, 1, 2);
        }

        public float GetLaneOffsetZ(int laneIndex)
        {
            if (laneIndex == 1) return _laneOffsetLeft;   // +3.0f (Left lane)
            if (laneIndex == 3) return _laneOffsetRight;  // -3.0f (Right lane)
            return _laneOffsetCenter;                     //  0.0f (Middle lane)
        }

        public void CleanupInactive()
        {
            _activeObstacles.RemoveAll(o => !o.IsActive);
        }

        public void RegisterObstacle(ActiveObstacle obstacle)
        {
            if (obstacle != null)
            {
                _activeObstacles.Add(obstacle);
            }
        }

        public void ClearAll()
        {
            _activeObstacles.Clear();
        }

        public void ClearPendingLasers()
        {
            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                var obs = _activeObstacles[i];
                if (obs.LaserInstance != null)
                {
                    UnityEngine.Object.Destroy(obs.LaserInstance);
                    obs.LaserInstance = null;
                    obs.IsPendingSpawn = false;
                }
            }
            CleanupInactive();
        }

        public bool CanSpawnObstacle(bool isUnlimitedMode)
        {
            CleanupInactive();
            if (isUnlimitedMode) return true;
            return _activeObstacles.Count < _maxConcurrentObstacles;
        }

        public bool CanSpawnObstacleOnLane(int laneIndex, bool isUnlimitedMode)
        {
            CleanupInactive();
            if (!isUnlimitedMode && _activeObstacles.Count >= _maxConcurrentObstacles)
            {
                return false;
            }

            foreach (var obs in _activeObstacles)
            {
                if (obs.LaneIndex == laneIndex && obs.IsActive)
                {
                    return false;
                }
            }

            return true;
        }

        public int ResolveTargetLane(int requestedLane, bool forceSpawn, bool isUnlimitedMode)
        {
            if (requestedLane >= 1 && requestedLane <= 3)
            {
                if (CanSpawnObstacleOnLane(requestedLane, isUnlimitedMode))
                {
                    return requestedLane;
                }
                if (forceSpawn)
                {
                    DisplaceObstacleOnLane(requestedLane);
                    return requestedLane;
                }
                return -1;
            }

            // Pick an available lane
            var availableLanes = new List<int>();
            for (int i = 1; i <= 3; i++)
            {
                if (CanSpawnObstacleOnLane(i, isUnlimitedMode))
                {
                    availableLanes.Add(i);
                }
            }

            if (availableLanes.Count > 0)
            {
                return availableLanes[UnityEngine.Random.Range(0, availableLanes.Count)];
            }

            if (forceSpawn)
            {
                return DisplaceLowestPriorityObstacle();
            }

            return -1;
        }

        /// <summary>
        /// Displaces and destroys any active obstacle or warning in the specified lane to make way for high-priority/whale spawns.
        /// </summary>
        public void DisplaceObstacleOnLane(int laneIndex)
        {
            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                var obs = _activeObstacles[i];
                if (obs.LaneIndex == laneIndex && obs.IsActive)
                {
                    if (obs.LaserInstance != null)
                    {
                        UnityEngine.Object.Destroy(obs.LaserInstance);
                        obs.LaserInstance = null;
                    }
                    if (obs.CarInstance != null)
                    {
                        AudioManager.Instance?.PlaySFX(SFXType.HitCarCrash, 0.85f);
                        UnityEngine.Object.Destroy(obs.CarInstance);
                        obs.CarInstance = null;
                    }
                    obs.IsPendingSpawn = false;
                    _activeObstacles.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>
        /// Displaces the oldest active lower-tier obstacle to guarantee lane availability for whale gifts.
        /// </summary>
        public int DisplaceLowestPriorityObstacle()
        {
            CleanupInactive();
            if (_activeObstacles.Count == 0) return UnityEngine.Random.Range(1, 4);

            int targetLane = _activeObstacles[0].LaneIndex;
            DisplaceObstacleOnLane(targetLane);
            return targetLane;
        }
    }
}

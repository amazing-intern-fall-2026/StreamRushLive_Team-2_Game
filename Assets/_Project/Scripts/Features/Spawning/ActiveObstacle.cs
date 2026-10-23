using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Represents an active or pending obstacle in the running track.
    /// Tracks lane, laser indicator instance, car GameObject instance, and runner relative position.
    /// </summary>
    public class ActiveObstacle
    {
        public int LaneIndex;
        public float LaneZ;
        public GameObject LaserInstance;
        public GameObject CarInstance;
        public Transform PlayerRef;
        public bool IsPendingSpawn;

        public bool IsActive
        {
            get
            {
                if (IsPendingSpawn) return true;
                if (LaserInstance != null) return true;
                if (CarInstance != null)
                {
                    // Vehicle remains an active obstacle until passing behind player
                    float playerX = PlayerRef != null ? PlayerRef.position.x : 0f;
                    return CarInstance.transform.position.x > playerX - 1.5f;
                }
                return false;
            }
        }
    }
}

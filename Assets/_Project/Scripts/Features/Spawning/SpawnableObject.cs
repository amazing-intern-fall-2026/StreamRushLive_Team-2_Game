using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Base class for all spawner objects.
    /// </summary>
    public class SpawnableObject : MonoBehaviour
    {
        [SerializeField] private SpawnType spawnType;

        public SpawnType Type => spawnType;
    }
}
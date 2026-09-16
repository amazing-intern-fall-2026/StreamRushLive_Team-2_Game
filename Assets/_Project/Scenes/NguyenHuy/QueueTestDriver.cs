using UnityEngine;
using StreamRushLive.Features.Spawning;

public class QueueTestDriver : MonoBehaviour
{
    [SerializeField] private Spawner spawner;

    private void Start()
    {
        spawner.EnqueueObstacle(ObstacleType.LowBarrier);
        spawner.EnqueueObstacle(ObstacleType.HighBarrier);
        spawner.EnqueueObstacle(ObstacleType.LowBarrier);
        spawner.EnqueueObstacle(ObstacleType.HighBarrier);
    }
}
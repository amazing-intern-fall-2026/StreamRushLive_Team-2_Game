using UnityEngine;
using StreamRushLive.Features.Spawning;

public class QueueTestDriver : MonoBehaviour
{
    [SerializeField] private Spawner spawner;

    private void Start()
    {
        spawner.EnqueueObstacle(ObstacleType.Car);
        spawner.EnqueueObstacle(ObstacleType.Trains);
        spawner.EnqueueObstacle(ObstacleType.Car);
        spawner.EnqueueObstacle(ObstacleType.Trains);
    }
}
using UnityEngine;
using SteamRush.Core;
using StreamRushLive.Features.Spawning;

namespace SteamRush.Features.StreamIntegration
{
    /// <summary>
    /// Adapter nối sự kiện RequestCarSpawnEvent từ FactionTugOfWarManager
    /// sang SingleObstacleSpawner để sinh xe cản đường khi phe Anti tích đủ 500 tim.
    /// </summary>
    public class ChatRunnerCarSpawnAdapter : MonoBehaviour
    {
        [SerializeField] private SingleObstacleSpawner _spawner;

        private void Awake()
        {
            if (_spawner == null)
            {
                _spawner = FindFirstObjectByType<SingleObstacleSpawner>();
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<RequestCarSpawnEvent>(OnRequestCarSpawn);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RequestCarSpawnEvent>(OnRequestCarSpawn);
        }

        private void OnRequestCarSpawn(RequestCarSpawnEvent evt)
        {
            if (_spawner == null)
            {
                _spawner = FindFirstObjectByType<SingleObstacleSpawner>();
            }

            if (_spawner != null)
            {
                if (evt.LaneIndex >= 1 && evt.LaneIndex <= 3)
                {
                    _spawner.TriggerSpawnCarOnLane(evt.LaneIndex);
                }
                else
                {
                    _spawner.TriggerSpawnCarFromAntiLikes();
                }
            }
            else
            {
                Debug.LogWarning("[ChatRunnerCarSpawnAdapter] Không tìm thấy SingleObstacleSpawner trong scene!");
            }
        }
    }
}

using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    public class StopSignObstacle : ObstacleBase
    {
        [SerializeField] private float stopDuration = 1.0f;

        private WorldSpeedManager _speedManager;

        private void Start()
        {
            _speedManager = FindFirstObjectByType<WorldSpeedManager>();
        }

        private void Update()
        {
            float speed = _speedManager != null ? _speedManager.CurrentSpeed : 0f;
            transform.position += Vector3.left * speed * Time.deltaTime;
        }

        public override void OnHitPlayer(GameObject player)
        {
            Debug.Log($"Stop Sign: Runner cần dừng {stopDuration} giây rồi tự tăng tốc lại (chưa implement thật).", this);
        }
    }
}
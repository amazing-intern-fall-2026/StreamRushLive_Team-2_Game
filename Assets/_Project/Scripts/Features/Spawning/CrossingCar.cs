using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    public class CrossingCar : ObstacleBase
    {
        [SerializeField] private float crossSpeed = 6f;
        [SerializeField] private float despawnZThreshold = 15f;

        private SteamRush.Track.WorldSpeedManager _speedManager;

        private void Awake()
        {
            obstacleType = ObstacleType.LowBarrier;
        }

        private void Start()
        {
            if (_speedManager == null)
            {
                _speedManager = FindFirstObjectByType<SteamRush.Track.WorldSpeedManager>();
            }
        }

        private void Update()
        {
            if (_speedManager == null)
            {
                _speedManager = FindFirstObjectByType<SteamRush.Track.WorldSpeedManager>();
            }

            transform.position += Vector3.back * (crossSpeed * Time.deltaTime);

            if (transform.position.z <= -despawnZThreshold)
            {
                Destroy(gameObject);
            }
        }


        public override void OnHitPlayer(GameObject player)
        {
        }
    }
}
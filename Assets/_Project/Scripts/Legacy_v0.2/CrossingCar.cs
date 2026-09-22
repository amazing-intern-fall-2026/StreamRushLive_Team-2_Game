using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    public class CrossingCar : ObstacleBase
    {
        [SerializeField] private float crossSpeed = 6f;
        [SerializeField] private float despawnZThreshold = 15f;
        private void Update()
        {
            transform.position += Vector3.back * (crossSpeed * Time.deltaTime);

            if (transform.position.z <= -despawnZThreshold)
            {
                Destroy(gameObject);
            }
        }


        public override void OnHitPlayer(GameObject player)
        {
            Debug.Log($"Crossing Car va chạm Player — cần trừ {energyPenaltyPercent}% Energy.", this);
        }
    }
}
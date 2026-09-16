using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    public class BouncingBoulderObstacle : ObstacleBase
    {
        [SerializeField] private float bounceHeight = 1.8f;
        [SerializeField] private float bouncePeriod = 0.8f;
        [SerializeField] private float rollSpeed = 3.5f;

        private float _baseY;
        private float _elapsedTime;

        private void Awake()
        {
            _baseY = transform.position.y;
        }

        private void Update()
        {
            _elapsedTime += Time.deltaTime;

            Vector3 position = transform.position;
            position.x -= rollSpeed * Time.deltaTime;
            position.y = _baseY + bounceHeight * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * _elapsedTime / bouncePeriod));
            transform.position = position;
        }

        public override void OnHitPlayer(GameObject player)
        {
            Debug.Log($"Bouncing Boulder va chạm Player — cần trừ {energyPenaltyPercent}% Energy.", this);
        }
    }
}
using System.Collections;
using UnityEngine;
using SteamRush.Track;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Stop Sign obstacle (GDD v1.2 Section 2):
    /// Spans across track. On collision, forces world scroll speed to 0 for stopDuration (default 1.0s),
    /// then smoothly accelerates the world back to normal speed.
    /// </summary>
    public class StopSignObstacle : ObstacleBase
    {
        [Header("Stop Sign Settings")]
        [Tooltip("Duration the world stops scrolling when hitting the STOP sign.")]
        [SerializeField] private float stopDuration = 1.0f;

        private WorldSpeedManager _speedManager;
        private MovingWorldObject _movingWorldObject;

        private void Awake()
        {
            obstacleType = ObstacleType.StopSign;
            _movingWorldObject = GetComponent<MovingWorldObject>();
        }

        private void Start()
        {
            if (_speedManager == null)
            {
                _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            }
        }

        private void Update()
        {
            // Only move manually if MovingWorldObject is absent to prevent double movement
            if (_movingWorldObject == null)
            {
                float speed = _speedManager != null ? _speedManager.CurrentSpeed : 0f;
                transform.position += Vector3.left * (speed * Time.deltaTime);
            }
        }

        public override void OnHitPlayer(GameObject player)
        {
            if (_speedManager != null)
            {
                StartCoroutine(HandleStopAndResume());
            }
        }

        private IEnumerator HandleStopAndResume()
        {
            float normalSpeed = _speedManager.CurrentSpeed > 0f ? _speedManager.CurrentSpeed : 10f;
            _speedManager.CurrentSpeed = 0f;

            yield return new WaitForSeconds(stopDuration);

            // Recover world scroll speed from 0 to normal speed over 1.0s
            float elapsed = 0f;
            float recoveryDuration = 1.0f;
            while (elapsed < recoveryDuration)
            {
                elapsed += Time.deltaTime;
                if (_speedManager != null)
                {
                    _speedManager.CurrentSpeed = Mathf.Lerp(0f, normalSpeed, elapsed / recoveryDuration);
                }
                yield return null;
            }

            if (_speedManager != null)
            {
                _speedManager.CurrentSpeed = normalSpeed;
            }
        }
    }
}
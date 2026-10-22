using System;
using System.Collections;
using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Single Responsibility: Spawns, positions, scales, blinks, and cleans up
    /// the warning laser indicator line on an active obstacle lane prior to vehicle appearance.
    /// </summary>
    public class ObstacleLaserWarningService
    {
        private readonly MonoBehaviour _coroutineHost;

        public ObstacleLaserWarningService(MonoBehaviour coroutineHost)
        {
            _coroutineHost = coroutineHost;
        }

        public GameObject SpawnLaserIndicator(
            GameObject laserPrefab,
            Transform playerRef,
            float selectedLaneZ,
            float spawnDistanceAhead)
        {
            if (laserPrefab == null) return null;

            float playerX = playerRef != null ? playerRef.position.x : 0f;
            Vector3 warningPosition = new Vector3(
                playerX + (spawnDistanceAhead * 0.5f),
                0.05f,
                selectedLaneZ);

            GameObject laserInstance = UnityEngine.Object.Instantiate(
                laserPrefab,
                warningPosition,
                Quaternion.identity);

            // Disable colliders on warning laser so player doesn't collide with it
            var colliders = laserInstance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = false;
            }

            // Scale laser along run axis X to form a clear lane warning line
            laserInstance.transform.localScale = new Vector3(spawnDistanceAhead, 0.05f, 2.2f);

            // Play warning siren sound
            AudioManager.Instance?.PlaySFX(SFXType.WarningSiren);

            return laserInstance;
        }

        public Coroutine StartLaserCountdown(
            ActiveObstacle obstacle,
            GameObject laserInstance,
            Transform playerRef,
            float selectedLaneZ,
            float spawnDistanceAhead,
            float duration,
            float blinkInterval,
            Func<bool> isSpawningLocked,
            Action onComplete)
        {
            if (_coroutineHost == null) return null;

            return _coroutineHost.StartCoroutine(BlinkLaserRoutine(
                obstacle,
                laserInstance,
                playerRef,
                selectedLaneZ,
                spawnDistanceAhead,
                duration,
                blinkInterval,
                isSpawningLocked,
                onComplete));
        }

        private IEnumerator BlinkLaserRoutine(
            ActiveObstacle obstacle,
            GameObject laserInstance,
            Transform playerRef,
            float selectedLaneZ,
            float spawnDistanceAhead,
            float duration,
            float blinkInterval,
            Func<bool> isSpawningLocked,
            Action onComplete)
        {
            Renderer[] renderers = laserInstance != null ? laserInstance.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            float elapsed = 0f;
            bool isVisible = true;
            float interval = Mathf.Max(0.01f, blinkInterval);

            while (elapsed < duration)
            {
                if ((isSpawningLocked != null && isSpawningLocked()) || laserInstance == null)
                {
                    if (laserInstance != null) UnityEngine.Object.Destroy(laserInstance);
                    if (obstacle != null)
                    {
                        obstacle.LaserInstance = null;
                        obstacle.IsPendingSpawn = false;
                    }
                    yield break;
                }

                if (playerRef != null && laserInstance != null)
                {
                    laserInstance.transform.position = new Vector3(
                        playerRef.position.x + (spawnDistanceAhead * 0.5f),
                        0.05f,
                        selectedLaneZ);
                }

                SetRenderersEnabled(renderers, isVisible);
                isVisible = !isVisible;

                float waitTime = Mathf.Min(interval, duration - elapsed);
                yield return new WaitForSeconds(waitTime);
                elapsed += waitTime;
            }

            if (laserInstance != null)
            {
                UnityEngine.Object.Destroy(laserInstance);
            }

            if (obstacle != null)
            {
                obstacle.LaserInstance = null;
            }

            if (isSpawningLocked != null && isSpawningLocked())
            {
                if (obstacle != null) obstacle.IsPendingSpawn = false;
                yield break;
            }

            onComplete?.Invoke();
        }

        private static void SetRenderersEnabled(Renderer[] renderers, bool isEnabled)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = isEnabled;
                }
            }
        }
    }
}

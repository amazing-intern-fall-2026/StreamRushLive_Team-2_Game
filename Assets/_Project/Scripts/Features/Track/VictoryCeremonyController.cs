using System.Collections;
using UnityEngine;
using SteamRush.Features.Runner;
using SteamRush.Features.UI.Views;
using StreamRushLive.Features.Spawning;

namespace SteamRush.Track
{
    // GDD v1.4.1 muc 7: dieu phoi toan bo Le An Mung khi Runner bang qua Cong Ve Dich - CHI goi
    // API cong khai da chuan bi san o WorldSpeedManager/SingleObstacleSpawner/ChatLaneRunnerController
    // (Buoc 2-5), khong dung lai logic rieng cua tung class do (Decoupling - AGENTS.md muc 9).
    public class VictoryCeremonyController : MonoBehaviour
    {
        [SerializeField] private FinishLineArchway finishLineArchway;
        [SerializeField] private WorldSpeedManager worldSpeedManager;
        [SerializeField] private SingleObstacleSpawner obstacleSpawner;
        [SerializeField] private ChatLaneRunnerController runnerController;
        [SerializeField] private GiftDanceController giftDanceController;
        [SerializeField] private ElapsedTimeStopwatch stopwatch;
        [SerializeField] private TrackProgressTracker progressTracker;
        [SerializeField] private VictoryPopupController victoryPopup;
        [SerializeField] private Transform playerReference;

        [Header("Confetti VFX (Lana Studio Hyper Casual FX)")]
        [SerializeField] private GameObject confettiBlastPrefab;
        [SerializeField] private GameObject confettiDirectionalPrefab;
        [Tooltip("Khoang cach giua 2 dot ban confetti lien tiep (giay).")]
        [SerializeField] private float confettiInterval = 1.5f;
        [Tooltip("So dot ban confetti lien tiep de tao cam giac 'ngop troi' thay vi 1 phat roi tat.")]
        [SerializeField] private int confettiBursts = 5;
        [SerializeField] private float confettiLifetime = 4f;

        [Header("Timing")]
        [SerializeField] private float victoryDecelDuration = 1.5f;
        [Tooltip("Cho World giam toc xong roi moi hien popup, tranh popup bat len dung luc con giat.")]
        [SerializeField] private float popupDelay = 1.5f;

        private bool _triggered;

        private void Awake()
        {
            if (finishLineArchway == null) finishLineArchway = FindFirstObjectByType<FinishLineArchway>();
            if (worldSpeedManager == null) worldSpeedManager = FindFirstObjectByType<WorldSpeedManager>() ?? WorldSpeedManager.Instance;
            if (obstacleSpawner == null) obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
            if (runnerController == null) runnerController = FindFirstObjectByType<ChatLaneRunnerController>();
            if (giftDanceController == null) giftDanceController = FindFirstObjectByType<GiftDanceController>();
            if (stopwatch == null) stopwatch = FindFirstObjectByType<ElapsedTimeStopwatch>();
            if (progressTracker == null) progressTracker = FindFirstObjectByType<TrackProgressTracker>();
            if (playerReference == null && runnerController != null) playerReference = runnerController.transform;
        }

        private void OnEnable()
        {
            if (finishLineArchway != null)
            {
                finishLineArchway.RunnerCrossedFinishLine += HandleRunnerCrossedFinishLine;
            }
        }

        private void OnDisable()
        {
            if (finishLineArchway != null)
            {
                finishLineArchway.RunnerCrossedFinishLine -= HandleRunnerCrossedFinishLine;
            }
        }

        private void HandleRunnerCrossedFinishLine()
        {
            if (_triggered) return;
            _triggered = true;

            Debug.Log("[VictoryCeremony] Runner ve dich! Bat dau Le An Mung.");

            worldSpeedManager?.TriggerVictoryStop(victoryDecelDuration);
            obstacleSpawner?.SetSpawningLocked(true);
            runnerController?.SetControlsLocked(true);
            stopwatch?.StopTimer();
            giftDanceController?.TriggerVictoryDance(9999f);

            StartCoroutine(ConfettiRoutine());
            StartCoroutine(ShowPopupDelayed());
        }

        [ContextMenu("Debug Trigger Victory")]
        public void TriggerVictory()
        {
            HandleRunnerCrossedFinishLine();
        }

        private IEnumerator ConfettiRoutine()
        {
            for (int i = 0; i < confettiBursts; i++)
            {
                SpawnConfettiBurst();
                yield return new WaitForSeconds(confettiInterval);
            }
        }

        private void SpawnConfettiBurst()
        {
            if (playerReference == null) return;

            Vector3 center = playerReference.position + Vector3.up * 1.5f;
            // Ban 2 ben duong (GDD: "ngop troi hai ben duong") - blast ben nay, directional ben kia.
            SpawnOneConfetti(confettiBlastPrefab, center + new Vector3(0f, 0f, 2.5f));
            SpawnOneConfetti(confettiDirectionalPrefab, center + new Vector3(0f, 0f, -2.5f));
        }

        private void SpawnOneConfetti(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return;
            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            Destroy(instance, confettiLifetime);
        }

        private IEnumerator ShowPopupDelayed()
        {
            yield return new WaitForSeconds(popupDelay);

            if (victoryPopup == null || progressTracker == null) yield break;

            string timeText = stopwatch != null ? stopwatch.CurrentDisplayText : "00:00";
            victoryPopup.Show(progressTracker.TotalDistanceMeters, progressTracker.GoalDistanceMeters, timeText);
        }
    }
}

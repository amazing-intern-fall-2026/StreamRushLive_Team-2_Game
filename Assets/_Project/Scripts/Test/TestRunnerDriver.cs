using UnityEngine;
using SteamRush.Track;
using SteamRush.Relay;

public class TestRunnerDriver : MonoBehaviour
{
    [SerializeField] private TrackProgressTracker _progressTracker;
    [SerializeField] private TrackTileLooper _tileLooper;
    [SerializeField] private RelayQueueManager _relayQueueManager;
    [SerializeField] private float _fakeSpeedMetersPerSecond = 20f;

    private void Start()
    {
        _progressTracker.ProgressChanged.AddListener(OnProgressChanged);

        _relayQueueManager.EnqueueFollower("follower_A");
        _relayQueueManager.EnqueueFollower("follower_B");
        _relayQueueManager.EnqueueFollower("follower_C");
    }

    private void Update()
    {
        float distanceThisFrame = _fakeSpeedMetersPerSecond * Time.deltaTime;
        _progressTracker.AddDistance(distanceThisFrame);
       
    }

    private void OnProgressChanged(float leg, float total, float goalProgress)
    {
        Debug.Log($"Progress: leg={leg:F1}m total={total:F1}m goal={goalProgress:P1}");
    }
}
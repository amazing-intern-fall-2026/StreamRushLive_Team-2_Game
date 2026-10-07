using UnityEngine;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Attached to roadside waiting proxy. Detects when the runner contacts the 12m trigger
    /// (OnTriggerEnter) to initiate baton handover precisely.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class RoadsideHandoverTrigger : MonoBehaviour
    {
        private ChatRunnerQueueManager _queueManager;
        private bool _hasTriggered;

        public void Initialize(ChatRunnerQueueManager queueManager, float laneWidthCoverage = 12f)
        {
            _queueManager = queueManager;

            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.5f, 3f, laneWidthCoverage);
            box.center = new Vector3(0f, 1.5f, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasTriggered) return;

            bool isPlayer = other.CompareTag("Player") 
                || other.GetComponentInParent<RunnerController>() != null
                || other.GetComponentInParent<ChatLaneRunnerController>() != null
                || other.GetComponentInParent<RunnerCollisionHandler>() != null;

            if (!isPlayer) return;

            _hasTriggered = true;
            Debug.Log("[RoadsideHandoverTrigger] Runner entered 12m Trigger -> Initiating baton pass!");
            _queueManager?.NotifyRunnerReachedProxy();
        }
    }
}
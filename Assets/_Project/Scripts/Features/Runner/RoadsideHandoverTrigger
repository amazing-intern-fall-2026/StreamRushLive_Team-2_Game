using UnityEngine;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Gắn vào Model đứng chờ bên lề đường (Roadside Proxy). Phát hiện chính xác thời
    /// khắc Runner chạy chạm tới (OnTriggerEnter) để kích hoạt chuyển gậy, thay vì so
    /// sánh khoảng cách X mỗi frame — đảm bảo đổi trang phục/bảng tên đúng lúc 2 nhân
    /// vật chạm mặt nhau.
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

            bool isPlayer = other.CompareTag("Player") || other.GetComponentInParent<RunnerController>() != null;
            if (!isPlayer) return;

            _hasTriggered = true;
            _queueManager?.NotifyRunnerReachedProxy();
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using SteamRush.Track;
using SteamRush.Features.UI;
using SteamRush.Features.Runner;

namespace SteamRush.Relay
{
    // Orchestrator: Cơ chế "In-Place Relay Handover" (GDD v1.2 mục 6).
    // CHỦ ĐỘNG KHÔNG sửa TrackProgressTracker.cs - chỉ subscribe UnityEvent có sẵn
    // (ProgressChanged) theo đúng README của Track, để tránh đụng code module người khác.
    // RelayQueueManager.cs chỉ được thêm ĐÚNG 1 method mới (PeekNextFollower - chỉ đọc, không
    // dequeue), không đổi logic cũ nào - đã xin phép trước khi sửa.
    //
    // Đánh đổi còn lại (do không dùng va chạm vật lý thật):
    // - Proxy không có Collider để tránh bị RunnerCollisionHandler hiểu nhầm là Obstacle/Buff.
    //
    // spawnAheadDistanceMeters vừa là mốc TRIGGER (còn bao nhiêu m thì spawn) vừa là khoảng cách ĐẶT
    // VỊ TRÍ Proxy - CỐ TÌNH dùng chung 1 biến (không tách 2 biến khác nhau) vì GDD mục 6.1 ghi rõ
    // Proxy phải "đứng chờ sẵn ở mốc [relay]" - nếu tách riêng 2 khoảng cách khác nhau, vị trí spawn
    // sẽ lệch RA NGOÀI mốc thật (không còn "ở mốc" nữa). Công thức bắt buộc để Proxy đứng ĐÚNG mốc:
    // vị_trí_spawn = vị_trí_Runner + (quãng_đường_còn_lại_lúc_trigger) => 2 khoảng cách này PHẢI BẰNG
    // NHAU.
    //
    // Giá trị mặc định đổi từ 10m (đúng chữ GDD: "còn 10m/đạt 90m") lên 25m kể từ bản cập nhật 15/09:
    // đo thực tế trên Prototype.unity cho thấy 10m khiến Proxy hiện ra NGAY TRONG khung hình Camera
    // (viewport x=0.63) và cả vòng đời spawn->bàn giao chỉ ~0.8s ở tốc độ 12m/s - cảm giác như "bàn
    // giao cho ma" vì mắt người không kịp nhận ra model. 25m đo được là đủ để spawn ngoài khung hình,
    // cho ~2s để thấy Runner "chạy tới gần" Proxy đúng như Leader mô tả. Đánh đổi: mốc trigger lệch
    // khỏi con số chữ "10m" trong GDD, nhưng bù lại Proxy luôn đứng ĐÚNG THẬT tại mốc relay (thay vì
    // lệch ra ngoài như khi tách 2 biến), và vì khoảng cách Proxy cần chạy tới == quãng đường leg còn
    // lại lúc trigger, Proxy sẽ chạm Runner CHÍNH XÁC đúng lúc leg đạt mốc 100%/200m - khớp thời điểm
    // với lúc RelayQueueManager.HandleRelayCompleted() cập nhật Nameplate/Avatar (không cần sửa thêm
    // RelayQueueManager.cs để đồng bộ).
    //
    // Dù vậy, dequeue hàng đợi/reset quãng đường vẫn có thể tới TRƯỚC khoảnh khắc Proxy chạm Runner
    // vài phần trăm giây (sai số float, hoặc GDD mục 6.4 hoãn do IsHandlingHit) - nên phần HIỂN THỊ
    // (đổi mesh Runner, hủy Proxy) vẫn luôn chờ HasProxyArrived() == true ở Update(), không dựa thẳng
    // vào sự kiện FollowerNameChanged.
    //
    // Edge case GDD mục 6.4 (va chạm ngay lúc bàn giao, vd tại 99m): TrackProgressTracker vẫn tự
    // bắn FollowerNameChanged như bình thường (không sửa được từ đây), nhưng CHỈ HOÃN việc thực sự
    // đổi mesh/tên tới khi RunnerCollisionHandler.IsHandlingHit (property public có sẵn, chỉ đọc)
    // trở lại false - tránh đổi mesh giữa lúc Runner đang chạy animation ngã.
    public class BatonHandoverController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TrackProgressTracker progressTracker;
        [SerializeField] private RelayQueueManager relayQueueManager;
        [SerializeField] private HUDManager hudManager;
        [SerializeField] private WorldSpeedManager worldSpeedManager;
        [SerializeField] private Transform runnerTransform;

        [Header("Proxy")]
        [SerializeField] private HandoverProxyController proxyPrefab;
        [Tooltip("Vừa là mốc TRIGGER (còn bao nhiêu m thì spawn) vừa là khoảng cách đặt VỊ TRÍ Proxy phía trước Runner - dùng chung 1 số để Proxy luôn đứng ĐÚNG tại mốc relay (GDD mục 6.1). Mặc định 25m (thay vì đúng 10m theo chữ GDD) vì đo thực tế trên Prototype.unity: 10m khiến Proxy hiện ra ngay trong khung hình Camera, giống 'bàn giao cho ma'.")]
        [SerializeField] private float spawnAheadDistanceMeters = 25f;
        [Tooltip("Khoảng cách X còn lại giữa Proxy và Runner được coi là đã 'chạm tới' để thực sự đổi mesh/tên (m).")]
        [SerializeField] private float arrivalThresholdMeters = 0.5f;
        [SerializeField] private string waitingLabel = "Người chơi tiếp theo...";

        [Header("Model Swap (đổi trang phục Runner)")]
        [Tooltip("Danh sách GameObject mesh trang phục con dùng chung 1 rig trên Runner - bật/tắt để đổi 'người chơi mới'. Chờ mapping avatar->outfit thật từ StreamIntegration, tạm thời chọn ngẫu nhiên.")]
        [SerializeField] private List<GameObject> outfitVariants = new List<GameObject>();

        private HandoverProxyController _activeProxy;
        private bool _proxySpawnedForCurrentLeg;
        private GameObject _currentOutfit;

        // Chỉ đọc IsHandlingHit từ RunnerCollisionHandler (KHÔNG sửa file đó) để hoãn bàn giao
        // đúng theo GDD mục 6.4 khi Runner đang bị va chạm/ngã ngay lúc chuẩn bị bàn giao.
        private RunnerCollisionHandler _runnerCollisionHandler;
        private bool _hasPendingHandover;
        private string _pendingFollowerName;

        private void Awake()
        {
            if (progressTracker == null) progressTracker = FindFirstObjectByType<TrackProgressTracker>();
            if (relayQueueManager == null) relayQueueManager = FindFirstObjectByType<RelayQueueManager>();
            if (hudManager == null) hudManager = FindFirstObjectByType<HUDManager>();
            if (worldSpeedManager == null) worldSpeedManager = FindFirstObjectByType<WorldSpeedManager>();

            if (runnerTransform != null)
            {
                _runnerCollisionHandler = runnerTransform.GetComponent<RunnerCollisionHandler>();
            }

            // Outfit đang bật sẵn (mặc định) = phần tử đầu tiên đang active trong danh sách.
            _currentOutfit = outfitVariants.Find(o => o != null && o.activeSelf);

            // Gán Runner hiện tại làm target cho Nameplate bám theo đầu - chưa có module nào khác
            // gọi HUDManager.UpdateRunnerTarget() nên tự làm ở đây để Nameplate hoạt động khi test.
            if (hudManager != null && runnerTransform != null)
            {
                hudManager.UpdateRunnerTarget(runnerTransform);
            }
        }

        private void OnEnable()
        {
            if (progressTracker != null)
            {
                progressTracker.ProgressChanged.AddListener(OnProgressChanged);
            }

            if (relayQueueManager != null)
            {
                relayQueueManager.FollowerNameChanged.AddListener(OnFollowerNameChanged);
            }
        }

        private void OnDisable()
        {
            if (progressTracker != null)
            {
                progressTracker.ProgressChanged.RemoveListener(OnProgressChanged);
            }

            if (relayQueueManager != null)
            {
                relayQueueManager.FollowerNameChanged.RemoveListener(OnFollowerNameChanged);
            }
        }

        private void OnProgressChanged(float legDistance, float totalDistance, float goalProgress)
        {
            if (progressTracker == null) return;

            // Chặng mới bắt đầu (leg vừa reset về gần 0) - cho phép spawn Proxy lại cho chặng này.
            if (legDistance < 1f)
            {
                _proxySpawnedForCurrentLeg = false;
            }

            if (_proxySpawnedForCurrentLeg) return;

            float triggerDistance = progressTracker.RelayDistanceMeters - spawnAheadDistanceMeters;
            if (legDistance >= triggerDistance)
            {
                _proxySpawnedForCurrentLeg = true;
                TrySpawnProxy();
            }
        }

        private void TrySpawnProxy()
        {
            if (proxyPrefab == null || runnerTransform == null)
            {
                Debug.LogWarning("[BatonHandoverController] Chưa gán proxyPrefab hoặc runnerTransform - bỏ qua spawn Proxy.");
                return;
            }

            // Hàng đợi trống = Solo Marathon Mode (GDD mục 8) - không có ai bàn giao, không spawn Proxy.
            if (relayQueueManager == null || relayQueueManager.Count == 0)
            {
                Debug.Log("[BatonHandoverController] Hàng đợi trống - Solo Marathon Mode, bỏ qua bàn giao.");
                return;
            }

            // PeekNextFollower() đọc trước tên thật mà không dequeue - hiện đúng tên ngay từ lúc
            // spawn (GDD mục 6.1), waitingLabel chỉ còn dùng làm fallback phòng khi queue trống.
            string displayName = relayQueueManager.PeekNextFollower() ?? waitingLabel;

            Vector3 spawnPosition = runnerTransform.position + Vector3.right * spawnAheadDistanceMeters;
            _activeProxy = Instantiate(proxyPrefab, spawnPosition, Quaternion.identity);
            _activeProxy.SetInfo(displayName, null);

            MovingWorldObject mover = _activeProxy.gameObject.AddComponent<MovingWorldObject>();
            if (worldSpeedManager != null)
            {
                mover.Initialize(worldSpeedManager);
            }
        }

        // Bắn kèm tên ĐÚNG, chỉ bắn SAU khi RelayQueueManager đã dequeue xong - dùng làm tín hiệu
        // "đã bàn giao" chính xác, không dựa vào RelayCompleted (thứ tự gọi giữa các listener của
        // TrackProgressTracker.RelayCompleted không đảm bảo, dễ đọc tên chưa kịp cập nhật).
        private void OnFollowerNameChanged(string followerId)
        {
            // Luôn hoãn, không đổi mesh/tên ngay tại đây nữa: dù spawnAheadDistanceMeters dùng chung
            // cho cả trigger lẫn vị trí (Proxy sẽ chạm Runner gần như đúng lúc mốc 100% này bắn ra),
            // vẫn có thể lệch vài phần trăm giây (sai số float, hoặc đang hoãn do IsHandlingHit ở GDD
            // mục 6.4) - nên đổi mesh thực sự chỉ diễn ra khi HasProxyArrived() == true (xem Update()),
            // không dựa thẳng vào sự kiện số học này.
            _hasPendingHandover = true;
            _pendingFollowerName = followerId;
        }

        private void Update()
        {
            if (!_hasPendingHandover) return;

            if (_runnerCollisionHandler != null && _runnerCollisionHandler.IsHandlingHit) return;

            if (!HasProxyArrived()) return;

            _hasPendingHandover = false;
            CompleteHandover(_pendingFollowerName);
        }

        // Proxy được xem là "chạm tới" khi khoảng cách X còn lại với Runner nhỏ hơn ngưỡng cho phép.
        // Nếu Proxy đã bị hủy vì lý do khác (không nên xảy ra trong luồng bình thường) thì coi như
        // đã tới để không bao giờ bị kẹt _hasPendingHandover mãi mãi.
        private bool HasProxyArrived()
        {
            if (_activeProxy == null) return true;

            float distanceX = Mathf.Abs(_activeProxy.transform.position.x - runnerTransform.position.x);
            return distanceX <= arrivalThresholdMeters;
        }

        private void CompleteHandover(string followerName)
        {
            SwapOutfit();

            if (_activeProxy != null)
            {
                Destroy(_activeProxy.gameObject);
                _activeProxy = null;
            }

            Debug.Log($"[BatonHandoverController] Chuyển gậy In-Place thành công cho: {followerName}");
        }

        private void SwapOutfit()
        {
            if (outfitVariants == null || outfitVariants.Count == 0)
            {
                Debug.Log("[BatonHandoverController] Chưa có danh sách outfit - bỏ qua đổi mesh.");
                return;
            }

            List<GameObject> candidates = outfitVariants.FindAll(o => o != null && o != _currentOutfit);
            if (candidates.Count == 0) return;

            GameObject next = candidates[Random.Range(0, candidates.Count)];

            if (_currentOutfit != null)
            {
                _currentOutfit.SetActive(false);
            }

            next.SetActive(true);
            _currentOutfit = next;

            // TODO: chờ dữ liệu mapping avatar -> outfit thật từ StreamIntegration, hiện tại
            // chọn ngẫu nhiên trong các outfit có sẵn trên rig để tạo cảm giác "người chơi mới".
        }
    }
}

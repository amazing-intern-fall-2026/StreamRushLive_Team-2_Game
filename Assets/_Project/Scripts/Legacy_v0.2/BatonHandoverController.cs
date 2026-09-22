using System.Collections.Generic;
using UnityEngine;
using SteamRush.Track;
using SteamRush.Features.UI;
using SteamRush.Features.Runner;

namespace SteamRush.Relay
{
    /// <summary>
    /// Điều phối cơ chế chuyển gậy tiếp sức (In-Place Relay Handover) tại các mốc cự ly (GDD v1.2 Mục 6).
    /// Tự động spawn nhân vật tiếp theo từ outfitVariants trên đường chạy và hoán đổi trang phục cho Runner khi hoàn tất bàn giao.
    /// </summary>
    public class BatonHandoverController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TrackProgressTracker progressTracker;
        [SerializeField] private RelayQueueManager relayQueueManager;
        [SerializeField] private HUDManager hudManager;
        [SerializeField] private WorldSpeedManager worldSpeedManager;
        [SerializeField] private Transform runnerTransform;

        [Header("Proxy")]
        [Tooltip("Prefab Nameplate hiển thị tên Viewer nổi trên đầu nhân vật bàn giao (Proxy).")]
        [SerializeField] private GameObject nameplatePrefab;
        [Tooltip("Khoảng cách X coi như đã chạm tới để thực hiện bàn giao (m).")]
        [SerializeField] private float arrivalThresholdMeters = 0.5f;
        [SerializeField] private string waitingLabel = "Người chơi tiếp theo...";

        [Header("Model Swap")]
        [Tooltip("Danh sách Model Prefab - mỗi lần bàn giao sẽ bốc ngẫu nhiên 1 model để spawn Proxy và hoán đổi trang phục cho Runner.")]
        [SerializeField] private List<GameObject> outfitVariants = new List<GameObject>();

        private HandoverProxyController _activeProxy;
        private bool _proxySpawnedForCurrentLeg;
        private string _currentOutfitName;
        private GameObject _pendingNextOutfit;
        private float _lastTotalDistance;

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

                // Xác định outfit hiện tại đang bật trên Runner
                foreach (Transform child in runnerTransform)
                {
                    if (child.name.StartsWith("Character_") && child.gameObject.activeSelf)
                    {
                        _currentOutfitName = CleanOutfitName(child.name);
                        break;
                    }
                }

                // Nếu danh sách outfitVariants chưa được kéo thả trên Inspector, tự động gom các mesh con trên Runner làm fallback
                if (outfitVariants == null || outfitVariants.Count == 0)
                {
                    outfitVariants = new List<GameObject>();
                    foreach (Transform child in runnerTransform)
                    {
                        if (child.name.StartsWith("Character_") && child.GetComponent<SkinnedMeshRenderer>() != null)
                        {
                            outfitVariants.Add(child.gameObject);
                        }
                    }
                }
            }

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
                progressTracker.RelayCompleted.AddListener(OnRelayCompleted);
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
                progressTracker.RelayCompleted.RemoveListener(OnRelayCompleted);
            }

            if (relayQueueManager != null)
            {
                relayQueueManager.FollowerNameChanged.RemoveListener(OnFollowerNameChanged);
            }
        }

        private void OnRelayCompleted(int relayNumber)
        {
            // Đảm bảo reset trạng thái cho phép spawn Proxy cho chặng kế tiếp
            _proxySpawnedForCurrentLeg = false;
        }

        private void OnProgressChanged(float legDistance, float totalDistance, float goalProgress)
        {
            if (progressTracker == null) return;

            // Xử lý khi bị trừ quãng đường do va chạm vật cản (GDD mục 6):
            // Nếu totalDistance giảm, đẩy Proxy lùi lại (về phía +X) đúng bằng cự ly bị trừ để mốc 100m luôn đồng bộ
            if (_lastTotalDistance > 0f && totalDistance < _lastTotalDistance)
            {
                float distanceLost = _lastTotalDistance - totalDistance;
                if (_activeProxy != null)
                {
                    _activeProxy.transform.position += Vector3.right * distanceLost;
                }
            }
            _lastTotalDistance = totalDistance;

            // Chặng mới bắt đầu hoặc sau khi hoàn tất chặng
            if (legDistance < 1f)
            {
                _proxySpawnedForCurrentLeg = false;
            }

            if (_proxySpawnedForCurrentLeg && _activeProxy != null) return;

            float aheadDistance = GetSpawnAheadDistance();
            float triggerDistance = progressTracker.RelayDistanceMeters - aheadDistance;
            if (legDistance >= triggerDistance)
            {
                _proxySpawnedForCurrentLeg = true;
                TrySpawnProxy();
            }
        }

        public float GetSpawnAheadDistance()
        {
            if (runnerTransform != null)
            {
                float diffX = transform.position.x - runnerTransform.position.x;
                if (diffX > 0f) return diffX;
            }
            return 25f;
        }

        private void TrySpawnProxy()
        {
            if (runnerTransform == null)
            {
                Debug.LogWarning("[BatonHandoverController] Chưa gán runnerTransform - bỏ qua spawn Proxy.");
                return;
            }

            // Hàng đợi trống = Solo Marathon Mode (GDD mục 8) - không có ai bàn giao, không spawn Proxy.
            if (relayQueueManager == null || relayQueueManager.Count == 0)
            {
                Debug.Log("[BatonHandoverController] Hàng đợi trống - Solo Marathon Mode, bỏ qua bàn giao.");
                return;
            }

            string displayName = relayQueueManager.PeekNextFollower() ?? waitingLabel;

            // Random chọn trước outfit tiếp theo từ danh sách outfitVariants
            PickPendingNextOutfit();

            if (_pendingNextOutfit == null)
            {
                Debug.LogWarning("[BatonHandoverController] outfitVariants đang trống - không thể spawn Proxy.");
                return;
            }

            Vector3 spawnPosition = transform.position;
            Quaternion spawnRotation = runnerTransform.rotation;

            // 1. Instantiate trực tiếp model prefab được chọn ngẫu nhiên từ outfitVariants!
            GameObject proxyObj = Instantiate(_pendingNextOutfit, spawnPosition, spawnRotation);
            proxyObj.name = $"Proxy_{_pendingNextOutfit.name}";

            // 2. Vô hiệu hoá Collider trên Proxy để tránh va chạm vật lý với Runner/Obstacle
            Collider[] colliders = proxyObj.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            // 3. Gắn HandoverProxyController
            _activeProxy = proxyObj.GetComponent<HandoverProxyController>();
            if (_activeProxy == null)
            {
                _activeProxy = proxyObj.AddComponent<HandoverProxyController>();
            }

            // 4. Gắn Nameplate hiển thị tên Viewer nổi trên đầu
            AttachNameplate(proxyObj, displayName);

            // 5. Bật Animation chạy cho Proxy nếu có Animator (Tắt Root Motion để không bị di chuyển lung tung!)
            var runnerAnim = runnerTransform.GetComponent<Animator>();
            var proxyAnimators = proxyObj.GetComponentsInChildren<Animator>();
            for (int i = 0; i < proxyAnimators.Length; i++)
            {
                proxyAnimators[i].applyRootMotion = false;
                if (runnerAnim != null)
                {
                    proxyAnimators[i].runtimeAnimatorController = runnerAnim.runtimeAnimatorController;
                }
            }

            // 6. Gắn MovingWorldObject để Proxy trôi theo tốc độ đường đua về phía Runner
            MovingWorldObject mover = proxyObj.GetComponent<MovingWorldObject>();
            if (mover == null)
            {
                mover = proxyObj.AddComponent<MovingWorldObject>();
            }

            if (worldSpeedManager != null)
            {
                mover.Initialize(worldSpeedManager);
            }
        }

        private void AttachNameplate(GameObject proxyObj, string displayName)
        {
            if (nameplatePrefab == null) return;

            GameObject nameplateObj = Instantiate(nameplatePrefab, proxyObj.transform);
            if (nameplateObj != null)
            {
                // Xoá hậu tố (Clone) để Hierarchy sạch sẽ đúng tên RelayNameplate
                nameplateObj.name = "RelayNameplate";
                nameplateObj.transform.localPosition = new Vector3(0f, 2.1f, 0f);
                
                // Khởi tạo góc xoay ban đầu hướng về camera chính
                if (Camera.main != null)
                {
                    nameplateObj.transform.rotation = Camera.main.transform.rotation;
                }

                var tmp = nameplateObj.GetComponentInChildren<TMPro.TMP_Text>();
                if (tmp != null)
                {
                    tmp.text = displayName;
                }

                if (_activeProxy != null)
                {
                    _activeProxy.SetNameplate(nameplateObj.transform, tmp);
                }
            }
        }

        private void PickPendingNextOutfit()
        {
            if (outfitVariants == null || outfitVariants.Count == 0)
            {
                _pendingNextOutfit = null;
                return;
            }

            // Loại trừ outfit hiện tại của Runner để mỗi lần bàn giao luôn đổi sang model khác
            List<GameObject> candidates = outfitVariants.FindAll(o =>
            {
                if (o == null) return false;
                string cleanName = CleanOutfitName(o.name);
                return !string.Equals(cleanName, _currentOutfitName, System.StringComparison.OrdinalIgnoreCase);
            });

            if (candidates.Count > 0)
            {
                _pendingNextOutfit = candidates[Random.Range(0, candidates.Count)];
            }
            else
            {
                _pendingNextOutfit = outfitVariants[Random.Range(0, outfitVariants.Count)];
            }
        }

        private string CleanOutfitName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "";
            string clean = rawName.Replace("(Clone)", "").Trim();
            if (clean.EndsWith("_01")) clean = clean.Substring(0, clean.Length - 3);
            return clean;
        }

        // Lắng nghe khi hoàn thành chặng và cập nhật viewer tiếp theo.
        // Hoãn bàn giao cho tới khi Proxy chạm Runner và Runner không trong trạng thái ngã.
        private void OnFollowerNameChanged(string followerId)
        {
            _hasPendingHandover = true;
            _pendingFollowerName = followerId;
        }

        private void Update()
        {
            // Bảo vệ Proxy: Không để Proxy trôi tụt lại sau lưng Runner khi đang chờ bàn giao hoặc Runner đang choáng
            if (_activeProxy != null && runnerTransform != null)
            {
                if (_activeProxy.transform.position.x < runnerTransform.position.x)
                {
                    Vector3 p = _activeProxy.transform.position;
                    p.x = runnerTransform.position.x;
                    _activeProxy.transform.position = p;
                }
            }

            if (!_hasPendingHandover) return;

            // GDD mục 6: Tạm hoãn chuyển giao cho đến khi Runner đứng dậy hoàn toàn (IsHandlingHit == false)
            if (_runnerCollisionHandler != null && _runnerCollisionHandler.IsHandlingHit) return;

            if (!HasProxyArrived()) return;

            _hasPendingHandover = false;
            CompleteHandover(_pendingFollowerName);
        }

        private bool HasProxyArrived()
        {
            if (_activeProxy == null) return true;

            // Proxy di chuyển từ +X về phía Runner. Khi deltaX <= arrivalThresholdMeters tức là đã tiếp cận/chạm.
            float deltaX = _activeProxy.transform.position.x - runnerTransform.position.x;
            return deltaX <= arrivalThresholdMeters;
        }

        private void CompleteHandover(string followerName)
        {
            // 1. Hoán đổi trang phục (model mesh)
            SwapOutfit();

            // 2. Cập nhật tên Runner trên HUD và hiển thị popup đúng thời điểm va chạm
            if (hudManager != null)
            {
                string display = !string.IsNullOrEmpty(followerName) ? followerName : waitingLabel;
                hudManager.UpdateRunnerInfo(display, null);
                hudManager.ShowStatusPopup($"Chuyển gậy: {display}!", true);
            }

            // 3. Huỷ nhân vật Proxy
            if (_activeProxy != null)
            {
                Destroy(_activeProxy.gameObject);
                _activeProxy = null;
            }

            // 4. Reset trạng thái sẵn sàng cho chặng tiếp theo
            _proxySpawnedForCurrentLeg = false;
            _hasPendingHandover = false;

            Debug.Log($"[BatonHandoverController] Chuyển gậy In-Place thành công cho: {followerName} (Đã chuyển sang model: {_currentOutfitName})");
        }

        private void SwapOutfit()
        {
            if (_pendingNextOutfit == null)
            {
                PickPendingNextOutfit();
            }

            if (_pendingNextOutfit == null)
            {
                Debug.Log("[BatonHandoverController] Chưa có danh sách outfit - bỏ qua đổi mesh.");
                return;
            }

            string targetOutfitName = CleanOutfitName(_pendingNextOutfit.name);
            bool swapped = false;

            if (runnerTransform != null)
            {
                foreach (Transform child in runnerTransform)
                {
                    if (child.name.StartsWith("Character_") && child.GetComponent<SkinnedMeshRenderer>() != null)
                    {
                        bool isMatch = string.Equals(child.name, targetOutfitName, System.StringComparison.OrdinalIgnoreCase);
                        child.gameObject.SetActive(isMatch);
                        if (isMatch)
                        {
                            swapped = true;
                            _currentOutfitName = child.name;
                        }
                    }
                }
            }

            if (swapped)
            {
                Debug.Log($"[BatonHandoverController] Đã đổi Runner sang outfit: {_currentOutfitName}");
            }
            else
            {
                Debug.LogWarning($"[BatonHandoverController] Không tìm thấy mesh con trên Runner khớp với outfit '{targetOutfitName}'");
            }

            _pendingNextOutfit = null;
        }
    }
}

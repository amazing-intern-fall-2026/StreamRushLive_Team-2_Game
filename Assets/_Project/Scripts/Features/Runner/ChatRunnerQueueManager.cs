using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SteamRush.Track;
using SteamRush.Features.StreamIntegration;
using SteamRush.Features.UI;
using SteamRush.Core;

namespace SteamRush.Features.Runner
{
    // Quan ly hang doi Follower cho ChatRunner (GDD v1.3 muc 1 & 3). Chi Follower qua
    // FollowerGate moi duoc vao hang doi. Het 1 chang (100m) hoac khi Runner chet (PlayerDeathEvent)
    // thi chuyen giao luot chay cho Follower tiep theo trong hang doi.
    public class ChatRunnerQueueManager : MonoBehaviour
    {
        [SerializeField] private WorldSpeedManager _worldSpeedManager;
        [SerializeField] private float _legDistanceMeters = 100f;

        [Header("Initial Queue Mock")]
        [SerializeField] private string _initialRunnerId = "Streamer_Alex";
        [SerializeField] private List<string> _initialFollowers = new List<string> { "Viewer_Bao", "Viewer_Chi", "Top1_Dung", "Mod_Giang", "Gamer_Huy" };
        [Tooltip("Tu dong sinh them Follower khi hang doi het de test lien tuc ma khong bi dung.")]
        [SerializeField] private bool _autoReplenishMockQueue = true;

        [Header("HUD (facade co san - xem HUDManager.cs)")]
        [SerializeField] private HUDManager _hudManager;
        [SerializeField] private Transform _runnerTransform;
        [SerializeField] private Sprite _defaultAvatar;

        [Header("Roadside Character Handover (Thay nguoi ben duong)")]
        [Tooltip("Bật cơ chế nhân vật tiếp theo đứng chờ sẵn bên lề đường để chuyển gậy.")]
        [SerializeField] private bool _enableRoadsideHandover = true;
        [Tooltip("Khoảng cách mét phía trước Runner xuất hiện nhân vật đứng chờ.")]
        [SerializeField] private float _spawnAheadMeters = 30f;
        [Tooltip("Khoảng cách X coi như Runner đã tiếp cận nhân vật đứng bên đường để hoàn tất chuyển gậy.")]
        [SerializeField] private float _arrivalThresholdX = 0.6f;
        [Tooltip("Vị trí Z của vỉa hè bên trái.")]
        [SerializeField] private float _leftSidewalkZ = -5.8f;
        [Tooltip("Vị trí Z của vỉa hè bên phải.")]
        [SerializeField] private float _rightSidewalkZ = 5.8f;
        [Tooltip("Cao độ Y của vỉa hè.")]
        [SerializeField] private float _sidewalkY = 0.2f;
        [Tooltip("Prefab Nameplate hiển thị tên Viewer nổi trên đầu nhân vật đứng chờ.")]
        [SerializeField] private GameObject _nameplatePrefab;
        [Tooltip("Danh sách Model Prefab nhân vật từ PolygonCity.")]
        [SerializeField] private List<GameObject> _outfitVariants = new List<GameObject>();

        [Serializable] public class WaitingStateChangedEvent : UnityEvent<bool> { }
        [SerializeField] private WaitingStateChangedEvent _waitingStateChanged = new WaitingStateChangedEvent();
        public WaitingStateChangedEvent WaitingStateChanged => _waitingStateChanged;

        private readonly Queue<string> _followerQueue = new Queue<string>();
        private readonly FollowerGate _followerGate = new FollowerGate();

        private float _distanceSinceLastLeg;
        private bool _isWaitingForFollower;
        private float _speedBeforeWait;

        private GameObject _activeProxy;
        private bool _proxySpawnedForCurrentLeg;
        private string _currentOutfitName = "";
        private GameObject _pendingNextOutfit;
        private string _pendingNextRunnerId = "";

        public string CurrentRunnerId { get; private set; }
        public bool IsWaitingForFollower => _isWaitingForFollower;
        public int QueuedCount => _followerQueue.Count;
        public float LegProgress => _distanceSinceLastLeg;
        public float LegDistanceMeters => _legDistanceMeters;

        public event Action<string, int> OnRunnerChanged;

        private void Awake()
        {
            if (_worldSpeedManager == null)
            {
                _worldSpeedManager = FindFirstObjectByType<WorldSpeedManager>();
            }

            if (_runnerTransform != null)
            {
                foreach (Transform child in _runnerTransform)
                {
                    if (child.name.StartsWith("Character_") && child.gameObject.activeSelf)
                    {
                        _currentOutfitName = CleanOutfitName(child.name);
                        break;
                    }
                }
            }
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(CurrentRunnerId))
            {
                CurrentRunnerId = _initialRunnerId;
            }

            if (_initialFollowers != null)
            {
                foreach (var f in _initialFollowers)
                {
                    _followerQueue.Enqueue(f);
                }
            }

            // An bang cho khi khoi dong
            _isWaitingForFollower = false;
            _waitingStateChanged.Invoke(false);

            UpdateRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, _followerQueue.Count);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
        }

        private void OnPlayerDeath(PlayerDeathEvent evt)
        {
            _distanceSinceLastLeg = 0f;
            AdvanceToNextRunner();
        }

        // Gọi từ StreamIntegration khi có sự kiện Follow mới - chỉ nhận nếu qua được FollowerGate.
        public void TryEnqueueFollower(string userId)
        {
            if (!_followerGate.CanJoinQueue(userId))
            {
                return;
            }

            _followerQueue.Enqueue(userId);
            OnRunnerChanged?.Invoke(CurrentRunnerId, _followerQueue.Count);

            if (_isWaitingForFollower)
            {
                ResumeFromWaiting();
            }
        }

        private void Update()
        {
            if (_isWaitingForFollower || _worldSpeedManager == null)
            {
                return;
            }

            _distanceSinceLastLeg += _worldSpeedManager.CurrentSpeed * Time.deltaTime;

            if (_enableRoadsideHandover)
            {
                UpdateRoadsideHandover();
            }
            else
            {
                if (_distanceSinceLastLeg >= _legDistanceMeters)
                {
                    _distanceSinceLastLeg -= _legDistanceMeters;
                    AdvanceToNextRunner();
                }
            }
        }

        private void UpdateRoadsideHandover()
        {
            // 1. Kiểm tra khi cự ly còn cách mốc leg một khoảng _spawnAheadMeters -> spawn proxy đứng chờ bên lề đường
            float triggerDistance = Mathf.Max(0f, _legDistanceMeters - _spawnAheadMeters);
            if (_distanceSinceLastLeg >= triggerDistance && !_proxySpawnedForCurrentLeg && _activeProxy == null)
            {
                TrySpawnRoadsideProxy();
            }

            // 2. Nếu đã spawn proxy, theo dõi khi nào Runner chạy tới ngang hàng với proxy
            if (_activeProxy != null && _runnerTransform != null)
            {
                float deltaX = _activeProxy.transform.position.x - _runnerTransform.position.x;
                if (deltaX <= _arrivalThresholdX)
                {
                    ExecuteRoadsideHandover();
                }
            }
            // Fallback nếu không có proxy mà đã chạy hết cự ly
            else if (_distanceSinceLastLeg >= _legDistanceMeters)
            {
                _distanceSinceLastLeg -= _legDistanceMeters;
                AdvanceToNextRunner();
            }
        }

        private void TrySpawnRoadsideProxy()
        {
            if (_runnerTransform == null) return;

            // Xác định người tiếp theo trong hàng đợi
            if (_followerQueue.Count == 0)
            {
                if (_autoReplenishMockQueue)
                {
                    _pendingNextRunnerId = "Follower_" + UnityEngine.Random.Range(100, 999);
                    _followerQueue.Enqueue(_pendingNextRunnerId);
                }
                else
                {
                    return;
                }
            }
            else
            {
                _pendingNextRunnerId = _followerQueue.Peek();
            }

            PickPendingNextOutfit();
            if (_pendingNextOutfit == null) return;

            _proxySpawnedForCurrentLeg = true;

            // Đứng RANDOM ở 2 bên đường (vỉa hè trái hoặc phải)
            bool isLeft = UnityEngine.Random.value > 0.5f;
            float targetZ = isLeft ? _leftSidewalkZ : _rightSidewalkZ;
            // Xoay mặt vào trong hướng về đường chạy và Runner đang chạy tới
            Quaternion spawnRot = isLeft ? Quaternion.Euler(0f, 65f, 0f) : Quaternion.Euler(0f, -65f, 0f);

            Vector3 spawnPos = new Vector3(_runnerTransform.position.x + _spawnAheadMeters, _sidewalkY, targetZ);

            _activeProxy = Instantiate(_pendingNextOutfit, spawnPos, spawnRot);
            _activeProxy.name = $"RoadsideProxy_{_pendingNextOutfit.name}";

            // Vô hiệu hoá Colliders trên proxy để tránh va chạm vật lý
            foreach (var col in _activeProxy.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            // ĐỨNG YÊN (không chạy phía trước người chơi): Vô hiệu hoá Animator và hạ tay xuống tư thế đứng thư giãn tự nhiên
            var anims = _activeProxy.GetComponentsInChildren<Animator>();
            foreach (var a in anims)
            {
                a.enabled = false;
            }
            ApplyNaturalStandingPose(_activeProxy);

            // Gắn Nameplate hiển thị tên Follower tiếp theo
            AttachNameplateToProxy(_activeProxy, _pendingNextRunnerId);

            // Gắn MovingWorldObject để proxy trôi theo thế giới về phía Runner
            MovingWorldObject mover = _activeProxy.GetComponent<MovingWorldObject>();
            if (mover == null) mover = _activeProxy.AddComponent<MovingWorldObject>();
            if (_worldSpeedManager != null) mover.Initialize(_worldSpeedManager);
        }

        private void ExecuteRoadsideHandover()
        {
            if (_followerQueue.Count > 0 && _followerQueue.Peek() == _pendingNextRunnerId)
            {
                _followerQueue.Dequeue();
            }

            CurrentRunnerId = _pendingNextRunnerId;
            _distanceSinceLastLeg = 0f;
            _proxySpawnedForCurrentLeg = false;

            // 1. Hoán đổi outfit trên Runner
            SwapRunnerOutfit(_pendingNextOutfit != null ? _pendingNextOutfit.name : "");

            // 2. Cập nhật HUD & Nameplate
            UpdateRunnerHud();
            if (_hudManager != null)
            {
                _hudManager.ShowStatusPopup($"Chuyển gậy: {CurrentRunnerId}!", true);
            }

            // 3. Huỷ proxy
            if (_activeProxy != null)
            {
                Destroy(_activeProxy);
                _activeProxy = null;
            }

            OnRunnerChanged?.Invoke(CurrentRunnerId, _followerQueue.Count);
            Debug.Log($"[ChatRunnerQueueManager] Chuyển gậy thành công cho: {CurrentRunnerId}!");
        }

        private void SwapRunnerOutfit(string rawTargetName)
        {
            string targetName = CleanOutfitName(rawTargetName);
            if (string.IsNullOrEmpty(targetName) || _runnerTransform == null) return;

            foreach (Transform child in _runnerTransform)
            {
                if (child.name.StartsWith("Character_") && child.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    bool isMatch = string.Equals(CleanOutfitName(child.name), targetName, StringComparison.OrdinalIgnoreCase);
                    child.gameObject.SetActive(isMatch);
                    if (isMatch)
                    {
                        _currentOutfitName = child.name;
                    }
                }
            }

            _pendingNextOutfit = null;
        }

        private void AttachNameplateToProxy(GameObject proxyObj, string displayName)
        {
            if (_nameplatePrefab == null) return;

            GameObject nameplate = Instantiate(_nameplatePrefab, proxyObj.transform);
            nameplate.name = "RelayNameplate";
            nameplate.transform.localPosition = new Vector3(0f, 2.1f, 0f);

            var tmp = nameplate.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null)
            {
                tmp.text = displayName;
            }

            var proxyCtrl = proxyObj.AddComponent<SteamRush.Relay.HandoverProxyController>();
            proxyCtrl.SetNameplate(nameplate.transform, tmp);
        }

        private void ApplyNaturalStandingPose(GameObject proxy)
        {
            if (proxy == null) return;
            var sL = proxy.transform.Find("Root/Hips/Spine_01/Spine_02/Spine_03/Clavicle_L/Shoulder_L");
            var sR = proxy.transform.Find("Root/Hips/Spine_01/Spine_02/Spine_03/Clavicle_R/Shoulder_R");
            if (sL != null)
            {
                sL.localRotation = Quaternion.Euler(15f, 10f, 65f);
            }
            if (sR != null)
            {
                sR.localRotation = Quaternion.Euler(15f, -10f, -65f);
            }

            var eL = proxy.transform.Find("Root/Hips/Spine_01/Spine_02/Spine_03/Clavicle_L/Shoulder_L/Elbow_L");
            var eR = proxy.transform.Find("Root/Hips/Spine_01/Spine_02/Spine_03/Clavicle_R/Shoulder_R/Elbow_R");
            if (eL != null) eL.localRotation = Quaternion.Euler(0f, 15f, 15f);
            if (eR != null) eR.localRotation = Quaternion.Euler(0f, -15f, -15f);
        }

        private void PickPendingNextOutfit()
        {
            if (_outfitVariants == null || _outfitVariants.Count == 0)
            {
                AutoCollectOutfits();
            }

            if (_outfitVariants == null || _outfitVariants.Count == 0) return;

            List<GameObject> candidates = _outfitVariants.FindAll(o =>
            {
                if (o == null) return false;
                string clean = CleanOutfitName(o.name);
                return !string.Equals(clean, _currentOutfitName, StringComparison.OrdinalIgnoreCase);
            });

            _pendingNextOutfit = candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : _outfitVariants[0];
        }

        private void AutoCollectOutfits()
        {
            if (_runnerTransform == null) return;
            if (_outfitVariants == null) _outfitVariants = new List<GameObject>();

            foreach (Transform child in _runnerTransform)
            {
                if (child.name.StartsWith("Character_") && child.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    _outfitVariants.Add(child.gameObject);
                }
            }
        }

        private string CleanOutfitName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "";
            string clean = rawName.Replace("(Clone)", "").Trim();
            if (clean.EndsWith("_01")) clean = clean.Substring(0, clean.Length - 3);
            return clean;
        }

        private void AdvanceToNextRunner()
        {
            if (_followerQueue.Count == 0)
            {
                if (_autoReplenishMockQueue)
                {
                    string autoFollower = "Follower_" + UnityEngine.Random.Range(100, 999);
                    _followerQueue.Enqueue(autoFollower);
                }
                else
                {
                    EnterWaitingState();
                    return;
                }
            }

            CurrentRunnerId = _followerQueue.Dequeue();
            UpdateRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, _followerQueue.Count);
        }

        // Gắn tên/avatar lên bảng tên của Runner hiện tại qua facade HUDManager có sẵn.
        private void UpdateRunnerHud()
        {
            if (_hudManager == null)
            {
                return;
            }

            _hudManager.UpdateRunnerInfo(CurrentRunnerId, _defaultAvatar);
            _hudManager.UpdateRunnerTarget(_runnerTransform);
        }

        // Empty Queue Hold: dừng thế giới + báo UI hiện bảng chờ.
        private void EnterWaitingState()
        {
            _isWaitingForFollower = true;
            _speedBeforeWait = _worldSpeedManager != null ? _worldSpeedManager.CurrentSpeed : 8f;

            if (_worldSpeedManager != null)
            {
                _worldSpeedManager.CurrentSpeed = 0f;
            }

            _waitingStateChanged.Invoke(true);
            OnRunnerChanged?.Invoke(CurrentRunnerId, _followerQueue.Count);
        }

        // Có Follower mới vào lúc đang chờ: khôi phục tốc độ, ẩn bảng, chọn Runner kế tiếp.
        private void ResumeFromWaiting()
        {
            _isWaitingForFollower = false;

            if (_worldSpeedManager != null)
            {
                _worldSpeedManager.CurrentSpeed = _speedBeforeWait > 0 ? _speedBeforeWait : 8f;
            }

            _waitingStateChanged.Invoke(false);

            CurrentRunnerId = _followerQueue.Dequeue();
            UpdateRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, _followerQueue.Count);
        }
    }
}

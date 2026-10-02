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
    /// <summary>
    /// Manages the queue of followers participating as runners.
    /// </summary>
    public class ChatRunnerQueueManager : MonoBehaviour
    {
        [SerializeField] private WorldSpeedManager _worldSpeedManager;
        [SerializeField] private float _legDistanceMeters = 100f;

        public static ChatRunnerQueueManager Instance { get; private set; }

        [Header("Initial Queue Mock")]
        [SerializeField] private string _initialRunnerId = "Streamer_Alex";
        [Tooltip("Bật/tắt nạp danh sách follower giả lập mẫu vào hàng đợi ban đầu.")]
        [SerializeField] private bool _enableMockFollowers = false;
        [SerializeField] private List<string> _initialFollowers = new List<string> { "Viewer_Bao", "Viewer_Chi", "Top1_Dung", "Mod_Giang", "Gamer_Huy" };
        [Tooltip("Tu dong sinh them Follower khi hang doi het de test lien tuc ma khong bi dung.")]
        [SerializeField] private bool _autoReplenishMockQueue = false;
        [Tooltip("Nếu true: Dừng Runner chờ follower khi hết hàng đợi. Nếu false: Runner hiện tại tiếp tục chạy chặng tiếp theo.")]
        [SerializeField] private bool _pauseWhenQueueEmpty = false;

        [Header("HUD Reference")]
        [SerializeField] private HUDManager _hudManager;
        [SerializeField] private Transform _runnerTransform;
        [SerializeField] private Sprite _defaultAvatar;
        private Sprite _currentRunnerAvatar;
        public Sprite CurrentRunnerAvatar => _currentRunnerAvatar != null ? _currentRunnerAvatar : _defaultAvatar;

        [Header("Roadside Character Handover")]
        [Tooltip("Bật cơ chế nhân vật tiếp theo đứng chờ sẵn bên lề đường để chuyển gậy.")]
        [SerializeField] private bool _enableRoadsideHandover = true;
        [Tooltip("Khoảng cách mét phía trước Runner xuất hiện nhân vật đứng chờ.")]
        [SerializeField] private float _spawnAheadMeters = 35f;
        [Tooltip("Prefab đại diện người đứng chờ chuyển gậy (chứa Trigger 12m và ModelAnchor).")]
        [SerializeField] private GameObject _roadsideProxyPrefab;
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

        [Header("VIP Ticket (F10) - Nameplate Color")]
        [Tooltip("Màu tên mặc định trên Nameplate của nhân vật đứng chờ.")]
        [SerializeField] private Color _normalNameColor = Color.white;
        [Tooltip("Màu tên khi người được chọn đứng chờ là chủ Vé VIP (F10).")]
        [SerializeField] private Color _vipNameColor = Color.yellow;

        private readonly HashSet<string> _vipFollowerIds = new HashSet<string>();

        [Serializable] public class WaitingStateChangedEvent : UnityEvent<bool> { }
        [SerializeField] private WaitingStateChangedEvent _waitingStateChanged = new WaitingStateChangedEvent();
        public WaitingStateChangedEvent WaitingStateChanged => _waitingStateChanged;

        private readonly Queue<string> _vipQueue = new Queue<string>();
        private readonly Queue<(string userId, bool isVip)> _followerQueue = new Queue<(string, bool)>();
        private readonly FollowerGate _followerGate = new FollowerGate();

        private float _distanceSinceLastLeg;
        private bool _isWaitingForFollower;
        private float _speedBeforeWait;

        private GameObject _activeProxy;
        private bool _proxySpawnedForCurrentLeg;
        private string _currentOutfitName = "";
        private GameObject _pendingNextOutfit;
        private string _pendingNextRunnerId = "";
        private bool _pendingNextRunnerIsVip;

        public string CurrentRunnerId { get; private set; }
        public bool CurrentRunnerIsVip { get; private set; }
        public bool IsWaitingForFollower => _isWaitingForFollower;
        public int QueuedCount => _vipQueue.Count + _followerQueue.Count;
        public int VipQueuedCount => _vipQueue.Count;
        public float LegProgress => _distanceSinceLastLeg;
        public float LegDistanceMeters => _legDistanceMeters;

        /// <summary>
        /// Xem trước Runner kế tiếp trong hàng đợi hoặc Runner đang đứng chờ bên đường.
        /// Ưu tiên hiển thị VIP proxy hoặc người đầu tiên trong Hàng Chờ VIP.
        /// </summary>
        public (string name, bool isVip)? PeekNextRunner()
        {
            if (_proxySpawnedForCurrentLeg && !string.IsNullOrEmpty(_pendingNextRunnerId))
            {
                return (_pendingNextRunnerId, _pendingNextRunnerIsVip);
            }
            if (_vipQueue.Count > 0)
            {
                return (_vipQueue.Peek(), true);
            }
            if (_followerQueue.Count > 0)
            {
                return _followerQueue.Peek();
            }
            return null;
        }

        public bool EnableMockFollowers
        {
            get => _enableMockFollowers;
            set
            {
                _enableMockFollowers = value;
                SetMockFollowersEnabled(value, !value);
            }
        }

        public bool AutoReplenishMockQueue
        {
            get => _autoReplenishMockQueue;
            set => _autoReplenishMockQueue = value;
        }

        public bool PauseWhenQueueEmpty
        {
            get => _pauseWhenQueueEmpty;
            set => _pauseWhenQueueEmpty = value;
        }

        public bool EnableRoadsideHandover
        {
            get => _enableRoadsideHandover;
            set => _enableRoadsideHandover = value;
        }

        /// <summary>
        /// Xóa hàng đợi follower / VIP và hủy nhân vật proxy đứng chờ bên lề đường (nếu có).
        /// </summary>
        public void ClearQueue(bool clearActiveRoadsideProxy = true)
        {
            _followerQueue.Clear();
            _vipQueue.Clear();
            if (clearActiveRoadsideProxy && _activeProxy != null)
            {
                Destroy(_activeProxy);
                _activeProxy = null;
                _proxySpawnedForCurrentLeg = false;
            }
            UpdateNextRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);
        }

        /// <summary>
        /// Bật hoặc tắt tính năng giả lập follower tự động.
        /// </summary>
        public void SetMockFollowersEnabled(bool enabled, bool clearExistingQueue = true)
        {
            _autoReplenishMockQueue = enabled;
            if (!enabled && clearExistingQueue)
            {
                ClearQueue(true);
            }
        }

        /// <summary>
        /// Cập nhật thông tin Runner đang chạy hiện tại (Tên hiển thị + Avatar Sprite).
        /// Thường được gọi bởi TikTokLiveClient khi đồng bộ thông tin chủ kênh Live.
        /// </summary>
        public void SetCurrentRunner(string runnerId, Sprite avatarSprite = null, bool isVip = false)
        {
            if (!string.IsNullOrEmpty(runnerId))
            {
                CurrentRunnerId = runnerId;
            }
            if (avatarSprite != null)
            {
                _currentRunnerAvatar = avatarSprite;
            }
            CurrentRunnerIsVip = isVip;
            UpdateRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public event Action<string, int> OnRunnerChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

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
            var tikTokClient = FindFirstObjectByType<TikTokLiveClient>();
            if (tikTokClient != null && !string.IsNullOrWhiteSpace(tikTokClient.TikTokUniqueId))
            {
                string hostName = tikTokClient.TikTokUniqueId.Trim().TrimStart('@');
                CurrentRunnerId = !string.IsNullOrEmpty(tikTokClient.HostDisplayName) ? tikTokClient.HostDisplayName : hostName;
                if (tikTokClient.HostAvatarSprite != null)
                {
                    _currentRunnerAvatar = tikTokClient.HostAvatarSprite;
                }
            }
            else if (string.IsNullOrEmpty(CurrentRunnerId))
            {
                CurrentRunnerId = _initialRunnerId;
            }

            var liveDemo = LiveSessionDemoRunner.Instance ?? FindFirstObjectByType<LiveSessionDemoRunner>();
            bool allowMock = _enableMockFollowers;
            if (liveDemo != null)
            {
                allowMock = liveDemo.EnableMockFollowers;
                if (!allowMock && liveDemo.SyncQueueManagerMock)
                {
                    _autoReplenishMockQueue = false;
                }
            }

            if (allowMock && _initialFollowers != null && _initialFollowers.Count > 0)
            {
                foreach (var f in _initialFollowers)
                {
                    _followerQueue.Enqueue((f, false));
                }
            }
            else
            {
                _followerQueue.Clear();
                _vipQueue.Clear();
            }

            // An bang cho khi khoi dong
            _isWaitingForFollower = false;
            _waitingStateChanged.Invoke(false);

            UpdateRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, _followerQueue.Count);
        }

        // Gọi từ StreamIntegration khi có sự kiện Follow mới - chỉ nhận nếu qua được FollowerGate.
        public void TryEnqueueFollower(string userId)
        {
            if (!_followerGate.CanJoinQueue(userId))
            {
                return;
            }

            _followerQueue.Enqueue((userId, false));
            UpdateNextRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, _followerQueue.Count);

            if (_isWaitingForFollower)
            {
                ResumeFromWaiting();
            }
        }

        // ===== [VIP Baton Pass] BEGIN - Hàng chờ VIP & Chuyền gậy tức thì =====
        /// <summary>
        /// Vé VIP (F10): Xuất hiện người đứng chờ chuyển gậy NGAY LẬP TỨC bên đường
        /// phía trước Runner mà không cần phải chờ đủ số mét quy định.
        /// Nếu đang có proxy thường -> VIP thay thế ngay lập tức.
        /// Nếu đang có một proxy VIP khác đang tiếp cận -> chèn vào Hàng Chờ VIP để tiếp tục chuyền gậy ngay sau đó.
        /// </summary>
        public void TryEnqueuePriorityFollower(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;

            _vipFollowerIds.Add(userId);
            AudioManager.Instance?.PlaySFX(SFXType.BatonHandover);

            if (_enableRoadsideHandover)
            {
                if (_activeProxy == null)
                {
                    // Chưa có ai đứng chờ -> Xuất hiện ngay nhân vật VIP đứng chờ chuyển gậy phía trước Runner!
                    SpawnRoadsideProxyInternal(userId, true);
                }
                else if (!_pendingNextRunnerIsVip)
                {
                    // Đang có proxy người thường đứng chờ -> VIP chen ngang lập tức, thay thế proxy thường bằng proxy VIP!
                    if (!string.IsNullOrEmpty(_pendingNextRunnerId))
                    {
                        List<(string userId, bool isVip)> tempQueue = new List<(string, bool)>(_followerQueue);
                        _followerQueue.Clear();
                        _followerQueue.Enqueue((_pendingNextRunnerId, false));
                        foreach (var item in tempQueue) _followerQueue.Enqueue(item);
                    }
                    SpawnRoadsideProxyInternal(userId, true);
                }
                else
                {
                    // Đang có một proxy VIP khác đang tiếp cận phía trước -> Chèn vào Hàng Chờ VIP
                    _vipQueue.Enqueue(userId);
                }
            }
            else
            {
                CurrentRunnerId = userId;
                CurrentRunnerIsVip = true;
                _distanceSinceLastLeg = 0f;
                PickPendingNextOutfit();
                SwapRunnerOutfit(_pendingNextOutfit != null ? _pendingNextOutfit.name : "");
                UpdateRunnerHud();
            }

            UpdateNextRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);

            if (_isWaitingForFollower)
            {
                ResumeFromWaiting();
            }
        }
        // ===== [VIP Baton Pass] END =====

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
            // 1. Ưu tiên Hàng Chờ VIP: Nếu chưa có proxy và có VIP đang đợi trong queue -> Xuất hiện ngay lập tức mà không cần chờ đủ mét!
            if (_activeProxy == null && _vipQueue.Count > 0)
            {
                string nextVip = _vipQueue.Dequeue();
                SpawnRoadsideProxyInternal(nextVip, true);
            }

            // 2. Chặng thông thường: Chỉ kiểm tra khi không có ai trong Hàng Chờ VIP
            float triggerDistance = Mathf.Max(0f, _legDistanceMeters - _spawnAheadMeters);
            if (_distanceSinceLastLeg >= triggerDistance && !_proxySpawnedForCurrentLeg && _activeProxy == null && _vipQueue.Count == 0)
            {
                TrySpawnRoadsideProxy();
            }

            // 3. Hoàn tất chuyển gậy khi Runner tiếp cận hoặc vượt qua vị trí proxy
            if (_activeProxy != null && _runnerTransform != null && _activeProxy.transform.position.x < _runnerTransform.position.x - 2f)
            {
                ExecuteRoadsideHandover();
            }
            else if (_activeProxy == null && _distanceSinceLastLeg >= _legDistanceMeters && _vipQueue.Count == 0)
            {
                if (_followerQueue.Count == 0 && !_autoReplenishMockQueue && !_pauseWhenQueueEmpty)
                {
                    _distanceSinceLastLeg = 0f;
                }
                else
                {
                    _distanceSinceLastLeg -= _legDistanceMeters;
                    AdvanceToNextRunner();
                }
            }
        }

        /// <summary>
        /// Được gọi bởi RoadsideHandoverTrigger khi Runner thực sự chạm proxy.
        /// </summary>
        public void NotifyRunnerReachedProxy()
        {
            if (_activeProxy == null || !_proxySpawnedForCurrentLeg)
            {
                return;
            }

            ExecuteRoadsideHandover();
        }

        private void TrySpawnRoadsideProxy()
        {
            if (_runnerTransform == null) return;

            string nextRunnerId;
            bool nextRunnerIsVip;

            if (_vipQueue.Count > 0)
            {
                nextRunnerId = _vipQueue.Dequeue();
                nextRunnerIsVip = true;
            }
            else if (_followerQueue.Count == 0)
            {
                if (_autoReplenishMockQueue)
                {
                    nextRunnerId = "Follower_" + UnityEngine.Random.Range(100, 999);
                    nextRunnerIsVip = false;
                }
                else
                {
                    return;
                }
            }
            else
            {
                (nextRunnerId, nextRunnerIsVip) = _followerQueue.Dequeue();
            }

            SpawnRoadsideProxyInternal(nextRunnerId, nextRunnerIsVip);
        }

        private void SpawnRoadsideProxyInternal(string runnerId, bool isVip)
        {
            if (_runnerTransform == null) return;

            if (_activeProxy != null)
            {
                Destroy(_activeProxy);
                _activeProxy = null;
            }

            _pendingNextRunnerId = runnerId;
            _pendingNextRunnerIsVip = isVip;

            PickPendingNextOutfit();
            if (_pendingNextOutfit == null) return;

            _proxySpawnedForCurrentLeg = true;

            bool isLeft = UnityEngine.Random.value > 0.5f;
            float targetZ = isLeft ? _leftSidewalkZ : _rightSidewalkZ;
            Quaternion spawnRot = isLeft ? Quaternion.Euler(0f, 65f, 0f) : Quaternion.Euler(0f, -65f, 0f);

            Vector3 rootPos = new Vector3(_runnerTransform.position.x + _spawnAheadMeters, 0f, 0f);

            if (_roadsideProxyPrefab != null)
            {
                _activeProxy = Instantiate(_roadsideProxyPrefab, rootPos, Quaternion.identity);
                _activeProxy.name = $"RoadsideHandoverProxy_{_pendingNextOutfit.name}";
            }
            else
            {
                _activeProxy = new GameObject($"RoadsideHandoverProxy_{_pendingNextOutfit.name}");
                _activeProxy.transform.position = rootPos;
                _activeProxy.transform.rotation = Quaternion.identity;
            }

            MovingWorldObject mover = _activeProxy.GetComponent<MovingWorldObject>();
            if (mover == null) mover = _activeProxy.AddComponent<MovingWorldObject>();
            if (_worldSpeedManager != null) mover.Initialize(_worldSpeedManager);

            BoxCollider box = _activeProxy.GetComponent<BoxCollider>();
            if (box == null) box = _activeProxy.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.5f, 3.5f, 12f);
            box.center = new Vector3(0f, 1.75f, 0f);

            RoadsideHandoverTrigger trigger = _activeProxy.GetComponent<RoadsideHandoverTrigger>();
            if (trigger == null) trigger = _activeProxy.AddComponent<RoadsideHandoverTrigger>();
            trigger.Initialize(this, 12f);

            Transform existingContainer = _activeProxy.transform.Find("ModelContainer");
            GameObject modelAnchor;
            if (existingContainer != null)
            {
                modelAnchor = existingContainer.gameObject;
            }
            else
            {
                modelAnchor = new GameObject("ModelAnchor");
                modelAnchor.transform.SetParent(_activeProxy.transform, false);
            }

            modelAnchor.transform.localPosition = new Vector3(0f, _sidewalkY, targetZ);
            modelAnchor.transform.localRotation = spawnRot;

            foreach (Transform c in modelAnchor.transform)
            {
                Destroy(c.gameObject);
            }

            GameObject characterInstance = Instantiate(_pendingNextOutfit, modelAnchor.transform);
            characterInstance.name = _pendingNextOutfit.name;
            characterInstance.transform.localPosition = Vector3.zero;
            characterInstance.transform.localRotation = Quaternion.identity;

            foreach (var col in characterInstance.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            var anims = characterInstance.GetComponentsInChildren<Animator>();
            foreach (var a in anims)
            {
                a.enabled = false;
            }
            ApplyNaturalStandingPose(characterInstance);

            AttachNameplateToProxy(characterInstance, _pendingNextRunnerId, _pendingNextRunnerIsVip);

            UpdateNextRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);
        }

        private void ExecuteRoadsideHandover()
        {
            CurrentRunnerId = _pendingNextRunnerId;
            CurrentRunnerIsVip = _pendingNextRunnerIsVip;
            _distanceSinceLastLeg = 0f;
            _proxySpawnedForCurrentLeg = false;

            SwapRunnerOutfit(_pendingNextOutfit != null ? _pendingNextOutfit.name : "");

            UpdateRunnerHud();
            if (_hudManager != null)
            {
                _hudManager.ShowStatusPopup($"Handover: [{CurrentRunnerId}]", true);
            }

            AudioManager.Instance?.PlaySFX(SFXType.BatonHandover);

            if (_activeProxy != null)
            {
                Destroy(_activeProxy);
                _activeProxy = null;
            }

            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);
            Debug.Log($"[ChatRunnerQueueManager] Chuyển gậy thành công cho: {CurrentRunnerId} (VIP={CurrentRunnerIsVip})!");

            // Nếu trong Hàng Chờ VIP có người, xuất hiện NGAY LẬP TỨC proxy cho người VIP tiếp theo mà không cần chờ đủ mét!
            if (_enableRoadsideHandover && _vipQueue.Count > 0)
            {
                string nextVip = _vipQueue.Dequeue();
                SpawnRoadsideProxyInternal(nextVip, true);
            }
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

        private void AttachNameplateToProxy(GameObject proxyObj, string displayName, bool isVip)
        {
            if (_nameplatePrefab == null) return;

            GameObject nameplate = Instantiate(_nameplatePrefab, proxyObj.transform);
            nameplate.name = "RelayNameplate";
            nameplate.transform.localPosition = new Vector3(0f, 2.1f, 0f);

            var tmp = nameplate.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null)
            {
                tmp.text = displayName;
                tmp.color = isVip ? _vipNameColor : _normalNameColor;
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
            if (_vipQueue.Count > 0)
            {
                CurrentRunnerId = _vipQueue.Dequeue();
                CurrentRunnerIsVip = true;
                UpdateRunnerHud();
                OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);
                return;
            }

            if (_followerQueue.Count == 0)
            {
                if (_autoReplenishMockQueue)
                {
                    string autoFollower = "Follower_" + UnityEngine.Random.Range(100, 999);
                    _followerQueue.Enqueue((autoFollower, false));
                }
                else if (_pauseWhenQueueEmpty)
                {
                    EnterWaitingState();
                    return;
                }
                else
                {
                    _distanceSinceLastLeg = 0f;
                    UpdateRunnerHud();
                    return;
                }
            }

            (CurrentRunnerId, CurrentRunnerIsVip) = _followerQueue.Dequeue();
            UpdateRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);
        }

        // Gắn tên/avatar/trạng thái VIP lên bảng tên của Runner hiện tại qua facade HUDManager có sẵn.
        private void UpdateRunnerHud()
        {
            if (_hudManager == null)
            {
                return;
            }

            _hudManager.UpdateRunnerInfo(CurrentRunnerId, CurrentRunnerAvatar, CurrentRunnerIsVip);
            _hudManager.UpdateRunnerTarget(_runnerTransform);
            UpdateNextRunnerHud();
        }

        public void UpdateNextRunnerHud()
        {
            if (_hudManager == null)
            {
                return;
            }

            var next = PeekNextRunner();
            if (next.HasValue)
            {
                _hudManager.UpdateNextRunnerPreview(next.Value.name, next.Value.isVip);
            }
            else
            {
                _hudManager.UpdateNextRunnerPreview(string.Empty, false);
            }
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
            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);
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

            if (_vipQueue.Count > 0)
            {
                CurrentRunnerId = _vipQueue.Dequeue();
                CurrentRunnerIsVip = true;
            }
            else if (_followerQueue.Count > 0)
            {
                (CurrentRunnerId, CurrentRunnerIsVip) = _followerQueue.Dequeue();
            }
            UpdateRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);
        }
    }
}
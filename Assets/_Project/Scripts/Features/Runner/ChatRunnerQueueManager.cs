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

        [Header("Initial Queue Mock")]
        [SerializeField] private string _initialRunnerId = "Streamer_Alex";
        [SerializeField] private List<string> _initialFollowers = new List<string> { "Viewer_Bao", "Viewer_Chi", "Top1_Dung", "Mod_Giang", "Gamer_Huy" };
        [Tooltip("Tu dong sinh them Follower khi hang doi het de test lien tuc ma khong bi dung.")]
        [SerializeField] private bool _autoReplenishMockQueue = true;

        [Header("HUD Reference")]
        [SerializeField] private HUDManager _hudManager;
        [SerializeField] private Transform _runnerTransform;
        [SerializeField] private Sprite _defaultAvatar;

        [Header("Roadside Character Handover")]
        [Tooltip("Bật cơ chế nhân vật tiếp theo đứng chờ sẵn bên lề đường để chuyển gậy.")]
        [SerializeField] private bool _enableRoadsideHandover = true;
        [Tooltip("Khoảng cách mét phía trước Runner xuất hiện nhân vật đứng chờ.")]
        [SerializeField] private float _spawnAheadMeters = 35f;
        [Tooltip("Prefab đại diện người đứng chờ chuyển gậy (chứa Trigger 12m và ModelAnchor).")]
        [SerializeField] private GameObject _roadsideProxyPrefab;
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

        [Header("VIP Ticket (F10) - Nameplate Color")]
        [Tooltip("Màu tên mặc định trên Nameplate của nhân vật đứng chờ.")]
        [SerializeField] private Color _normalNameColor = Color.white;
        [Tooltip("Màu tên khi người được chọn đứng chờ là chủ Vé VIP (F10).")]
        [SerializeField] private Color _vipNameColor = Color.yellow;

        private readonly HashSet<string> _vipFollowerIds = new HashSet<string>();

        [Serializable] public class WaitingStateChangedEvent : UnityEvent<bool> { }
        [SerializeField] private WaitingStateChangedEvent _waitingStateChanged = new WaitingStateChangedEvent();
        public WaitingStateChangedEvent WaitingStateChanged => _waitingStateChanged;

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
        public int QueuedCount => _followerQueue.Count;
        public float LegProgress => _distanceSinceLastLeg;
        public float LegDistanceMeters => _legDistanceMeters;

        /// <summary>
        /// Xem trước Runner kế tiếp trong hàng đợi hoặc Runner đang đứng chờ bên đường.
        /// </summary>
        public (string name, bool isVip)? PeekNextRunner()
        {
            if (_proxySpawnedForCurrentLeg && !string.IsNullOrEmpty(_pendingNextRunnerId))
            {
                return (_pendingNextRunnerId, _pendingNextRunnerIsVip);
            }
            if (_followerQueue.Count > 0)
            {
                return _followerQueue.Peek();
            }
            return null;
        }

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
                    _followerQueue.Enqueue((f, false));
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

            _followerQueue.Enqueue((userId, false));
            UpdateNextRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, _followerQueue.Count);

            if (_isWaitingForFollower)
            {
                ResumeFromWaiting();
            }
        }

        // ===== [Dhuy] BEGIN - F9/F10 Vé hàng chờ (VIP Ticket) =====
        /// <summary>
        /// Vé VIP (F10): chèn thẳng lên ĐẦU hàng đợi để chạy chặng tiếp theo.
        /// An toàn tuyệt đối với proxy đang đứng chờ (nếu có), vì proxy đã được Dequeue()
        /// ngay lúc spawn (xem TrySpawnRoadsideProxy) - slot đầu _followerQueue lúc này
        /// luôn là người CHƯA được chọn/spawn proxy, không có ai bị "cướp chỗ".
        /// </summary>
        public void TryEnqueuePriorityFollower(string userId)
        {
            if (!_followerGate.CanJoinQueue(userId))
            {
                return;
            }

            // Queue<T> không hỗ trợ chèn vào đầu trực tiếp -> dựng lại qua List tạm,
            // không đụng tới cấu trúc Queue gốc của các hàm khác.
            List<(string userId, bool isVip)> remaining = new List<(string, bool)>(_followerQueue);
            _followerQueue.Clear();
            _followerQueue.Enqueue((userId, true));
            foreach (var entry in remaining)
            {
                _followerQueue.Enqueue(entry);
            }

            _vipFollowerIds.Add(userId);

            UpdateNextRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, _followerQueue.Count);

            if (_isWaitingForFollower)
            {
                ResumeFromWaiting();
            }
        }
        // ===== [Dhuy] END =====

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
            float triggerDistance = Mathf.Max(0f, _legDistanceMeters - _spawnAheadMeters);
            if (_distanceSinceLastLeg >= triggerDistance && !_proxySpawnedForCurrentLeg && _activeProxy == null)
            {
                TrySpawnRoadsideProxy();
            }

            if (_activeProxy != null && _runnerTransform != null && _activeProxy.transform.position.x < _runnerTransform.position.x - 2f)
            {
                ExecuteRoadsideHandover();
            }
            else if (_activeProxy == null && _distanceSinceLastLeg >= _legDistanceMeters)
            {
                _distanceSinceLastLeg -= _legDistanceMeters;
                AdvanceToNextRunner();
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

            if (_followerQueue.Count == 0)
            {
                if (_autoReplenishMockQueue)
                {
                    _pendingNextRunnerId = "Follower_" + UnityEngine.Random.Range(100, 999);
                    _pendingNextRunnerIsVip = false;
                }
                else
                {
                    return;
                }
            }
            else
            {
                (_pendingNextRunnerId, _pendingNextRunnerIsVip) = _followerQueue.Dequeue();
            }

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
            OnRunnerChanged?.Invoke(CurrentRunnerId, _followerQueue.Count);
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
                _hudManager.ShowStatusPopup($"Handover: {CurrentRunnerId}!", true);
            }

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
            if (_followerQueue.Count == 0)
            {
                if (_autoReplenishMockQueue)
                {
                    string autoFollower = "Follower_" + UnityEngine.Random.Range(100, 999);
                    _followerQueue.Enqueue((autoFollower, false));
                }
                else
                {
                    EnterWaitingState();
                    return;
                }
            }

            (CurrentRunnerId, CurrentRunnerIsVip) = _followerQueue.Dequeue();
            UpdateRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, _followerQueue.Count);
        }

        // Gắn tên/avatar/trạng thái VIP lên bảng tên của Runner hiện tại qua facade HUDManager có sẵn.
        private void UpdateRunnerHud()
        {
            if (_hudManager == null)
            {
                return;
            }

            _hudManager.UpdateRunnerInfo(CurrentRunnerId, _defaultAvatar, CurrentRunnerIsVip);
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

            (CurrentRunnerId, CurrentRunnerIsVip) = _followerQueue.Dequeue();
            UpdateRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, _followerQueue.Count);
        }
    }
}
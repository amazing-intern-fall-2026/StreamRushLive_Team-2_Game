using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SteamRush.Track;
using SteamRush.Features.StreamIntegration;
using SteamRush.Features.UI;
using SteamRush.Core;
using SteamRush.Core.Typography;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Manages the queue of followers participating as runners in the roadside baton handover relay.
    /// Following on stream immediately queues viewers or spawns them ahead on the sidewalk as the next runner.
    /// </summary>
    public class ChatRunnerQueueManager : MonoBehaviour
    {
        [SerializeField] private WorldSpeedManager _worldSpeedManager;
        [SerializeField] private float _legDistanceMeters = 100f;

        public static ChatRunnerQueueManager Instance { get; private set; }

        [Header("Initial Queue Mock")]
        [SerializeField] private string _initialRunnerId = "user";
        [Tooltip("Enable/disable loading initial mock followers into the queue.")]
        [SerializeField] private bool _enableMockFollowers = false;
        [SerializeField] private List<string> _initialFollowers = new List<string> { "Viewer_Bao", "Viewer_Chi", "Top1_Dung", "Mod_Giang", "Gamer_Huy" };
        [Tooltip("Automatically replenish mock followers when queue is empty for continuous testing.")]
        [SerializeField] private bool _autoReplenishMockQueue = false;
        [Tooltip("If true, stop runner when queue is empty; if false, current runner continues to the next leg.")]
        [SerializeField] private bool _pauseWhenQueueEmpty = false;

        [Header("HUD Reference")]
        [SerializeField] private HUDManager _hudManager;
        [SerializeField] private Transform _runnerTransform;
        [SerializeField] private Sprite _defaultAvatar;
        private Sprite _currentRunnerAvatar;
        public Sprite CurrentRunnerAvatar => _currentRunnerAvatar != null ? _currentRunnerAvatar : _defaultAvatar;

        [Header("Roadside Character Handover")]
        [Tooltip("Distance in meters ahead of the runner to spawn the roadside waiting proxy.")]
        [SerializeField] private float _spawnAheadMeters = 35f;
        [Tooltip("Prefab representing the roadside waiting handover proxy.")]
        [SerializeField] private GameObject _roadsideProxyPrefab;
        [Tooltip("Z position of the left sidewalk.")]
        [SerializeField] private float _leftSidewalkZ = -5.8f;
        [Tooltip("Y rotation angle for character on left sidewalk (facing oncoming runner and road).")]
        [SerializeField] private float _leftSidewalkRotY = -60f;
        [Tooltip("Z position of the right sidewalk.")]
        [SerializeField] private float _rightSidewalkZ = 5.8f;
        [Tooltip("Y rotation angle for character on right sidewalk (facing oncoming runner and road).")]
        [SerializeField] private float _rightSidewalkRotY = -120f;
        [Tooltip("Y elevation of the sidewalk.")]
        [SerializeField] private float _sidewalkY = 0.2f;
        [Tooltip("Nameplate prefab displaying the viewer name above the waiting character.")]
        [SerializeField] private GameObject _nameplatePrefab;
        [Tooltip("List of character model prefabs from PolygonCity.")]
        [SerializeField] private List<GameObject> _outfitVariants = new List<GameObject>();

        [Header("Roadside Nameplate Settings")]
        [Tooltip("Name color on the roadside character nameplate.")]
        [SerializeField] private Color _nameColor = Color.white;

        [Header("Roadside Visual Highlight")]
        [Tooltip("Material used for roadside waiting character outline (e.g. Cyan/Blue).")]
        [SerializeField] private Material _roadsideOutlineMaterial;
        [Tooltip("Outline color for roadside character.")]
        [SerializeField] private Color _roadsideOutlineColor = new Color(0.15f, 0.75f, 1f, 1f); // Electric Cyan Blue
        [SerializeField] private float _roadsideOutlineWidth = 0.015f;

        [Serializable] public class WaitingStateChangedEvent : UnityEvent<bool> { }
        [SerializeField] private WaitingStateChangedEvent _waitingStateChanged = new WaitingStateChangedEvent();
        public WaitingStateChangedEvent WaitingStateChanged => _waitingStateChanged;

        private readonly Queue<string> _followerQueue = new Queue<string>();

        private float _distanceSinceLastLeg;
        private bool _isWaitingForFollower;
        private float _speedBeforeWait;

        private GameObject _activeProxy;
        private bool _proxySpawnedForCurrentLeg;
        private string _currentOutfitName = "";
        private GameObject _pendingNextOutfit;
        private string _pendingNextRunnerId = "";

        public string CurrentRunnerId { get; private set; }
        public bool CurrentRunnerIsVip => false;
        public bool IsWaitingForFollower => _isWaitingForFollower;
        public int QueuedCount => _followerQueue.Count;
        public int VipQueuedCount => 0;
        public float LegProgress => _distanceSinceLastLeg;
        public float LegDistanceMeters => _legDistanceMeters;

        public event Action<string, int> OnRunnerChanged;

        /// <summary>
        /// Peeks the next runner in queue or the roadside waiting runner.
        /// </summary>
        public (string name, bool isVip)? PeekNextRunner()
        {
            if (_proxySpawnedForCurrentLeg && !string.IsNullOrEmpty(_pendingNextRunnerId))
            {
                return (_pendingNextRunnerId, false);
            }
            if (_followerQueue.Count > 0)
            {
                return (_followerQueue.Peek(), false);
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

        /// <summary>
        /// Clears follower queue and destroys any active roadside proxy.
        /// </summary>
        public void ClearQueue(bool clearActiveRoadsideProxy = true)
        {
            _followerQueue.Clear();
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
        /// Enables or disables automatic mock follower generation.
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
        /// Updates current runner display info (Display Name + Avatar Sprite).
        /// Typically called by stream integration when syncing host channel profile.
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
            UpdateRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);
        }

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
                    _followerQueue.Enqueue(f);
                }
            }
            else
            {
                _followerQueue.Clear();
            }

            _isWaitingForFollower = false;
            _waitingStateChanged.Invoke(false);

            UpdateRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, _followerQueue.Count);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Backwards compatibility forwarder for legacy callers.
        /// </summary>
        [Obsolete("TryEnqueueFollower is legacy. Use EnqueueFollowerAsRunner instead.")]
        public void TryEnqueueFollower(string userId)
        {
            EnqueueFollowerAsRunner(userId);
        }

        /// <summary>
        /// Backwards compatibility forwarder for legacy callers.
        /// </summary>
        [Obsolete("TryEnqueuePriorityFollower is legacy. Use EnqueueFollowerAsRunner instead.")]
        public void TryEnqueuePriorityFollower(string userId)
        {
            EnqueueFollowerAsRunner(userId);
        }

        /// <summary>
        /// When a viewer follows, they immediately get queued or spawned ahead as the next roadside handover runner.
        /// </summary>
        public void EnqueueFollowerAsRunner(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;

            AudioManager.Instance?.PlaySFX(SFXType.StreamNewFollower);

            if (_activeProxy == null)
            {
                // No proxy currently waiting -> spawn roadside waiting character immediately ahead of runner
                SpawnRoadsideProxyInternal(userId);
            }
            else
            {
                // A proxy is already standing on the sidewalk -> queue this follower as the next runner
                _followerQueue.Enqueue(userId);
            }

            UpdateNextRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);

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
            UpdateRoadsideHandover();
        }

        private void UpdateRoadsideHandover()
        {
            // 1. If no active proxy, spawn next runner if available in queue
            if (_activeProxy == null && _followerQueue.Count > 0)
            {
                string nextRunner = _followerQueue.Dequeue();
                SpawnRoadsideProxyInternal(nextRunner);
            }
            // Or auto replenish mock if enabled
            else if (_activeProxy == null && _followerQueue.Count == 0 && _autoReplenishMockQueue)
            {
                float triggerDistance = Mathf.Max(0f, _legDistanceMeters - _spawnAheadMeters);
                if (_distanceSinceLastLeg >= triggerDistance)
                {
                    string mockRunner = "Follower_" + UnityEngine.Random.Range(100, 999);
                    SpawnRoadsideProxyInternal(mockRunner);
                }
            }

            // 2. Complete handover when runner passes proxy position
            if (_activeProxy != null && _runnerTransform != null && _activeProxy.transform.position.x < _runnerTransform.position.x - 2f)
            {
                ExecuteRoadsideHandover();
            }
            // 3. Check empty queue hold if leg distance reached and queue is completely empty
            else if (_activeProxy == null && _distanceSinceLastLeg >= _legDistanceMeters && _followerQueue.Count == 0)
            {
                if (_pauseWhenQueueEmpty)
                {
                    EnterWaitingState();
                }
                else
                {
                    _distanceSinceLastLeg = 0f;
                }
            }
        }

        /// <summary>
        /// Called by RoadsideHandoverTrigger when the runner contacts the 12m trigger box.
        /// </summary>
        public void NotifyRunnerReachedProxy()
        {
            if (_activeProxy == null || !_proxySpawnedForCurrentLeg)
            {
                return;
            }

            ExecuteRoadsideHandover();
        }

        private void SpawnRoadsideProxyInternal(string runnerId)
        {
            if (_runnerTransform == null) return;

            if (_activeProxy != null)
            {
                Destroy(_activeProxy);
                _activeProxy = null;
            }

            _pendingNextRunnerId = runnerId;

            PickPendingNextOutfit();
            if (_pendingNextOutfit == null) return;

            _proxySpawnedForCurrentLeg = true;

            bool isLeft = UnityEngine.Random.value > 0.5f;
            float targetZ = isLeft ? _leftSidewalkZ : _rightSidewalkZ;
            Quaternion spawnRot = isLeft ? Quaternion.Euler(0f, _leftSidewalkRotY, 0f) : Quaternion.Euler(0f, _rightSidewalkRotY, 0f);

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

            AttachNameplateToProxy(characterInstance, _pendingNextRunnerId);
            AttachRoadsideOutline(characterInstance);

            UpdateNextRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);
        }

        private void AttachRoadsideOutline(GameObject proxyObj)
        {
            if (proxyObj == null) return;
            var outline = proxyObj.GetComponent<CharacterVisualOutline>();
            if (outline == null)
            {
                outline = proxyObj.AddComponent<CharacterVisualOutline>();
            }

            if (_roadsideOutlineMaterial == null)
            {
#if UNITY_EDITOR
                _roadsideOutlineMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Roadside_Outline_Mat.mat");
#endif
            }

            outline.ConfigureOutline(_roadsideOutlineMaterial, _roadsideOutlineColor, _roadsideOutlineWidth);
        }

        private void ExecuteRoadsideHandover()
        {
            CurrentRunnerId = _pendingNextRunnerId;
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
            Debug.Log($"[ChatRunnerQueueManager] Baton handover completed for: {CurrentRunnerId}!");

            // If queue has more followers waiting, spawn next proxy ahead immediately
            if (_followerQueue.Count > 0)
            {
                string nextRunner = _followerQueue.Dequeue();
                SpawnRoadsideProxyInternal(nextRunner);
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

            var runnerOutline = _runnerTransform.GetComponent<CharacterVisualOutline>();
            if (runnerOutline != null)
            {
                runnerOutline.RefreshOutline();
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
                tmp.text = ViewerNameSanitizer.SanitizeDisplayName(displayName);
                tmp.color = _nameColor;
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

        private void UpdateRunnerHud()
        {
            if (_hudManager == null)
            {
                return;
            }

            _hudManager.UpdateRunnerInfo(CurrentRunnerId, CurrentRunnerAvatar, false);
            _hudManager.UpdateRunnerAvatar(CurrentRunnerAvatar);
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

        private void ResumeFromWaiting()
        {
            _isWaitingForFollower = false;

            if (_worldSpeedManager != null)
            {
                _worldSpeedManager.CurrentSpeed = _speedBeforeWait > 0 ? _speedBeforeWait : 8f;
            }

            _waitingStateChanged.Invoke(false);

            if (_followerQueue.Count > 0)
            {
                CurrentRunnerId = _followerQueue.Dequeue();
            }
            UpdateRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);
        }
    }
}
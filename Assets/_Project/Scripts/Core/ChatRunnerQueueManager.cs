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
    /// Coordinates the queue of followers participating as runners in the roadside baton handover relay.
    /// Delegated responsibilities:
    /// - RunnerOutfitRegistry: Outfit variant selection and visual mesh swapping.
    /// - RoadsideHandoverProxySpawner: Spawning, positioning, physics stripping, and nameplate attachment for roadside proxies.
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
        [SerializeField] private Color _roadsideOutlineColor = new Color(0.15f, 0.75f, 1f, 1f);
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
        private GameObject _pendingNextOutfit;
        private string _pendingNextRunnerId = "";

        // SOLID Subsystems
        private readonly RunnerOutfitRegistry _outfitRegistry = new RunnerOutfitRegistry();
        private readonly RoadsideHandoverProxySpawner _proxySpawner = new RoadsideHandoverProxySpawner();

        public string CurrentRunnerId { get; private set; }
        public bool CurrentRunnerIsVip => false;
        public bool IsWaitingForFollower => _isWaitingForFollower;
        public int QueuedCount => _followerQueue.Count;
        public int VipQueuedCount => 0;
        public float LegProgress => _distanceSinceLastLeg;
        public float LegDistanceMeters => _legDistanceMeters;

        public event Action<string, int> OnRunnerChanged;

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

        public void SetMockFollowersEnabled(bool enabled, bool clearExistingQueue = true)
        {
            _autoReplenishMockQueue = enabled;
            if (!enabled && clearExistingQueue)
            {
                ClearQueue(true);
            }
        }

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

            _outfitRegistry.Initialize(_runnerTransform, _outfitVariants);
            _proxySpawner.Initialize(
                _roadsideProxyPrefab,
                _nameplatePrefab,
                _spawnAheadMeters,
                _leftSidewalkZ,
                _leftSidewalkRotY,
                _rightSidewalkZ,
                _rightSidewalkRotY,
                _sidewalkY,
                _nameColor,
                _roadsideOutlineMaterial,
                _roadsideOutlineColor,
                _roadsideOutlineWidth);
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

        [Obsolete("TryEnqueueFollower is legacy. Use EnqueueFollowerAsRunner instead.")]
        public void TryEnqueueFollower(string userId) => EnqueueFollowerAsRunner(userId);

        [Obsolete("TryEnqueuePriorityFollower is legacy. Use EnqueueFollowerAsRunner instead.")]
        public void TryEnqueuePriorityFollower(string userId) => EnqueueFollowerAsRunner(userId);

        public void EnqueueFollowerAsRunner(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;

            AudioManager.Instance?.PlaySFX(SFXType.StreamNewFollower);

            if (_activeProxy == null)
            {
                SpawnRoadsideProxyInternal(userId);
            }
            else
            {
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
            if (_activeProxy == null && _followerQueue.Count > 0)
            {
                string nextRunner = _followerQueue.Dequeue();
                SpawnRoadsideProxyInternal(nextRunner);
            }
            else if (_activeProxy == null && _followerQueue.Count == 0 && _autoReplenishMockQueue)
            {
                float triggerDistance = Mathf.Max(0f, _legDistanceMeters - _spawnAheadMeters);
                if (_distanceSinceLastLeg >= triggerDistance)
                {
                    string mockRunner = "Follower_" + UnityEngine.Random.Range(100, 999);
                    SpawnRoadsideProxyInternal(mockRunner);
                }
            }

            if (_activeProxy != null && _runnerTransform != null && _activeProxy.transform.position.x < _runnerTransform.position.x - 2f)
            {
                ExecuteRoadsideHandover();
            }
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
            _pendingNextOutfit = _outfitRegistry.PickNextOutfit(_runnerTransform);
            if (_pendingNextOutfit == null) return;

            _proxySpawnedForCurrentLeg = true;

            _activeProxy = _proxySpawner.SpawnProxy(
                _runnerTransform,
                _worldSpeedManager,
                this,
                _pendingNextOutfit,
                _pendingNextRunnerId);

            UpdateNextRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);
        }

        private void ExecuteRoadsideHandover()
        {
            CurrentRunnerId = _pendingNextRunnerId;
            _distanceSinceLastLeg = 0f;
            _proxySpawnedForCurrentLeg = false;

            _outfitRegistry.SwapRunnerOutfit(_runnerTransform, _pendingNextOutfit != null ? _pendingNextOutfit.name : "");

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

            if (_followerQueue.Count > 0)
            {
                string nextRunner = _followerQueue.Dequeue();
                SpawnRoadsideProxyInternal(nextRunner);
            }
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
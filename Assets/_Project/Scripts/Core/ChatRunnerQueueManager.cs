using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SteamRush.Track;
using SteamRush.Features.StreamIntegration;
using SteamRush.Features.UI;
using SteamRush.Features.UI.PreGameConfig;
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

        // Represents a follower queued to become a relay runner, including avatar metadata.
        [Serializable]
        public class FollowerQueueEntry
        {
            public string RunnerId;
            public string DisplayName;
            public string AvatarUrl;
            public Sprite AvatarSprite;
            public bool IsVip;

            public FollowerQueueEntry(string runnerId, string displayName = null, string avatarUrl = null, Sprite avatarSprite = null, bool isVip = false)
            {
                RunnerId = runnerId;
                DisplayName = !string.IsNullOrEmpty(displayName) ? displayName : runnerId;
                AvatarUrl = avatarUrl;
                AvatarSprite = avatarSprite;
                IsVip = isVip;
            }
        }

        private readonly Queue<FollowerQueueEntry> _followerQueue = new Queue<FollowerQueueEntry>();
        private readonly Dictionary<string, Sprite> _avatarCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _downloadingUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private float _distanceSinceLastLeg;
        private bool _isWaitingForFollower;
        private float _speedBeforeWait;

        private GameObject _activeProxy;
        private bool _proxySpawnedForCurrentLeg;
        private GameObject _pendingNextOutfit;
        private string _pendingNextRunnerId = "";
        private FollowerQueueEntry _pendingNextEntry;

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
            if (_proxySpawnedForCurrentLeg && _pendingNextEntry != null)
            {
                return (_pendingNextEntry.DisplayName, _pendingNextEntry.IsVip);
            }
            if (_proxySpawnedForCurrentLeg && !string.IsNullOrEmpty(_pendingNextRunnerId))
            {
                return (_pendingNextRunnerId, false);
            }
            if (_followerQueue.Count > 0)
            {
                var peek = _followerQueue.Peek();
                return (peek.DisplayName, peek.IsVip);
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
                _pendingNextEntry = null;
                _pendingNextRunnerId = string.Empty;
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

        // Sets active runner with explicit avatar or cached avatar fallback.
        public void SetCurrentRunner(string runnerId, Sprite avatarSprite = null, bool isVip = false)
        {
            if (!string.IsNullOrEmpty(runnerId))
            {
                CurrentRunnerId = runnerId;
            }
            if (avatarSprite != null)
            {
                _currentRunnerAvatar = avatarSprite;
                if (!string.IsNullOrEmpty(runnerId))
                {
                    _avatarCache[runnerId] = avatarSprite;
                }
            }
            else if (!string.IsNullOrEmpty(runnerId) && _avatarCache.TryGetValue(runnerId, out var cached))
            {
                _currentRunnerAvatar = cached;
            }
            else
            {
                _currentRunnerAvatar = _defaultAvatar;
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
                    _followerQueue.Enqueue(new FollowerQueueEntry(f, f, null, null, false));
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

        // Enqueues follower as runner with user identifier only (backwards compatibility).
        public void EnqueueFollowerAsRunner(string userId) => EnqueueFollowerAsRunner(userId, userId, null, null, false);

        // Enqueues follower with avatar url, pre-loaded sprite, and VIP status (backwards compatibility).
        public void EnqueueFollowerAsRunner(string userId, string avatarUrl, Sprite avatarSprite = null, bool isVip = false)
            => EnqueueFollowerAsRunner(userId, userId, avatarUrl, avatarSprite, isVip);

        // Enqueues follower with separate runnerId, displayName, avatar url, pre-loaded sprite, and VIP status.
        public void EnqueueFollowerAsRunner(string runnerId, string displayName, string avatarUrl, Sprite avatarSprite = null, bool isVip = false)
        {
            if (string.IsNullOrEmpty(runnerId) && string.IsNullOrEmpty(displayName)) return;
            string finalId = !string.IsNullOrEmpty(runnerId) ? runnerId : displayName;
            string finalName = !string.IsNullOrEmpty(displayName) ? displayName : runnerId;

            AudioManager.Instance?.PlaySFX(SFXType.StreamNewFollower);

            var entry = new FollowerQueueEntry(finalId, finalName, avatarUrl, avatarSprite, isVip);

            // Cache or resolve preloaded avatar sprite across both keys.
            if (avatarSprite != null)
            {
                _avatarCache[finalId] = avatarSprite;
                _avatarCache[finalName] = avatarSprite;
            }
            else if (_avatarCache.TryGetValue(finalId, out var cachedById))
            {
                entry.AvatarSprite = cachedById;
            }
            else if (_avatarCache.TryGetValue(finalName, out var cachedByName))
            {
                entry.AvatarSprite = cachedByName;
            }
            else if (!string.IsNullOrEmpty(avatarUrl))
            {
                // Initiate non-blocking download of follower's avatar.
                DownloadFollowerAvatar(entry);
            }
            else
            {
                // Create deterministic procedural avatar if no avatar URL provided.
                Sprite procAvatar = CreateProceduralAvatar(finalName);
                entry.AvatarSprite = procAvatar;
                _avatarCache[finalId] = procAvatar;
                _avatarCache[finalName] = procAvatar;
            }

            if (_activeProxy == null)
            {
                SpawnRoadsideProxyInternal(entry);
            }
            else
            {
                _followerQueue.Enqueue(entry);
            }

            UpdateNextRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);

            if (_isWaitingForFollower)
            {
                ResumeFromWaiting();
            }
        }

        // Initiates asynchronous download of follower avatar with proxy & procedural fallback.
        private void DownloadFollowerAvatar(FollowerQueueEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.AvatarUrl)) return;
            string url = entry.AvatarUrl;
            string uid = entry.RunnerId;
            string name = entry.DisplayName;

            if (_downloadingUrls.Contains(url)) return;
            _downloadingUrls.Add(url);

            StartCoroutine(DownloadAvatarCoroutine(name, url, sprite =>
            {
                _downloadingUrls.Remove(url);
                if (sprite == null) return;

                if (!string.IsNullOrEmpty(uid)) _avatarCache[uid] = sprite;
                if (!string.IsNullOrEmpty(name)) _avatarCache[name] = sprite;
                entry.AvatarSprite = sprite;

                // If waiting at roadside proxy, update pending entry.
                if (_pendingNextEntry != null && 
                    (string.Equals(_pendingNextEntry.RunnerId, uid, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(_pendingNextEntry.DisplayName, name, StringComparison.OrdinalIgnoreCase)))
                {
                    _pendingNextEntry.AvatarSprite = sprite;
                }

                // If handover already occurred and this follower is currently running, refresh active HUD avatar.
                if (string.Equals(CurrentRunnerId, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(CurrentRunnerId, uid, StringComparison.OrdinalIgnoreCase))
                {
                    _currentRunnerAvatar = sprite;
                    UpdateRunnerHud();
                }
            }));
        }

        // Coroutine fetching image texture with proxy transcode support and procedural fallback.
        private System.Collections.IEnumerator DownloadAvatarCoroutine(string runnerId, string avatarUrl, Action<Sprite> onLoaded)
        {
            if (string.IsNullOrEmpty(avatarUrl))
            {
                onLoaded?.Invoke(CreateProceduralAvatar(runnerId));
                yield break;
            }

            int backendPort = PreGameConfigManager.Instance != null && PreGameConfigManager.Instance.CurrentConfig != null 
                ? PreGameConfigManager.Instance.CurrentConfig.backendPort 
                : 9091;

            string targetUrl = avatarUrl;
            bool isLocalProxy = avatarUrl.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                                avatarUrl.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase);

            // If not already proxied, route through local backend proxy to handle headers & WebP transcoding
            if (!isLocalProxy && (avatarUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || avatarUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                targetUrl = $"http://localhost:{backendPort}/api/avatar-proxy?url={UnityEngine.Networking.UnityWebRequest.EscapeURL(avatarUrl)}";
            }

            Sprite loadedSprite = null;

            // Attempt 1: Fetch through avatar proxy
            yield return StartCoroutine(FetchTextureRoutine(targetUrl, s => loadedSprite = s));

            // Attempt 2: If proxy failed and original URL was external, try direct with browser User-Agent
            if (loadedSprite == null && !isLocalProxy)
            {
                yield return StartCoroutine(FetchTextureRoutine(avatarUrl, s => loadedSprite = s));
            }

            // Attempt 3: If still null, generate a clean procedural avatar so the user never sees a blank void
            if (loadedSprite == null)
            {
                loadedSprite = CreateProceduralAvatar(runnerId);
            }

            if (loadedSprite != null)
            {
                onLoaded?.Invoke(loadedSprite);
            }
        }

        private System.Collections.IEnumerator FetchTextureRoutine(string url, Action<Sprite> onLoaded)
        {
            if (string.IsNullOrEmpty(url)) yield break;

            using (UnityEngine.Networking.UnityWebRequest imgReq = UnityEngine.Networking.UnityWebRequest.Get(url))
            {
                imgReq.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                imgReq.timeout = 8;
                yield return imgReq.SendWebRequest();

                if (imgReq.result == UnityEngine.Networking.UnityWebRequest.Result.Success && imgReq.downloadHandler != null && imgReq.downloadHandler.data != null)
                {
                    byte[] data = imgReq.downloadHandler.data;
                    if (data.Length > 0)
                    {
                        // True enables mipchain generation for ultra-sharp 3D world sampling
                        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                        if (tex.LoadImage(data))
                        {
                            tex.filterMode = FilterMode.Trilinear;
                            tex.anisoLevel = 16;
                            tex.Apply(true, false);
                            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                            onLoaded?.Invoke(sprite);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[ChatRunnerQueueManager] Avatar fetch attempt failed for {url}: {imgReq.error}");
                }
            }
        }

        // Generates deterministic procedural avatar with distinct pastel background and initial letter.
        private static Sprite CreateProceduralAvatar(string name)
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            int hash = Mathf.Abs(string.IsNullOrEmpty(name) ? 0 : name.GetHashCode());
            Color[] palette = new Color[]
            {
                new Color(0.2f, 0.6f, 1f),    // Blue
                new Color(0.9f, 0.3f, 0.4f),   // Coral Red
                new Color(0.2f, 0.8f, 0.5f),   // Emerald Green
                new Color(0.95f, 0.65f, 0.1f), // Amber Gold
                new Color(0.65f, 0.35f, 0.95f),// Purple
                new Color(0.1f, 0.75f, 0.85f)  // Cyan
            };
            Color baseColor = palette[hash % palette.Length];
            Color innerColor = Color.Lerp(baseColor, Color.white, 0.35f);
            float radius = size * 0.46f;
            float center = size * 0.5f;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= radius)
                    {
                        float t = dist / radius;
                        pixels[y * size + x] = Color.Lerp(innerColor, baseColor, t);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sprite.name = $"ProceduralAvatar_{name}";
            return sprite;
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
                var nextRunner = _followerQueue.Dequeue();
                SpawnRoadsideProxyInternal(nextRunner);
            }
            else if (_activeProxy == null && _followerQueue.Count == 0 && _autoReplenishMockQueue)
            {
                float triggerDistance = Mathf.Max(0f, _legDistanceMeters - _spawnAheadMeters);
                if (_distanceSinceLastLeg >= triggerDistance)
                {
                    string mockRunner = "Follower_" + UnityEngine.Random.Range(100, 999);
                    SpawnRoadsideProxyInternal(new FollowerQueueEntry(mockRunner, mockRunner, null, null, false));
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

        private void SpawnRoadsideProxyInternal(FollowerQueueEntry entry)
        {
            if (_runnerTransform == null || entry == null) return;

            if (_activeProxy != null)
            {
                Destroy(_activeProxy);
                _activeProxy = null;
            }

            _pendingNextEntry = entry;
            _pendingNextRunnerId = entry.DisplayName;
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

            // Resolve new follower avatar with cache fallback, or reset to default avatar.
            Sprite newAvatar = _pendingNextEntry != null && _pendingNextEntry.AvatarSprite != null 
                ? _pendingNextEntry.AvatarSprite 
                : null;

            if (newAvatar == null && _pendingNextEntry != null && !string.IsNullOrEmpty(_pendingNextEntry.RunnerId) &&
                _avatarCache.TryGetValue(_pendingNextEntry.RunnerId, out var cachedById))
            {
                newAvatar = cachedById;
            }

            if (newAvatar == null && !string.IsNullOrEmpty(CurrentRunnerId) &&
                _avatarCache.TryGetValue(CurrentRunnerId, out var cachedByName))
            {
                newAvatar = cachedByName;
            }

            // Crucial fix: never leave previous runner/host avatar on baton handover.
            _currentRunnerAvatar = newAvatar != null ? newAvatar : _defaultAvatar;

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
            Debug.Log($"[ChatRunnerQueueManager] Baton handover completed for: {CurrentRunnerId}! New avatar: {(_currentRunnerAvatar != null ? _currentRunnerAvatar.name : "null")}");

            if (_followerQueue.Count > 0)
            {
                var nextRunner = _followerQueue.Dequeue();
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
                var entry = _followerQueue.Dequeue();
                CurrentRunnerId = entry.DisplayName;
                _currentRunnerAvatar = entry.AvatarSprite != null ? entry.AvatarSprite : _defaultAvatar;
            }
            UpdateRunnerHud();
            OnRunnerChanged?.Invoke(CurrentRunnerId, QueuedCount);
        }
    }
}
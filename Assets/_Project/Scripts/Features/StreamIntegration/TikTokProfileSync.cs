using System;
using System.Collections;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using SteamRush.Core;
using SteamRush.Features.Runner;
using SteamRush.Features.UI;
using SteamRush.Features.UI.PreGameConfig;

namespace SteamRush.Features.StreamIntegration
{
    /// <summary>
    /// Synchronizes the TikTok streamer/host profile and avatar into the game as the active runner.
    /// Handles web profile scraping, CDN avatar download, and roomInfo extraction.
    /// </summary>
    [DisallowMultipleComponent]
    public class TikTokProfileSync : MonoBehaviour
    {
        [Header("Host Profile")]
        [SerializeField] private TikTokLiveClient _client;

        [Tooltip("Auto-sync host username and avatar as the initial Runner.")]
        [SerializeField] private bool _syncHostAsInitialRunner = true;

        [Tooltip("Display name fetched from host's TikTok profile.")]
        [SerializeField] private string _hostDisplayName = "";

        [Tooltip("Avatar Sprite downloaded directly from host's TikTok profile.")]
        [SerializeField] private Sprite _hostAvatarSprite;

        [Header("Subsystems")]
        [SerializeField] private ChatRunnerQueueManager _queueManager;
        [SerializeField] private HUDManager _hudManager;

        [Header("Diagnostics")]
        [SerializeField] private bool _logEvents = true;

        public string TikTokUniqueId => _client != null ? _client.TikTokUniqueId : string.Empty;

        public string HostDisplayName => _hostDisplayName;
        public Sprite HostAvatarSprite => _hostAvatarSprite;

        public bool SyncHostAsInitialRunner
        {
            get => _syncHostAsInitialRunner;
            set => _syncHostAsInitialRunner = value;
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void Start()
        {
            EnsureReferences();
            string uniqueId = TikTokUniqueId;
            if (_syncHostAsInitialRunner && !string.IsNullOrEmpty(uniqueId))
            {
                SyncHostProfileToRunner(uniqueId);
            }
        }

        private void OnEnable()
        {
            EnsureReferences();
            EventBus.Subscribe<TikTokConnectedEvent>(OnTikTokConnected);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TikTokConnectedEvent>(OnTikTokConnected);
        }

        public void EnsureReferences()
        {
            if (_client == null) _client = GetComponent<TikTokLiveClient>() ?? FindFirstObjectByType<TikTokLiveClient>();
            if (_queueManager == null) _queueManager = FindFirstObjectByType<ChatRunnerQueueManager>();
            if (_hudManager == null) _hudManager = FindFirstObjectByType<HUDManager>();
        }

        private void OnTikTokConnected(TikTokConnectedEvent evt)
        {
            EnsureReferences();

            string uniqueId = !string.IsNullOrEmpty(evt.UniqueId) ? evt.UniqueId : TikTokUniqueId;

            if (evt.RawState != null)
            {
                ParseRoomInfoState(evt.RawState);
            }
            else if (!string.IsNullOrEmpty(uniqueId))
            {
                SyncHostProfileToRunner(uniqueId);
            }
        }

        [ContextMenu("Sync Host Profile Now")]
        public void SyncHostProfileToRunner()
        {
            SyncHostProfileToRunner(TikTokUniqueId);
        }

        public void SyncHostProfileToRunner(string uniqueId)
        {
            string cleanId = NormalizeUniqueId(uniqueId);
            if (string.IsNullOrEmpty(cleanId)) return;

            EnsureReferences();

            string initialName = !string.IsNullOrEmpty(_hostDisplayName) ? _hostDisplayName : cleanId;
            if (_queueManager != null)
            {
                _queueManager.SetCurrentRunner(initialName, _hostAvatarSprite, false);
            }
            else if (_hudManager != null)
            {
                _hudManager.UpdateRunnerInfo(initialName, _hostAvatarSprite, false);
            }

            StopCoroutine("FetchTikTokHostProfileRoutine");
            StartCoroutine(FetchTikTokHostProfileRoutine(cleanId));
        }

        public void ParseRoomInfoState(JObject json)
        {
            if (json == null) return;

            string nick = ReadStringWithFallback(json,
                "nickname",
                "hostDisplayName",
                "roomInfo.data.owner.nickname",
                "roomInfo.owner.nickname",
                "data.owner.nickname",
                "owner.nickname",
                "data.user.nickname");

            string avt = ReadStringWithFallback(json,
                "avatarUrl",
                "hostAvatarUrl",
                "roomInfo.data.owner.avatar_large.url_list[2]",
                "roomInfo.data.owner.avatar_large.url_list[1]",
                "roomInfo.data.owner.avatar_large.url_list[0]",
                "roomInfo.data.owner.avatar_medium.url_list[2]",
                "roomInfo.data.owner.avatar_medium.url_list[0]",
                "roomInfo.data.owner.avatar_thumb.url_list[2]",
                "roomInfo.data.owner.avatar_thumb.url_list[0]",
                "roomInfo.owner.avatar_large.url_list[0]",
                "roomInfo.owner.avatar_thumb.url_list[0]",
                "owner.avatar_large.url_list[0]",
                "owner.avatar_thumb.url_list[0]",
                "data.user.profilePictureUrl");

            if (!string.IsNullOrEmpty(nick))
            {
                _hostDisplayName = nick;
            }

            if (!string.IsNullOrEmpty(avt))
            {
                StartCoroutine(DownloadAvatarTextureRoutine(avt, sprite =>
                {
                    _hostAvatarSprite = sprite;
                    ApplyHostProfileToRunner();
                }));
            }
            else if (!string.IsNullOrEmpty(nick))
            {
                ApplyHostProfileToRunner();
            }
        }

        private IEnumerator FetchTikTokHostProfileRoutine(string uniqueId)
        {
            string cleanId = NormalizeUniqueId(uniqueId);
            if (string.IsNullOrEmpty(cleanId)) yield break;

            // 1. Try querying local backend API first (uses Euler API, reliably gets nickname & avatar)
            int httpPort = 9091;
            string backendApiUrl = $"http://localhost:{httpPort}/api/streamer/{cleanId}";
            bool backendSuccess = false;

            using (UnityWebRequest apiReq = UnityWebRequest.Get(backendApiUrl))
            {
                apiReq.timeout = 5;
                yield return apiReq.SendWebRequest();

                if (apiReq.result == UnityWebRequest.Result.Success)
                {
                    string fetchedNick = null;
                    string fetchedAvatar = null;

                    try
                    {
                        JObject obj = JObject.Parse(apiReq.downloadHandler.text);
                        fetchedNick = obj.Value<string>("nickname");
                        fetchedAvatar = obj.Value<string>("avatarUrl");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[TikTokProfileSync] Error parsing backend streamer response: {ex.Message}");
                    }

                    if (!string.IsNullOrEmpty(fetchedNick))
                    {
                        _hostDisplayName = fetchedNick;
                        backendSuccess = true;
                    }

                    if (!string.IsNullOrEmpty(fetchedAvatar))
                    {
                        yield return StartCoroutine(DownloadAvatarTextureRoutine(fetchedAvatar, sprite =>
                        {
                            _hostAvatarSprite = sprite;
                            ApplyHostProfileToRunner();
                        }));
                        yield break;
                    }
                    else if (backendSuccess)
                    {
                        ApplyHostProfileToRunner();
                        yield break;
                    }
                }
            }

            // 2. Fallback: query web profile if backend was offline or didn't return avatar
            string profileUrl = $"https://www.tiktok.com/@{cleanId}";
            using (UnityWebRequest webReq = UnityWebRequest.Get(profileUrl))
            {
                webReq.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                webReq.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
                webReq.timeout = 6;
                yield return webReq.SendWebRequest();

                if (webReq.result == UnityWebRequest.Result.Success)
                {
                    string html = webReq.downloadHandler.text;
                    string rawNick = ExtractRegexGroup(html, @"\""nickname\"":\""([^\""]+)\""");
                    string rawAvatar = ExtractRegexGroup(html, @"\""avatar(?:Larger|Medium|Thumb)\"":\""(https:[^\""]+)\""");

                    if (!string.IsNullOrEmpty(rawNick))
                    {
                        try { _hostDisplayName = Regex.Unescape(rawNick); } catch { _hostDisplayName = rawNick; }
                    }
                    else if (string.IsNullOrEmpty(_hostDisplayName))
                    {
                        _hostDisplayName = cleanId;
                    }

                    if (!string.IsNullOrEmpty(rawAvatar))
                    {
                        string avatarUrl = rawAvatar;
                        try { avatarUrl = Regex.Unescape(rawAvatar); } catch { }

                        yield return StartCoroutine(DownloadAvatarTextureRoutine(avatarUrl, sprite =>
                        {
                            _hostAvatarSprite = sprite;
                            ApplyHostProfileToRunner();
                        }));
                    }
                    else
                    {
                        ApplyHostProfileToRunner();
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(_hostDisplayName))
                    {
                        _hostDisplayName = cleanId;
                    }
                    ApplyHostProfileToRunner();
                }
            }
        }

        private IEnumerator DownloadAvatarTextureRoutine(string avatarUrl, Action<Sprite> onLoaded)
        {
            if (string.IsNullOrEmpty(avatarUrl)) yield break;

            int backendPort = PreGameConfigManager.Instance != null && PreGameConfigManager.Instance.CurrentConfig != null 
                ? PreGameConfigManager.Instance.CurrentConfig.backendPort 
                : 9091;

            string targetUrl = avatarUrl;
            bool isLocalProxy = avatarUrl.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                                avatarUrl.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase);

            if (!isLocalProxy && (avatarUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || avatarUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                targetUrl = $"http://localhost:{backendPort}/api/avatar-proxy?url={UnityWebRequest.EscapeURL(avatarUrl)}";
            }

            Sprite loaded = null;
            yield return StartCoroutine(FetchAvatarSprite(targetUrl, s => loaded = s));

            if (loaded == null && !isLocalProxy)
            {
                yield return StartCoroutine(FetchAvatarSprite(avatarUrl, s => loaded = s));
            }

            if (loaded != null)
            {
                onLoaded?.Invoke(loaded);
            }
        }

        private IEnumerator FetchAvatarSprite(string url, Action<Sprite> onLoaded)
        {
            if (string.IsNullOrEmpty(url)) yield break;

            using (UnityWebRequest imgReq = UnityWebRequest.Get(url))
            {
                imgReq.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                imgReq.timeout = 8;
                yield return imgReq.SendWebRequest();

                if (imgReq.result == UnityWebRequest.Result.Success && imgReq.downloadHandler != null && imgReq.downloadHandler.data != null)
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
                else if (_logEvents)
                {
                    Debug.LogWarning($"[TikTokProfileSync] Failed to fetch avatar texture from {url}: {imgReq.error}");
                }
            }
        }

        private void ApplyHostProfileToRunner()
        {
            string uniqueId = TikTokUniqueId;
            string finalName = !string.IsNullOrEmpty(_hostDisplayName) ? _hostDisplayName : NormalizeUniqueId(uniqueId);
            if (string.IsNullOrEmpty(finalName)) return;

            EnsureReferences();
            if (_queueManager != null)
            {
                _queueManager.SetCurrentRunner(finalName, _hostAvatarSprite, false);
            }
            else if (_hudManager != null)
            {
                _hudManager.UpdateRunnerInfo(finalName, _hostAvatarSprite, false);
            }

            if (_logEvents)
            {
                Debug.Log($"[TikTokProfileSync] Synced Runner from live channel: {finalName} (@{NormalizeUniqueId(uniqueId)})");
            }
        }

        private static string ReadStringWithFallback(JObject json, params string[] paths)
        {
            if (json == null || paths == null) return string.Empty;
            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                JToken token = json.SelectToken(path);
                if (token != null && token.Type != JTokenType.Null && !string.IsNullOrWhiteSpace(token.ToString()))
                {
                    return token.ToString().Trim();
                }
            }
            return string.Empty;
        }

        private static string ExtractRegexGroup(string text, string pattern)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern)) return string.Empty;
            Match m = Regex.Match(text, pattern);
            return m.Success && m.Groups.Count > 1 ? m.Groups[1].Value : string.Empty;
        }

        private static string NormalizeUniqueId(string raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().TrimStart('@');
        }
    }
}

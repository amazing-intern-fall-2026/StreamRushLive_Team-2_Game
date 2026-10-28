using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using SteamRush.Track;
using SteamRush.Features.Runner;
using SteamRush.Features.StreamIntegration;
using SteamRush.Features.UI.Views;
using StreamRushLive.Features.Gifts;

namespace SteamRush.Features.UI.PreGameConfig
{
    /// <summary>
    /// Target of a Live / Offline (Sandbox Test) mode switch.
    /// </summary>
    public enum PreGameSessionMode
    {
        None,
        Live,
        Offline
    }

    /// <summary>
    /// Central coordinator for PreGame configuration lifecycle, UI modal interactions, and session mode state transitions.
    /// Storage & persistence delegated to PreGameConfigRepository.
    /// Runtime translation & application delegated to PreGameConfigApplier.
    /// </summary>
    [DisallowMultipleComponent]
    public class PreGameConfigManager : MonoBehaviour
    {
        private static PreGameConfigManager _instance;
        public static PreGameConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindFirstObjectByType<PreGameConfigManager>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("UI Reference")]
        [SerializeField] private PreGameConfigUI _configUI;
        [SerializeField] private UnityEngine.UI.Button _btnOpenConfig;
        [SerializeField] private UnityEngine.UI.Button _btnToggleAudio;

        [Header("State")]
        public static bool ForceOpenSettingsOnStart { get; set; } = false;

        // Mode chosen in the confirmation popup; survives scene reload and starts in Start().
        private static PreGameSessionMode _pendingMode = PreGameSessionMode.None;
        public static bool IsLiveSessionActive { get; private set; } = false;
        [SerializeField] private PreGameSessionMode _activeSessionMode = PreGameSessionMode.None;
        // Channel (normalized, no '@') the running Live session was started on. Empty outside a Live session.
        private string _activeSessionHost = "";
        [SerializeField] private bool _openOnStart = true;
        [SerializeField] private bool _isTestModeActive = false;
        [SerializeField] private GameObject _badgeDemoRun;

        private PreGameConfigData _currentConfig;
        public PreGameConfigData CurrentConfig
        {
            get
            {
                if (_currentConfig == null)
                {
                    LoadConfig();
                }
                return _currentConfig;
            }
            private set => _currentConfig = value;
        }
        public bool IsTestModeActive => _isTestModeActive;
        public bool IsOpen => _configUI != null && _configUI.gameObject.activeInHierarchy;

        // SOLID Subsystems
        private readonly PreGameConfigRepository _repository = new PreGameConfigRepository();
        private readonly PreGameConfigApplier _applier = new PreGameConfigApplier();

        public string SaveFilePath => _repository.SaveFilePath;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            IsLiveSessionActive = false;

            LoadConfig();
            BindOpenButton();

            // Cache starting gift icons from scene into resolver immediately
            var router = FindFirstObjectByType<TikTokGiftRouter>();
            if (router != null && router.GiftMappings != null)
            {
                foreach (var m in router.GiftMappings)
                {
                    var canonical = PreGameGiftIconResolver.ResolveGiftIcon(m.giftId, m.giftName);
                    if (canonical != null)
                    {
                        m.giftIcon = canonical;
                    }
                    else if (m.giftIcon != null)
                    {
                        PreGameGiftIconResolver.RegisterIcon(m.giftId, m.giftName, m.giftIcon);
                    }
                }
            }

            // Early configure TikTokLiveClient with the correct Socket.IO port
            var client = FindFirstObjectByType<TikTokLiveClient>();
            if (client != null && CurrentConfig != null)
            {
                int socketPort = CurrentConfig.backendSocketPort > 0 ? CurrentConfig.backendSocketPort : 3001;
                client.SetServerUrl($"http://localhost:{socketPort}");
            }
        }

        private void Start()
        {
            if (_configUI == null)
            {
                _configUI = GetComponentInChildren<PreGameConfigUI>(true);
            }

            BindOpenButton();
            ApplyConfigToRuntime(connectTikTok: false);

            // A mode switch was confirmed right before the scene reload: start that mode on the fresh scene.
            PreGameSessionMode pendingMode = _pendingMode;
            _pendingMode = PreGameSessionMode.None;

            if (pendingMode != PreGameSessionMode.None)
            {
                ForceOpenSettingsOnStart = false;
                Time.timeScale = 1f;
                StartCoroutine(StartPendingModeNextFrame(pendingMode));
            }
            else if (_openOnStart || ForceOpenSettingsOnStart)
            {
                ForceOpenSettingsOnStart = false;
                OpenConfigUI();
            }
            else
            {
                Time.timeScale = 1f;
            }

            UpdateDemoRunBadgeVisibility();
        }

        private System.Collections.IEnumerator StartPendingModeNextFrame(PreGameSessionMode mode)
        {
            yield return null;

            if (mode == PreGameSessionMode.Offline) StartTestMode();
            else if (mode == PreGameSessionMode.Live) StartGoLive();
        }

        public static void ResetGameAndOpenSettings()
        {
            Debug.Log("[PreGameConfigManager] ResetGameAndOpenSettings requested: reloading active scene.");
            _pendingMode = PreGameSessionMode.None;
            ForceOpenSettingsOnStart = true;
            ReloadActiveScene();
        }

        public static void ResetGameAndStartMode(PreGameSessionMode mode)
        {
            Debug.Log($"[PreGameConfigManager] ResetGameAndStartMode requested ({mode}): reloading active scene.");
            _pendingMode = mode;
            ForceOpenSettingsOnStart = false;
            ReloadActiveScene();
        }

        private static void ReloadActiveScene()
        {
            IsLiveSessionActive = false;
            Time.timeScale = 1f;
            DOTween.KillAll();
            string sceneName = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(sceneName);
        }

        public PreGameSessionMode CurrentSessionMode
        {
            get
            {
                if (_activeSessionMode != PreGameSessionMode.None) return _activeSessionMode;
                if (IsLiveSessionActive) return PreGameSessionMode.Live;
                if (_isTestModeActive) return PreGameSessionMode.Offline;
                return PreGameSessionMode.None;
            }
        }

        public bool HasGameProgress()
        {
            var tracker = FindFirstObjectByType<TrackProgressTracker>();
            if (tracker != null)
            {
                return tracker.TotalDistanceMeters >= 5.0f;
            }

            var sw = FindFirstObjectByType<Views.ElapsedTimeStopwatch>();
            if (sw != null)
            {
                return sw.ElapsedSeconds > 5.0f;
            }

            return false;
        }

        /// <summary>Strips whitespace and a leading '@' so channel names can be compared reliably.</summary>
        private static string NormalizeHost(string raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? "" : raw.Trim().TrimStart('@');
        }

        /// <summary>
        /// True when a Live session that already has progress is running on channel A
        /// and <paramref name="requestedHost"/> is a different channel B.
        /// </summary>
        public bool IsHostChange(string requestedHost)
        {
            if (_activeSessionMode != PreGameSessionMode.Live) return false;

            string requested = NormalizeHost(requestedHost);
            if (string.IsNullOrEmpty(requested) || string.IsNullOrEmpty(_activeSessionHost)) return false;
            if (string.Equals(requested, _activeSessionHost, StringComparison.OrdinalIgnoreCase)) return false;

            return HasGameProgress();
        }

        /// <summary>
        /// Call BEFORE connecting to a TikTok channel. If it is a different channel than the running Live session,
        /// shows the reset confirmation and returns true (the caller must NOT connect). Otherwise returns false.
        /// Confirm -> saves the config, reloads the scene, then starts <paramref name="startAfterReset"/>
        /// (None = just reopen the settings with the new channel filled in). Cancel -> the old channel keeps running.
        /// </summary>
        public bool TryConfirmHostChange(string requestedHost, PreGameSessionMode startAfterReset)
        {
            if (!IsHostChange(requestedHost)) return false;
            if (ConfirmationModal.IsShowing) return true;

            string oldHost = "@" + _activeSessionHost;
            string newHost = "@" + NormalizeHost(requestedHost);

            ConfirmationModal.Show(
                ConfirmationModal.T("Đổi kênh TikTok?", "Switch TikTok channel?"),
                ConfirmationModal.T(
                    $"Đổi sang {newHost} sẽ làm mới lại toàn bộ màn chơi. Bạn có muốn tiếp tục?",
                    $"Switching to {newHost} will reset the current game. Do you want to continue?"),
                ConfirmationModal.T("XÁC NHẬN / RESET", "CONFIRM / RESET"),
                ConfirmationModal.T("HỦY", "CANCEL"),
                onConfirm: () =>
                {
                    // The scene reload re-reads the config from disk, so persist the new channel first.
                    SaveConfig();
                    if (startAfterReset == PreGameSessionMode.None) ResetGameAndOpenSettings();
                    else ResetGameAndStartMode(startAfterReset);
                });

            return true;
        }

        public void RequestSwitchMode(PreGameSessionMode target)
        {
            if (target == PreGameSessionMode.None) return;
            if (ConfirmationModal.IsShowing) return;

            // Live -> Live on a different channel is not a mode change, but the old channel's progress must not leak.
            if (target == PreGameSessionMode.Live &&
                TryConfirmHostChange(CurrentConfig != null ? CurrentConfig.tiktokUsername : "", PreGameSessionMode.Live))
            {
                return;
            }

            PreGameSessionMode currentMode = CurrentSessionMode;
            bool isModeChange = (currentMode != PreGameSessionMode.None && currentMode != target);
            bool hasProgress = HasGameProgress();

            if (!isModeChange || !hasProgress)
            {
                if (target == PreGameSessionMode.Offline) StartTestMode();
                else StartGoLive();
                return;
            }

            string modeName = target == PreGameSessionMode.Live
                ? ConfirmationModal.T("LIVE (TikTok thật)", "LIVE (real TikTok)")
                : ConfirmationModal.T("OFFLINE / Test", "OFFLINE / Test");

            string title = ConfirmationModal.T("Đổi chế độ chơi?", "Switch game mode?");
            string message = ConfirmationModal.T(
                $"Chuyển sang chế độ {modeName} sẽ làm mới lại toàn bộ màn chơi từ đầu. Bạn có muốn tiếp tục?",
                $"Switching to {modeName} will reset the current game to the start. Do you want to continue?");

            ConfirmationModal.Show(
                title,
                message,
                ConfirmationModal.T("XÁC NHẬN / RESET", "CONFIRM / RESET"),
                ConfirmationModal.T("HỦY", "CANCEL"),
                onConfirm: () =>
                {
                    SaveConfig();
                    ResetGameAndStartMode(target);
                });
        }

        private void Update()
        {
            UpdateDemoRunBadgeVisibility();
        }

        public void SetDemoRunBadge(GameObject badge)
        {
            _badgeDemoRun = badge;
            UpdateDemoRunBadgeVisibility();
        }

        public void UpdateDemoRunBadgeVisibility()
        {
            if (_badgeDemoRun == null) return;

            bool isDemo = _isTestModeActive && (CurrentConfig != null && CurrentConfig.enableLiveDemoSimulation);
            if (IsOpen) isDemo = false;

            if (_badgeDemoRun.activeSelf != isDemo)
            {
                _badgeDemoRun.SetActive(isDemo);
            }
        }

        private void BindOpenButton()
        {
            if (_btnOpenConfig == null || _btnOpenConfig.name == "BtnOpenPreGameConfig")
            {
                Transform t = transform.Find("Btn_OpenPreGameConfig");
                if (t != null) _btnOpenConfig = t.GetComponent<UnityEngine.UI.Button>();
                if (_btnOpenConfig == null)
                {
                    var found = GameObject.Find("Btn_OpenPreGameConfig");
                    if (found != null) _btnOpenConfig = found.GetComponent<UnityEngine.UI.Button>();
                }
            }

            if (_btnToggleAudio == null)
            {
                var foundAudio = GameObject.Find("Btn_ToggleAudio");
                if (foundAudio != null) _btnToggleAudio = foundAudio.GetComponent<UnityEngine.UI.Button>();
            }

            if (_btnOpenConfig != null)
            {
                _btnOpenConfig.onClick.RemoveListener(ToggleConfigUI);
                _btnOpenConfig.onClick.AddListener(ToggleConfigUI);
            }
        }

        public void ElevatePersistentHUDButtons()
        {
            if (_btnOpenConfig != null)
            {
                _btnOpenConfig.transform.SetAsLastSibling();
            }

            if (_btnToggleAudio == null)
            {
                var foundAudio = GameObject.Find("Btn_ToggleAudio");
                if (foundAudio != null) _btnToggleAudio = foundAudio.GetComponent<UnityEngine.UI.Button>();
            }

            if (_btnToggleAudio != null)
            {
                _btnToggleAudio.transform.SetAsLastSibling();
            }
        }

        private bool _isLoadingConfig = false;

        public void LoadConfig()
        {
            if (_isLoadingConfig) return;
            try
            {
                _isLoadingConfig = true;
                CurrentConfig = _repository.LoadConfig();
            }
            finally
            {
                _isLoadingConfig = false;
            }
        }

        public void SaveConfig()
        {
            _repository.SaveConfig(CurrentConfig);
        }

        public void ResetToDefaults()
        {
            CurrentConfig = PreGameConfigData.CreateDefault();

            if (_configUI != null)
            {
                _configUI.PopulateUI(CurrentConfig);
            }

            Debug.Log("[PreGameConfigManager] Config reset to developer defaults in UI. Click 'SAVE SETTINGS' to persist.");
        }

        private int _lastToggleFrame = -1;
        private float _lastToggleTime = -1f;

        public void ToggleConfigUI()
        {
            if (Time.frameCount == _lastToggleFrame) return;
            if (Time.unscaledTime - _lastToggleTime < 0.15f) return;
            _lastToggleFrame = Time.frameCount;
            _lastToggleTime = Time.unscaledTime;

            if (IsOpen)
            {
                LoadConfig();
                CloseConfigUI();
            }
            else
            {
                OpenConfigUI();
            }
        }

        public void OpenConfigUI()
        {
            LoadConfig();
            IsLiveSessionActive = false;

            if (_configUI != null)
            {
                _configUI.gameObject.SetActive(true);
                _configUI.transform.SetAsLastSibling();
                _configUI.PopulateUI(CurrentConfig);

                ElevatePersistentHUDButtons();
            }

            Time.timeScale = 0f;
        }

        public void CloseConfigUI()
        {
            if (_configUI != null)
            {
                _configUI.gameObject.SetActive(false);
            }

            Time.timeScale = 1f;
            if (_activeSessionMode == PreGameSessionMode.Live)
            {
                IsLiveSessionActive = true;
            }
            UpdateDemoRunBadgeVisibility();
        }

        public void StartTestMode()
        {
            _activeSessionMode = PreGameSessionMode.Offline;
            _activeSessionHost = "";
            _isTestModeActive = true;
            IsLiveSessionActive = false;
            ApplyConfigToRuntime(connectTikTok: false);
            CloseConfigUI();

            var demoRunner = LiveSessionDemoRunner.Instance ?? FindFirstObjectByType<LiveSessionDemoRunner>();
            if (demoRunner != null)
            {
                demoRunner.ConfigureDemoSimulation(
                    CurrentConfig.enableLiveDemoSimulation,
                    CurrentConfig.enableSimulatedChats,
                    CurrentConfig.enableSimulatedGifts,
                    CurrentConfig.enableSimulatedLikes,
                    CurrentConfig.enableSimulatedFollowers,
                    CurrentConfig.enableSimulatedStreamDelay
                );
            }

            var mockConsole = FindFirstObjectByType<Runner.MockChatConsole>();
            if (mockConsole != null)
            {
                mockConsole.SetDebugUIVisible(CurrentConfig.enableDebugUI);
            }

            var hud = FindFirstObjectByType<HUDManager>();
            string modeInfo = CurrentConfig.enableLiveDemoSimulation ? "Simulation [ON]" : "Sandbox [OFF]";
            if (CurrentConfig.enableDebugUI)
            {
                hud?.ShowStatusPopup($"[Test Mode] Sandbox active ({modeInfo})! Press [`] to toggle debug menu.", true);
            }
            Debug.Log($"<color=#00FFFF>[PreGameConfig] Started Sandbox Test Mode ({modeInfo}, DebugUI: {CurrentConfig.enableDebugUI}).</color>");
        }

        public void StartGoLive()
        {
            _activeSessionMode = PreGameSessionMode.Live;
            _activeSessionHost = NormalizeHost(CurrentConfig != null ? CurrentConfig.tiktokUsername : "");
            _isTestModeActive = false;
            IsLiveSessionActive = true;
            ApplyConfigToRuntime(connectTikTok: true);
            CloseConfigUI();
            UpdateDemoRunBadgeVisibility();

            var demoRunner = LiveSessionDemoRunner.Instance ?? FindFirstObjectByType<LiveSessionDemoRunner>();
            if (demoRunner != null)
            {
                demoRunner.StopLiveDemo();
            }

            var mockConsole = FindFirstObjectByType<Runner.MockChatConsole>();
            if (mockConsole != null)
            {
                mockConsole.SetDebugUIVisible(false);
            }

            var hud = FindFirstObjectByType<HUDManager>();
            string userTag = string.IsNullOrEmpty(CurrentConfig.tiktokUsername) ? "unnamed" : "@" + CurrentConfig.tiktokUsername;
            hud?.ShowStatusPopup($"[Go Live] Connecting to TikTok Live ({userTag})...", true);
            Debug.Log($"<color=#FF4081>[PreGameConfig] Started Go Live mode! Connecting to @{CurrentConfig.tiktokUsername}</color>");
        }

        public void ApplyConfigToRuntime(bool connectTikTok)
        {
            _applier.ApplyConfigToRuntime(CurrentConfig, connectTikTok, _isTestModeActive);
        }

        public void ApplyGiftsToRouter()
        {
            _applier.ApplyGiftsToRouter(CurrentConfig);
        }
    }
}
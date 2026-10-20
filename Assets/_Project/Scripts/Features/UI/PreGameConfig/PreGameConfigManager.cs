using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using SteamRush.Track;
using SteamRush.Features.Runner;
using SteamRush.Features.StreamIntegration;
using SteamRush.Core.Localization;
using SteamRush.Features.UI.Views;
using StreamRushLive.Features.Gifts;
using StreamRushLive.Features.Spawning;

namespace SteamRush.Features.UI.PreGameConfig
{
    /// <summary>Target of a Live / Offline (Sandbox Test) mode switch.</summary>
    public enum PreGameSessionMode
    {
        None,
        Live,
        Offline
    }

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

        [Header("State")]
        public static bool ForceOpenSettingsOnStart { get; set; } = false;

        // Mode chosen in the confirmation popup; survives the scene reload and is started in Start().
        private static PreGameSessionMode _pendingMode = PreGameSessionMode.None;
        // True once the config modal has been closed at least once (the run is live / being played).
        // Only then is there state to lose, so only then does a mode switch need a reset confirmation.
        private bool _sessionInProgress = false;
        public static bool IsLiveSessionActive { get; private set; } = false;
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

        private string SaveFilePath => Path.Combine(Application.persistentDataPath, "pregame_settings.json");

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
                _sessionInProgress = true;
            }

            UpdateDemoRunBadgeVisibility();
        }

        // Wait one frame so every other component has finished its own Start() before the mode is applied.
        private System.Collections.IEnumerator StartPendingModeNextFrame(PreGameSessionMode mode)
        {
            yield return null;

            if (mode == PreGameSessionMode.Offline) StartTestMode();
            else if (mode == PreGameSessionMode.Live) StartGoLive();
        }

        public static void ResetGameAndOpenSettings()
        {
            Debug.Log("[PreGameConfigManager] ResetGameAndOpenSettings requested: restoring timescale, killing tweens, and reloading active scene.");
            _pendingMode = PreGameSessionMode.None;
            ForceOpenSettingsOnStart = true;
            ReloadActiveScene();
        }

        /// <summary>Resets the whole level (scene reload), then starts the given mode directly on the fresh scene.</summary>
        public static void ResetGameAndStartMode(PreGameSessionMode mode)
        {
            Debug.Log($"[PreGameConfigManager] ResetGameAndStartMode requested ({mode}): reloading active scene, then starting the mode.");
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

        /// <summary>
        /// Entry point for the "Go Live" / "Test Mode" buttons. If a run is already in progress, asks the streamer
        /// to confirm a full reset first; on a fresh scene there is nothing to lose, so the mode starts directly.
        /// </summary>
        public void RequestSwitchMode(PreGameSessionMode target)
        {
            if (target == PreGameSessionMode.None) return;
            if (ConfirmationModal.IsShowing) return;

            if (!_sessionInProgress)
            {
                if (target == PreGameSessionMode.Offline) StartTestMode();
                else StartGoLive();
                return;
            }

            string modeName = target == PreGameSessionMode.Live
                ? ConfirmationModal.T("LIVE (TikTok thật)", "LIVE (real TikTok)")
                : ConfirmationModal.T("OFFLINE / Test", "OFFLINE / Test");

            ConfirmationModal.Show(
                ConfirmationModal.T("Reset màn chơi?", "Reset the game?"),
                ConfirmationModal.T(
                    $"Chuyển sang chế độ {modeName} sẽ reset toàn bộ màn chơi (năng lượng phe, điểm số, xe đang chạy, hàng chờ runner). Bạn có chắc chắn không?",
                    $"Switching to {modeName} mode will reset the whole level (faction energy, score, active cars, runner queue). Are you sure?"),
                ConfirmationModal.T("XÁC NHẬN / RESET", "CONFIRM / RESET"),
                ConfirmationModal.T("HỦY", "CANCEL"),
                onConfirm: () =>
                {
                    // The scene reload re-reads the config from disk, so persist the values just read from the UI.
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

            if (_btnOpenConfig != null)
            {
                _btnOpenConfig.onClick.RemoveListener(ToggleConfigUI);
                _btnOpenConfig.onClick.AddListener(ToggleConfigUI);
            }
        }

        public void LoadConfig()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    string json = File.ReadAllText(SaveFilePath);
                    CurrentConfig = JsonUtility.FromJson<PreGameConfigData>(json);
                    Debug.Log($"[PreGameConfigManager] Loaded config from {SaveFilePath}");
                }
                else if (PlayerPrefs.HasKey("PreGameConfig_JSON"))
                {
                    string json = PlayerPrefs.GetString("PreGameConfig_JSON");
                    CurrentConfig = JsonUtility.FromJson<PreGameConfigData>(json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PreGameConfigManager] Error reading config file: {ex.Message}. Reverting to defaults.");
                CurrentConfig = null;
            }

            if (CurrentConfig == null || CurrentConfig.gifts == null || CurrentConfig.gifts.Count == 0)
            {
                CurrentConfig = PreGameConfigData.CreateDefault();
                SaveConfig();
            }
            else
            {
                // Ensure all default gifts exist even if config was loaded from an older version
                SyncWithDefaultGifts();

                // Sync language with LocalizationManager
                if (LocalizationManager.Instance != null && !string.IsNullOrEmpty(CurrentConfig.language))
                {
                    LocalizationManager.Instance.SetLanguage(string.Equals(CurrentConfig.language, "vi", StringComparison.OrdinalIgnoreCase)
                        ? AppLanguage.Vietnamese
                        : AppLanguage.English);
                }

                // Migrate old default path D:\Download\backendLiveGame to AppData Local
                if (string.IsNullOrEmpty(CurrentConfig.backendDirectory) || CurrentConfig.backendDirectory == @"D:\Download\backendLiveGame")
                {
                    CurrentConfig.backendDirectory = PreGameConfigData.GetDefaultBackendDirectory();
                }

                if (!string.IsNullOrEmpty(CurrentConfig.tiktokUsername) && CurrentConfig.tiktokUsername.Equals("Username", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentConfig.tiktokUsername = "";
                }

                // Sanitize legacy target distance values
                if (CurrentConfig.finiteTargetDistanceMeters <= 0f || CurrentConfig.finiteTargetDistanceMeters >= 900000000f)
                {
                    CurrentConfig.finiteTargetDistanceMeters = (CurrentConfig.targetDistanceMeters > 0f && CurrentConfig.targetDistanceMeters < 900000000f)
                        ? CurrentConfig.targetDistanceMeters
                        : 1000000f;
                }

                if (!CurrentConfig.isInfiniteDistance)
                {
                    if (CurrentConfig.targetDistanceMeters >= 900000000f || CurrentConfig.targetDistanceMeters <= 0f)
                    {
                        CurrentConfig.targetDistanceMeters = CurrentConfig.finiteTargetDistanceMeters;
                    }
                }

                // Temporarily disable manual keyboard controls
                CurrentConfig.enableKeyboardControl = false;
            }
        }

        private void SyncWithDefaultGifts()
        {
            var def = PreGameConfigData.CreateDefault();
            var existingActions = new HashSet<GiftActionType>();
            foreach (var g in CurrentConfig.gifts)
            {
                existingActions.Add(g.action);
                if (g.customValue <= 0f && PreGameGiftItemConfig.HasStatForAction(g.action))
                {
                    g.customValue = PreGameGiftItemConfig.GetDefaultStat(g.action);
                }

                // Sanitize legacy feature action names
                if (g.featureName == "+300 Blue Energy" || g.featureName == "300 Blue Energy" || (g.action == GiftActionType.Blue_EnergyBottle && g.featureName.Contains("300")))
                {
                    g.featureName = "Blue Energy";
                }
                else if (g.featureName == "+Red Energy" || (g.action == GiftActionType.Red_EnergyBottle && g.featureName.StartsWith("+")))
                {
                    g.featureName = "Red Energy";
                }
                else if (g.featureName == "Meme Dance" || (g.action == GiftActionType.Special_GiftDance && g.featureName.Contains("Meme")))
                {
                    g.featureName = "Dance";
                }

                if (g.giftName == "Meme Dance" || (g.action == GiftActionType.Special_GiftDance && g.giftName.Contains("Meme")))
                {
                    g.giftName = "Dance";
                }

                // Sanitize legacy gift IDs & names
                if (g.action == GiftActionType.Like_Energy)
                {
                    g.giftName = "Heart";
                    g.giftId = 0;
                }
                else if (g.giftName == "Cap" && (g.giftId == 5879 || g.giftId == 0))
                {
                    g.giftId = 6104;
                }
                else if (g.giftName == "Sunglasses" && (g.giftId == 5661 || g.giftId == 0))
                {
                    g.giftId = 5509;
                }
                else if (g.giftName == "Rain" && g.giftId == 5978)
                {
                    g.giftId = 0;
                }
                else if (g.giftName == "Finger Heart")
                {
                    g.giftId = 5487;
                }
            }

            foreach (var g in def.gifts)
            {
                if (!existingActions.Contains(g.action))
                {
                    CurrentConfig.gifts.Add(g);
                }
            }
        }

        public void SaveConfig()
        {
            if (CurrentConfig == null) return;

            try
            {
                if (LocalizationManager.Instance != null)
                {
                    CurrentConfig.language = LocalizationManager.Instance.CurrentLanguageCode;
                }

                string json = JsonUtility.ToJson(CurrentConfig, true);
                File.WriteAllText(SaveFilePath, json);
                PlayerPrefs.SetString("PreGameConfig_JSON", json);
                PlayerPrefs.Save();
                Debug.Log($"[PreGameConfigManager] Saved config to {SaveFilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PreGameConfigManager] Failed to save config: {ex.Message}");
            }
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

                // Keep Settings toggle button and Audio button above the backdrop so they remain clickable
                if (_btnOpenConfig != null)
                {
                    _btnOpenConfig.transform.SetAsLastSibling();
                }
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
            _sessionInProgress = true;
            UpdateDemoRunBadgeVisibility();
        }

        public void StartTestMode()
        {
            _isTestModeActive = true;
            IsLiveSessionActive = false;
            ApplyConfigToRuntime(connectTikTok: false);
            CloseConfigUI();

            // Configure LiveSessionDemoRunner simulation for Test Mode
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

            // Configure MockChatConsole visibility according to user config
            var mockConsole = FindFirstObjectByType<MockChatConsole>();
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
            _isTestModeActive = false;
            IsLiveSessionActive = true;
            ApplyConfigToRuntime(connectTikTok: true);
            CloseConfigUI();
            UpdateDemoRunBadgeVisibility();

            // Stop all mock demo simulation in real live mode
            var demoRunner = LiveSessionDemoRunner.Instance ?? FindFirstObjectByType<LiveSessionDemoRunner>();
            if (demoRunner != null)
            {
                demoRunner.StopLiveDemo();
            }

            // Minimize mock console for clean live broadcast
            var mockConsole = FindFirstObjectByType<MockChatConsole>();
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
            if (CurrentConfig == null) return;

            // 1. Goal Distance
            var tracker = FindFirstObjectByType<TrackProgressTracker>();
            if (tracker != null)
            {
                float targetMeters = CurrentConfig.isInfiniteDistance ? 999999f * 1000f : CurrentConfig.targetDistanceMeters;
                tracker.SetGoalDistanceMeters(targetMeters);
            }

            // 2. Runner Energy Costs
            var runner = FindFirstObjectByType<ChatLaneRunnerController>();
            if (runner != null)
            {
                runner.LaneChangeEnergyCost = CurrentConfig.laneChangeEnergyCost;
                runner.JumpEnergyCost = CurrentConfig.jumpEnergyCost;
            }

            // 2b. Runner Keyboard Control & Chat Control Lock
            var inputHandler = FindFirstObjectByType<RunnerInputHandler>();
            if (inputHandler != null)
            {
                inputHandler.EnableKeyboardControl = CurrentConfig.enableKeyboardControl;
            }

            if (runner != null)
            {
                runner.IsChatControlLocked = CurrentConfig.enableKeyboardControl;
            }

            // 3. GiftManager Buffs & Vehicle Penalties
            var gm = GiftManager.Instance ?? FindFirstObjectByType<GiftManager>();
            if (gm != null)
            {
                gm.SetSprintDuration(CurrentConfig.sprintBuffDuration);
                gm.SetFreeControlDuration(CurrentConfig.freeControlDuration);

                // Sedan
                var sedan = gm.SedanConfig;
                if (sedan != null)
                {
                    sedan.distancePenaltyMeters = CurrentConfig.distancePenaltyMeters;
                    sedan.energyPenaltyPercent = CurrentConfig.sedanEnergyPenaltyPercent;
                }

                // Pickup
                var pickup = gm.PickupConfig;
                if (pickup != null)
                {
                    pickup.distancePenaltyMeters = CurrentConfig.distancePenaltyMeters * 1.5f;
                    pickup.energyPenaltyPercent = CurrentConfig.pickupEnergyPenaltyPercent;
                }

                // Heavy Truck
                var heavy = gm.HeavyTruckConfig;
                if (heavy != null)
                {
                    heavy.distancePenaltyMeters = CurrentConfig.distancePenaltyMeters * 2.5f;
                    heavy.energyPenaltyPercent = CurrentConfig.heavyTruckEnergyPenaltyPercent;
                }
            }

            // 4. Faction Auto Spawn Car
            var faction = FindFirstObjectByType<FactionTugOfWarManager>();
            if (faction != null)
            {
                faction.AutoSpawnOnFullEnergy = CurrentConfig.autoSpawnOnFullEnergy;
            }

            // 5. Spawner Max Obstacles
            var spawner = FindFirstObjectByType<SingleObstacleSpawner>();
            if (spawner != null)
            {
                spawner.MaxConcurrentObstacles = CurrentConfig.maxConcurrentCars;
            }

            // 6. Gift Banner Configuration (Ordering, Description & Enabling)
            ApplyGiftsToRouter();

            // 7. Debug UI Visibility
            var mockConsole = FindFirstObjectByType<SteamRush.Features.Runner.MockChatConsole>();
            if (mockConsole != null)
            {
                mockConsole.SetDebugUIVisible(CurrentConfig.enableDebugUI);
            }

            // 8. TikTok Connection
            var client = FindFirstObjectByType<TikTokLiveClient>();
            if (client != null)
            {
                int socketPort = CurrentConfig.backendSocketPort > 0 ? CurrentConfig.backendSocketPort : 3001;
                client.SetServerUrl($"http://localhost:{socketPort}");

                if (connectTikTok && !string.IsNullOrWhiteSpace(CurrentConfig.tiktokUsername))
                {
                    client.ConnectWithUsername(CurrentConfig.tiktokUsername);
                }
                else if (!connectTikTok && _isTestModeActive)
                {
                    // Only disconnect TikTok when explicitly switching to Sandbox / Test Mode
                    client.Disconnect();
                }
                // When saving settings or closing modal during an active Live broadcast, keep connection alive!
            }

            // 9. Overlay Banners & Displays (How To Play Guide, Gift Info Panel, Stopwatch)
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                var guideObj = canvas.transform.Find("HowToPlayGuide");
                if (guideObj != null)
                {
                    guideObj.gameObject.SetActive(CurrentConfig.showHowToPlayGuide);
                    var guideCtrl = guideObj.GetComponent<Views.HowToPlayGuideController>() ?? guideObj.gameObject.AddComponent<Views.HowToPlayGuideController>();
                    guideCtrl.SetRedOnlyMode(CurrentConfig.enableKeyboardControl);
                }

                var giftPanelObj = canvas.transform.Find("GiftInfoPanel");
                if (giftPanelObj != null) giftPanelObj.gameObject.SetActive(CurrentConfig.showGiftInfoPanel);

                var stopwatchObj = canvas.transform.Find("ElapsedTimeStopwatch");
                if (stopwatchObj != null)
                {
                    var sw = stopwatchObj.GetComponent<Views.ElapsedTimeStopwatch>();
                    if (sw != null) sw.SetDisplayVisible(CurrentConfig.showStopwatchTimer);
                    else stopwatchObj.gameObject.SetActive(CurrentConfig.showStopwatchTimer);
                }
            }

            // 10. Timer Circles Visibility
            ApplyTimerCirclesVisibility(CurrentConfig.showTimerCircles);
        }

        private void ApplyTimerCirclesVisibility(bool visible)
        {
            var stackMgr = Views.TimerCircleVerticalStackManager.Instance ?? FindFirstObjectByType<Views.TimerCircleVerticalStackManager>();
            if (stackMgr != null)
            {
                var cg = stackMgr.GetComponent<CanvasGroup>();
                if (cg == null) cg = stackMgr.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = visible ? 1f : 0f;
                cg.interactable = visible;
                cg.blocksRaycasts = visible;
            }

            var circles = new string[]
            {
                "FreeControlTimerCircle",
                "ShieldTimerCircle",
                "AntiVehiclePhaseTimerCircle",
                "FanSprintTimerCircle",
                "AntiUnlimitedTimerCircle"
            };

            foreach (var circleName in circles)
            {
                var go = GameObject.Find(circleName);
                if (go != null)
                {
                    var cg = go.GetComponent<CanvasGroup>();
                    if (cg != null && !visible)
                    {
                        cg.alpha = 0f;
                    }
                }
            }
        }

        private void ApplyGiftsToRouter()
        {
            var router = FindFirstObjectByType<TikTokGiftRouter>();
            var panel = FindFirstObjectByType<GiftInfoPanelController>();
            if (router == null) return;

            // Cache master sprites from current router mappings into master resolver
            foreach (var m in router.GiftMappings)
            {
                if (m.giftIcon != null)
                {
                    PreGameGiftIconResolver.RegisterIcon(m.giftId, m.giftName, m.giftIcon);
                }
            }

            var newMappings = new List<TikTokGiftMapping>();
            foreach (var g in CurrentConfig.gifts)
            {
                if (!g.isEnabled) continue; // Filter out disabled gifts

                Sprite icon = PreGameGiftIconResolver.ResolveGiftIcon(g.giftId, g.giftName);

                newMappings.Add(new TikTokGiftMapping
                {
                    giftId = g.giftId,
                    giftName = g.giftName,
                    description = g.description,
                    action = g.action,
                    giftIcon = icon,
                    customValue = g.customValue
                });
            }

            router.GiftMappings.Clear();
            router.GiftMappings.AddRange(newMappings);

            if (panel != null)
            {
                panel.BuildGiftDisplay();
            }
        }
    }
}
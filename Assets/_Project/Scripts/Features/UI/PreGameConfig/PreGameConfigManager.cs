using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using SteamRush.Track;
using SteamRush.Features.Runner;
using SteamRush.Features.StreamIntegration;
using SteamRush.Features.UI.Views;
using StreamRushLive.Features.Gifts;
using StreamRushLive.Features.Spawning;

namespace SteamRush.Features.UI.PreGameConfig
{
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
        [SerializeField] private bool _openOnStart = true;
        [SerializeField] private bool _isTestModeActive = false;
        [SerializeField] private GameObject _badgeDemoRun;

        public PreGameConfigData CurrentConfig { get; private set; }
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

            LoadConfig();
            BindOpenButton();

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

            if (_openOnStart)
            {
                OpenConfigUI();
            }
            else
            {
                Time.timeScale = 1f;
            }

            UpdateDemoRunBadgeVisibility();
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

                // Sanitize legacy target distance values
                if (CurrentConfig.finiteTargetDistanceMeters <= 0f || CurrentConfig.finiteTargetDistanceMeters >= 900000000f)
                {
                    CurrentConfig.finiteTargetDistanceMeters = (CurrentConfig.targetDistanceMeters > 0f && CurrentConfig.targetDistanceMeters < 900000000f && CurrentConfig.targetDistanceMeters != 1000000f)
                        ? CurrentConfig.targetDistanceMeters
                        : 1000f;
                }

                if (!CurrentConfig.isInfiniteDistance)
                {
                    if (CurrentConfig.targetDistanceMeters >= 900000000f || CurrentConfig.targetDistanceMeters == 1000000f)
                    {
                        CurrentConfig.targetDistanceMeters = CurrentConfig.finiteTargetDistanceMeters;
                    }
                }
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
            SaveConfig();

            if (_configUI != null)
            {
                _configUI.PopulateUI(CurrentConfig);
            }

            Debug.Log("[PreGameConfigManager] Config reset to developer defaults.");
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
                CloseConfigUI();
            }
            else
            {
                OpenConfigUI();
            }
        }

        public void OpenConfigUI()
        {
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
            UpdateDemoRunBadgeVisibility();
        }

        public void StartTestMode()
        {
            _isTestModeActive = true;
            SaveConfig();
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
            SaveConfig();
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
                if (guideObj != null) guideObj.gameObject.SetActive(CurrentConfig.showHowToPlayGuide);

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

            // Cache master sprites from current router mappings
            var iconCache = new Dictionary<string, Sprite>();
            foreach (var m in router.GiftMappings)
            {
                if (m.giftIcon != null && !iconCache.ContainsKey(m.giftName.ToLowerInvariant()))
                {
                    iconCache[m.giftName.ToLowerInvariant()] = m.giftIcon;
                }
            }
            foreach (var opt in PreGameConfigData.AvailableGifts)
            {
                string k = opt.giftName.ToLowerInvariant();
                if (!iconCache.ContainsKey(k))
                {
                    Sprite s = PreGameUIBuilder.ResolveGiftIcon(opt.giftId, opt.giftName);
                    if (s != null) iconCache[k] = s;
                }
            }

            var newMappings = new List<TikTokGiftMapping>();
            foreach (var g in CurrentConfig.gifts)
            {
                if (!g.isEnabled) continue; // Filter out disabled gifts

                Sprite icon = null;
                string key = (g.giftName ?? "").ToLowerInvariant();
                if (iconCache.ContainsKey(key) && iconCache[key] != null)
                {
                    icon = iconCache[key];
                }
                if (icon == null)
                {
                    icon = PreGameUIBuilder.ResolveGiftIcon(g.giftId, g.giftName);
                }

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

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
        }

        private void BindOpenButton()
        {
            if (_btnOpenConfig == null)
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
            }
        }

        private void SyncWithDefaultGifts()
        {
            var def = PreGameConfigData.CreateDefault();
            var existingActions = new HashSet<GiftActionType>();
            foreach (var g in CurrentConfig.gifts)
            {
                existingActions.Add(g.action);
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

        public void ToggleConfigUI()
        {
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
                if (connectTikTok && !string.IsNullOrWhiteSpace(CurrentConfig.tiktokUsername))
                {
                    client.ConnectWithUsername(CurrentConfig.tiktokUsername);
                }
                else
                {
                    client.Disconnect();
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
#if UNITY_EDITOR
            string dir = "Assets/_Project/Textures/TikTokGifts";
            foreach (var opt in PreGameConfigData.AvailableGifts)
            {
                string k = opt.giftName.ToLowerInvariant();
                if (!iconCache.ContainsKey(k))
                {
                    Sprite s = null;
                    if (opt.giftId > 0) s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{opt.giftId}.png");
                    if (s == null) s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{k}.png");
                    if (s != null) iconCache[k] = s;
                }
            }
#endif

            var newMappings = new List<TikTokGiftMapping>();
            foreach (var g in CurrentConfig.gifts)
            {
                if (!g.isEnabled) continue; // Filter out disabled gifts

                Sprite icon = null;
                string key = (g.giftName ?? "").ToLowerInvariant();
                if (iconCache.ContainsKey(key))
                {
                    icon = iconCache[key];
                }

                newMappings.Add(new TikTokGiftMapping
                {
                    giftId = g.giftId,
                    giftName = g.giftName,
                    description = g.description,
                    action = g.action,
                    giftIcon = icon
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

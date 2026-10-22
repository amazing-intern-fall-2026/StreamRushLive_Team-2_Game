using System;
using System.Collections.Generic;
using UnityEngine;
using SteamRush.Core.Localization;
using SteamRush.Track;
using SteamRush.Features.Runner;
using SteamRush.Features.StreamIntegration;
using StreamRushLive.Features.Gifts;
using StreamRushLive.Features.Spawning;

namespace SteamRush.Features.UI.PreGameConfig
{
    /// <summary>
    /// Single Responsibility: Translates and applies a PreGameConfigData model
    /// to all runtime game systems, managers, UI overlays, and network clients.
    /// </summary>
    public class PreGameConfigApplier
    {
        public void ApplyConfigToRuntime(PreGameConfigData config, bool connectTikTok, bool isTestModeActive)
        {
            if (config == null) return;

            // 0. Language Localization
            if (LocalizationManager.Instance != null && !string.IsNullOrEmpty(config.language))
            {
                var targetLang = string.Equals(config.language, "vi", StringComparison.OrdinalIgnoreCase)
                    ? AppLanguage.Vietnamese
                    : AppLanguage.English;
                if (LocalizationManager.Instance.CurrentLanguage != targetLang)
                {
                    LocalizationManager.Instance.SetLanguage(targetLang);
                }
            }

            // 1. Goal Distance
            var tracker = UnityEngine.Object.FindFirstObjectByType<TrackProgressTracker>();
            if (tracker != null)
            {
                float targetMeters = config.isInfiniteDistance ? 999999f * 1000f : config.targetDistanceMeters;
                tracker.SetGoalDistanceMeters(targetMeters);
            }

            // 2. Runner Energy Costs
            var runner = UnityEngine.Object.FindFirstObjectByType<ChatLaneRunnerController>();
            if (runner != null)
            {
                runner.LaneChangeEnergyCost = config.laneChangeEnergyCost;
                runner.JumpEnergyCost = config.jumpEnergyCost;
            }

            // 2b. Runner Keyboard Control & Chat Control Lock
            var inputHandler = UnityEngine.Object.FindFirstObjectByType<RunnerInputHandler>();
            if (inputHandler != null)
            {
                inputHandler.EnableKeyboardControl = config.enableKeyboardControl;
            }

            if (runner != null)
            {
                runner.IsChatControlLocked = config.enableKeyboardControl;
            }

            // 3. GiftManager Buffs & Vehicle Penalties
            var gm = GiftManager.Instance ?? UnityEngine.Object.FindFirstObjectByType<GiftManager>();
            if (gm != null)
            {
                gm.SetSprintDuration(config.sprintBuffDuration);
                gm.SetFreeControlDuration(config.freeControlDuration);

                // Sedan
                var sedan = gm.SedanConfig;
                if (sedan != null)
                {
                    sedan.distancePenaltyMeters = config.distancePenaltyMeters;
                    sedan.energyPenaltyPercent = config.sedanEnergyPenaltyPercent;
                }

                // Pickup
                var pickup = gm.PickupConfig;
                if (pickup != null)
                {
                    pickup.distancePenaltyMeters = config.distancePenaltyMeters * 1.5f;
                    pickup.energyPenaltyPercent = config.pickupEnergyPenaltyPercent;
                }

                // Heavy Truck
                var heavy = gm.HeavyTruckConfig;
                if (heavy != null)
                {
                    heavy.distancePenaltyMeters = config.distancePenaltyMeters * 2.5f;
                    heavy.energyPenaltyPercent = config.heavyTruckEnergyPenaltyPercent;
                }
            }

            // 4. Faction Auto Spawn Car & Inactivity Timeout
            var faction = UnityEngine.Object.FindFirstObjectByType<FactionTugOfWarManager>();
            if (faction != null)
            {
                faction.AutoSpawnOnFullEnergy = config.autoSpawnOnFullEnergy;
                if (config.memberInactivityTimeoutSeconds > 0)
                {
                    faction.MemberInactivityTimeoutSeconds = config.memberInactivityTimeoutSeconds;
                }
            }

            // 5. Spawner Max Obstacles
            var spawner = UnityEngine.Object.FindFirstObjectByType<SingleObstacleSpawner>();
            if (spawner != null)
            {
                spawner.MaxConcurrentObstacles = config.maxConcurrentCars;
            }

            // 6. Gift Banner Configuration (Ordering, Description & Enabling)
            ApplyGiftsToRouter(config);

            // 7. Debug UI Visibility
            var mockConsole = UnityEngine.Object.FindFirstObjectByType<MockChatConsole>();
            if (mockConsole != null)
            {
                mockConsole.SetDebugUIVisible(config.enableDebugUI);
            }

            // 8. TikTok Connection
            var client = UnityEngine.Object.FindFirstObjectByType<TikTokLiveClient>();
            if (client != null)
            {
                int socketPort = config.backendSocketPort > 0 ? config.backendSocketPort : 3001;
                client.SetServerUrl($"http://localhost:{socketPort}");

                if (connectTikTok && !string.IsNullOrWhiteSpace(config.tiktokUsername))
                {
                    client.ConnectWithUsername(config.tiktokUsername);
                }
                else if (!connectTikTok && isTestModeActive)
                {
                    // Only disconnect TikTok when explicitly switching to Sandbox / Test Mode
                    client.Disconnect();
                }
            }

            // 9. Overlay Banners & Displays (How To Play Guide, Gift Info Panel, Stopwatch)
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                var guideObj = canvas.transform.Find("HowToPlayGuide");
                if (guideObj != null)
                {
                    guideObj.gameObject.SetActive(config.showHowToPlayGuide);
                    var guideCtrl = guideObj.GetComponent<Views.HowToPlayGuideController>() ?? guideObj.gameObject.AddComponent<Views.HowToPlayGuideController>();
                    guideCtrl.SetRedOnlyMode(config.enableKeyboardControl);
                }

                var giftPanelObj = canvas.transform.Find("GiftInfoPanel");
                if (giftPanelObj != null) giftPanelObj.gameObject.SetActive(config.showGiftInfoPanel);

                var stopwatchObj = canvas.transform.Find("ElapsedTimeStopwatch");
                if (stopwatchObj != null)
                {
                    var sw = stopwatchObj.GetComponent<Views.ElapsedTimeStopwatch>();
                    if (sw != null) sw.SetDisplayVisible(config.showStopwatchTimer);
                    else stopwatchObj.gameObject.SetActive(config.showStopwatchTimer);
                }
            }

            // 10. Timer Circles Visibility
            ApplyTimerCirclesVisibility(config.showTimerCircles);
        }

        private void ApplyTimerCirclesVisibility(bool visible)
        {
            var stackMgr = Views.TimerCircleVerticalStackManager.Instance ?? UnityEngine.Object.FindFirstObjectByType<Views.TimerCircleVerticalStackManager>();
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

        public void ApplyGiftsToRouter(PreGameConfigData config)
        {
            if (config == null) return;

            var router = UnityEngine.Object.FindFirstObjectByType<TikTokGiftRouter>();
            var panel = UnityEngine.Object.FindFirstObjectByType<Views.GiftInfoPanelController>();
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
            foreach (var g in config.gifts)
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

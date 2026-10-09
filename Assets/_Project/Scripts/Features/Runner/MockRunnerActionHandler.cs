using UnityEngine;
using StreamRushLive.Features.Spawning;
using SteamRush.Features.StreamIntegration;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Executes simulated runner buffs, gift activations, vehicle obstacles, and energy adjustments.
    /// Extracted from MockChatConsole to adhere to the Single Responsibility Principle (SRP).
    /// </summary>
    public class MockRunnerActionHandler : MonoBehaviour
    {
        [Header("Target References")]
        [SerializeField] private RunnerItemEffects itemEffects;
        [SerializeField] private ChatLaneRunnerController chatLaneRunner;
        [SerializeField] private GiftDanceController giftDance;
        [SerializeField] private FactionTugOfWarManager factionManager;
        [SerializeField] private ChatRunnerQueueManager queueManager;
        [SerializeField] private SteamRush.Features.UI.HUDManager hudManager;
        [SerializeField] private SingleObstacleSpawner obstacleSpawner;

        private float ShieldDuration => StreamRushLive.Features.Gifts.GiftManager.Instance != null ? StreamRushLive.Features.Gifts.GiftManager.Instance.ShieldDuration : 15f;
        private int FanEnergyBottleAmount => StreamRushLive.Features.Gifts.GiftManager.Instance != null ? StreamRushLive.Features.Gifts.GiftManager.Instance.BlueEnergyBottleAmount : 300;
        private int AntiEnergyBottleAmount => StreamRushLive.Features.Gifts.GiftManager.Instance != null ? StreamRushLive.Features.Gifts.GiftManager.Instance.RedEnergyBottleAmount : 500;

        public void AutoWireReferences()
        {
            if (itemEffects == null) itemEffects = GetComponent<RunnerItemEffects>() ?? GetComponentInParent<RunnerItemEffects>() ?? FindFirstObjectByType<RunnerItemEffects>();
            if (chatLaneRunner == null) chatLaneRunner = GetComponent<ChatLaneRunnerController>() ?? GetComponentInParent<ChatLaneRunnerController>() ?? FindFirstObjectByType<ChatLaneRunnerController>();
            if (giftDance == null) giftDance = GetComponent<GiftDanceController>() ?? GetComponentInParent<GiftDanceController>() ?? FindFirstObjectByType<GiftDanceController>();
            if (factionManager == null) factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            if (queueManager == null) queueManager = FindFirstObjectByType<ChatRunnerQueueManager>();
            if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            if (obstacleSpawner == null) obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
        }

        public void MockDonateShield(string sender = "Viewer")
        {
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                StreamRushLive.Features.Gifts.GiftManager.Instance.ActivateShield(sender);
                return;
            }

            AutoWireReferences();
            if (itemEffects != null)
            {
                float duration = ShieldDuration > 0f ? ShieldDuration : 15f;
                itemEffects.ActivateShield(duration);
                hudManager?.ShowFanAction(sender, $"Shield ({duration:F0}s)");
                hudManager?.ShowStatusPopup($"Shield Activated ({duration:F0}s)!", true);
                Debug.Log($"[MockRunnerActionHandler] {sender} gifted Shield ({duration:F0}s).");
            }
            else
            {
                Debug.LogWarning("[MockRunnerActionHandler] RunnerItemEffects not found to activate Shield!");
            }
        }

        public void MockActivateFanSprintBuff(string sender = "Blue Team")
        {
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                StreamRushLive.Features.Gifts.GiftManager.Instance.ActivateSprintBuff(sender);
                return;
            }

            AutoWireReferences();
            if (chatLaneRunner != null)
            {
                if (!chatLaneRunner.IsSprintBuffActive)
                {
                    chatLaneRunner.ActivateSprintBuff(30f);
                    hudManager?.ShowFanAction(sender, "Speed Boost (30s)");
                }
            }
            else
            {
                Debug.LogWarning("[MockRunnerActionHandler] ChatLaneRunnerController not found.");
            }
        }

        public void MockActivateFreeControl(string sender = "Blue Team")
        {
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                StreamRushLive.Features.Gifts.GiftManager.Instance.ActivateFreeControl(sender);
                return;
            }

            AutoWireReferences();
            if (chatLaneRunner != null)
            {
                chatLaneRunner.ActivateFreeControl(30f);
                hudManager?.ShowFanAction(sender, "Free Control (30s)");
                hudManager?.ShowStatusPopup("Free Control Active (30s)!", true);
                Debug.Log($"[MockRunnerActionHandler] {sender} activated Free-Control Buff (30s).");
            }
            else
            {
                Debug.LogWarning("[MockRunnerActionHandler] ChatLaneRunnerController not found.");
            }
        }

        public void MockToggleSprintBuffDebug()
        {
            AutoWireReferences();
            if (chatLaneRunner != null)
            {
                bool newState = !chatLaneRunner.IsSprintBuffActive;
                chatLaneRunner.SetSprintBuffDebug(newState);
                hudManager?.ShowFanAction("DEBUG", newState ? "Sprint: ON" : "Sprint: OFF");
            }
        }

        public void MockSpawnSedanCar(string sender = "Red Team")
        {
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                StreamRushLive.Features.Gifts.GiftManager.Instance.SpawnAntiCar(StreamRushLive.Features.Spawning.VehicleTier.SedanCar, sender);
                return;
            }

            AutoWireReferences();
            bool isUnlimited = obstacleSpawner != null && obstacleSpawner.IsUnlimitedModeActive;
            int cost = factionManager != null && factionManager.AntiCarLaneCost > 0 ? factionManager.AntiCarLaneCost : 100;

            if (!isUnlimited && factionManager != null)
            {
                if (!factionManager.TrySpendAntiEnergy(cost))
                {
                    hudManager?.ShowAntiAction(sender, $"Need {cost} ({factionManager.AntiLikes})");
                    hudManager?.ShowStatusPopup("Out of Energy!", false);
                    Debug.LogWarning($"[MockRunnerActionHandler] Red Team out of energy! Cannot spawn Sedan (Current: {factionManager.AntiLikes}, Need: {cost}).");
                    return;
                }
            }

            if (obstacleSpawner != null)
            {
                bool success = obstacleSpawner.TriggerSpawnCarTier(StreamRushLive.Features.Spawning.VehicleTier.SedanCar);
                if (success)
                {
                    hudManager?.ShowAntiAction(sender, "Spawned Sedan");
                }
                else if (!isUnlimited && factionManager != null)
                {
                    factionManager.AddAntiEnergy(cost);
                }
            }
        }

        public void MockSpawnPickupTruck(string sender = "Red Team")
        {
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                StreamRushLive.Features.Gifts.GiftManager.Instance.SpawnAntiCar(StreamRushLive.Features.Spawning.VehicleTier.PickupTruck, sender);
                return;
            }

            AutoWireReferences();
            bool isUnlimited = obstacleSpawner != null && obstacleSpawner.IsUnlimitedModeActive;
            int cost = factionManager != null && factionManager.AntiCarLaneCost > 0 ? factionManager.AntiCarLaneCost : 100;

            if (!isUnlimited && factionManager != null)
            {
                if (!factionManager.TrySpendAntiEnergy(cost))
                {
                    hudManager?.ShowAntiAction(sender, $"Need {cost} ({factionManager.AntiLikes})");
                    hudManager?.ShowStatusPopup("Out of Energy!", false);
                    Debug.LogWarning($"[MockRunnerActionHandler] Red Team out of energy! Cannot spawn Pickup (Current: {factionManager.AntiLikes}, Need: {cost}).");
                    return;
                }
            }

            if (obstacleSpawner != null)
            {
                bool success = obstacleSpawner.TriggerSpawnCarTier(StreamRushLive.Features.Spawning.VehicleTier.PickupTruck);
                if (success)
                {
                    hudManager?.ShowAntiAction(sender, "Spawned Pickup");
                }
                else if (!isUnlimited && factionManager != null)
                {
                    factionManager.AddAntiEnergy(cost);
                }
            }
        }

        public void MockActivatePickupTruckPhase(string sender = "Red Team")
        {
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                StreamRushLive.Features.Gifts.GiftManager.Instance.ActivatePickupTruckPhase(sender);
                return;
            }

            AutoWireReferences();
            if (obstacleSpawner != null)
            {
                obstacleSpawner.ActivatePickupTruckPhase();
                hudManager?.ShowAntiAction(sender, $"Animals Phase ({obstacleSpawner.VehiclePhaseDuration:F0}s)");
                hudManager?.ShowStatusPopup($"Animals Phase Started ({obstacleSpawner.VehiclePhaseDuration:F0}s)", false);
            }
            else
            {
                Debug.LogWarning("[MockRunnerActionHandler] SingleObstacleSpawner not found.");
            }
        }

        public void MockSpawnHeavyTruck(string sender = "Red Team")
        {
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                StreamRushLive.Features.Gifts.GiftManager.Instance.SpawnAntiCar(StreamRushLive.Features.Spawning.VehicleTier.HeavyTruck, sender);
                return;
            }

            AutoWireReferences();
            bool isUnlimited = obstacleSpawner != null && obstacleSpawner.IsUnlimitedModeActive;
            int cost = factionManager != null && factionManager.AntiCarLaneCost > 0 ? factionManager.AntiCarLaneCost : 100;

            if (!isUnlimited && factionManager != null)
            {
                if (!factionManager.TrySpendAntiEnergy(cost))
                {
                    hudManager?.ShowAntiAction(sender, $"Need {cost} ({factionManager.AntiLikes})");
                    hudManager?.ShowStatusPopup("Out of Energy!", false);
                    Debug.LogWarning($"[MockRunnerActionHandler] Red Team out of energy! Cannot spawn Heavy Truck (Current: {factionManager.AntiLikes}, Need: {cost}).");
                    return;
                }
            }

            if (obstacleSpawner != null)
            {
                bool success = obstacleSpawner.TriggerSpawnCarTier(StreamRushLive.Features.Spawning.VehicleTier.HeavyTruck);
                if (success)
                {
                    hudManager?.ShowAntiAction(sender, "Spawned Train");
                }
                else if (!isUnlimited && factionManager != null)
                {
                    factionManager.AddAntiEnergy(cost);
                }
            }
        }

        public void MockActivateHeavyTruckPhase(string sender = "Red Team")
        {
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                StreamRushLive.Features.Gifts.GiftManager.Instance.ActivateHeavyTruckPhase(sender);
                return;
            }

            AutoWireReferences();
            if (obstacleSpawner != null)
            {
                obstacleSpawner.ActivateHeavyTruckPhase();
                hudManager?.ShowAntiAction(sender, $"Train Phase ({obstacleSpawner.VehiclePhaseDuration:F0}s)");
                hudManager?.ShowStatusPopup($"Train Phase Started ({obstacleSpawner.VehiclePhaseDuration:F0}s)", false);
            }
            else
            {
                Debug.LogWarning("[MockRunnerActionHandler] SingleObstacleSpawner not found.");
            }
        }

        public void MockFanEnergyBottle(string sender = "Viewer_Blue")
        {
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                StreamRushLive.Features.Gifts.GiftManager.Instance.AddBlueEnergy(sender);
                return;
            }

            AutoWireReferences();
            if (factionManager != null)
            {
                int amount = FanEnergyBottleAmount > 0 ? FanEnergyBottleAmount : 300;
                factionManager.OnChatCommand(sender, "#fan");
                factionManager.DebugAdjustFanEnergy(amount);
                hudManager?.ShowFanAction(sender, $"+{amount} Energy");
                hudManager?.ShowStatusPopup($"+{amount} Blue Energy", true);
                AudioManager.Instance?.PlaySFX(SFXType.CollectEnergy, 1.0f);
                Debug.Log($"[MockRunnerActionHandler] {sender} gifted Blue Energy Bottle +{amount}. (Total: {factionManager.FanLikes})");
            }
            else
            {
                Debug.LogWarning("[MockRunnerActionHandler] FactionTugOfWarManager not found.");
            }
        }

        public void MockAntiEnergyBottle(string sender = "Viewer_Red")
        {
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                StreamRushLive.Features.Gifts.GiftManager.Instance.AddRedEnergy(sender);
                return;
            }

            AutoWireReferences();
            if (factionManager != null)
            {
                int amount = AntiEnergyBottleAmount > 0 ? AntiEnergyBottleAmount : 500;
                factionManager.OnChatCommand(sender, "#anti");
                factionManager.DebugAdjustAntiEnergy(amount);
                hudManager?.ShowAntiAction(sender, $"+{amount} Energy");
                hudManager?.ShowStatusPopup($"+{amount} Red Energy", false);
                AudioManager.Instance?.PlaySFX(SFXType.CollectEnergy, 1.0f);
                Debug.Log($"[MockRunnerActionHandler] {sender} gifted Red Energy Bottle +{amount}. (Total: {factionManager.AntiLikes})");
            }
            else
            {
                Debug.LogWarning("[MockRunnerActionHandler] FactionTugOfWarManager not found.");
            }
        }

        public void MockNewFollower(string customUserId = null)
        {
            AutoWireReferences();
            if (queueManager != null)
            {
                string newId = string.IsNullOrEmpty(customUserId) ? ("Follower_" + Random.Range(100, 999)) : customUserId;
                queueManager.EnqueueFollowerAsRunner(newId);
                AudioManager.Instance?.PlaySFX(SFXType.StreamNewFollower);
                hudManager?.ShowFanAction(newId, "Followed -> Next Runner!");
                Debug.Log($"[MockRunnerActionHandler] Shift+F5 -> Follower set as next runner: {newId}");
            }
            else
            {
                Debug.LogWarning("[MockRunnerActionHandler] ChatRunnerQueueManager not found.");
            }
        }

        public void MockActivateAntiCarUnlimited(string sender = "Red Team")
        {
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                StreamRushLive.Features.Gifts.GiftManager.Instance.ActivateUnlimitedCars(sender);
                return;
            }

            AutoWireReferences();
            if (obstacleSpawner != null)
            {
                if (!obstacleSpawner.IsUnlimitedModeActive)
                {
                    obstacleSpawner.ActivateUnlimitedMode();
                    hudManager?.ShowAntiAction(sender, "Unlimited Cars (60s)");
                    Debug.Log("[MockRunnerActionHandler] F7 -> Unlimited Cars (60s) activated.");
                }
                else
                {
                    Debug.Log("[MockRunnerActionHandler] F7 -> Unlimited Cars already active.");
                }
            }
            else
            {
                Debug.LogWarning("[MockRunnerActionHandler] SingleObstacleSpawner not found.");
            }
        }

        public void MockGiftDance(string sender = "Viewer")
        {
            if (StreamRushLive.Features.Gifts.GiftManager.Instance != null)
            {
                StreamRushLive.Features.Gifts.GiftManager.Instance.TriggerGiftDance(sender);
                return;
            }

            AutoWireReferences();
            if (giftDance != null)
            {
                string displayName = !string.IsNullOrEmpty(sender) ? sender : "Viewer";
                float dur = giftDance.Duration;
                hudManager?.ShowFanAction(displayName, $"Dance ({dur:F0}s)");
                hudManager?.ShowStatusPopup($"[{displayName}] Meme Dance!", true);
                giftDance.TriggerDance();
            }
            else
            {
                Debug.LogWarning("[MockRunnerActionHandler] GiftDanceController not found.");
            }
        }

        public void MockWeatherHazard(string sender = "Viewer_Red")
        {
            var weatherManager = SteamRush.Features.Environment.WeatherHazardManager.Instance ?? FindFirstObjectByType<SteamRush.Features.Environment.WeatherHazardManager>();
            if (weatherManager == null)
            {
                var go = new GameObject("WeatherHazardManager");
                weatherManager = go.AddComponent<SteamRush.Features.Environment.WeatherHazardManager>();
            }

            weatherManager.TriggerWeatherHazard(60f);
            hudManager?.ShowAntiAction(sender, "Rain & Fog (60s)");
            Debug.Log($"[MockRunnerActionHandler] {sender} Weather Hazard 60s (FX_Rain & Fog -3.5f).");
        }

        public void MockBuyVipTicket(string customUserId = null)
        {
            AutoWireReferences();
            if (queueManager != null)
            {
                string newId = string.IsNullOrEmpty(customUserId) ? ("Follower_" + Random.Range(100, 999)) : customUserId;
                queueManager.EnqueueFollowerAsRunner(newId);
                hudManager?.ShowFanAction(newId, "Followed -> Next Runner!");
            }
            else
            {
                Debug.LogWarning("[MockRunnerActionHandler] ChatRunnerQueueManager not found.");
            }
        }

        public void MockToggleUnlimitedModeDebug()
        {
            AutoWireReferences();
            if (obstacleSpawner != null)
            {
                bool newState = !obstacleSpawner.IsUnlimitedModeActive;
                obstacleSpawner.SetUnlimitedModeDebug(newState);
                hudManager?.ShowStatusPopup(newState ? "[DEBUG] Unlimited Mode: ON" : "[DEBUG] Unlimited Mode: OFF", false);
            }
            else
            {
                Debug.LogWarning("[MockRunnerActionHandler] SingleObstacleSpawner not found.");
            }
        }

        public void DebugIncreaseFanEnergy(int amount = 10)
        {
            AutoWireReferences();
            if (factionManager != null)
            {
                factionManager.DebugAdjustFanEnergy(amount);
                hudManager?.ShowStatusPopup($"Blue Energy +{amount}", true);
            }
        }

        public void DebugDecreaseFanEnergy(int amount = 10)
        {
            AutoWireReferences();
            if (factionManager != null)
            {
                factionManager.DebugAdjustFanEnergy(-amount);
                hudManager?.ShowStatusPopup($"Blue Energy -{amount}", true);
            }
        }

        public void DebugIncreaseAntiEnergy(int amount = 10)
        {
            AutoWireReferences();
            if (factionManager != null)
            {
                factionManager.DebugAdjustAntiEnergy(amount);
                hudManager?.ShowStatusPopup($"Red Energy +{amount}", false);
            }
        }

        public void DebugDecreaseAntiEnergy(int amount = 10)
        {
            AutoWireReferences();
            if (factionManager != null)
            {
                factionManager.DebugAdjustAntiEnergy(-amount);
                hudManager?.ShowStatusPopup($"Red Energy -{amount}", false);
            }
        }

        public void MockTriggerFinishLineApproach(float distanceAhead = 50f)
        {
            var progressTracker = FindFirstObjectByType<SteamRush.Track.TrackProgressTracker>();
            if (progressTracker != null)
            {
                progressTracker.DebugJumpNearGoal(distanceAhead);
            }

            var archway = FindFirstObjectByType<SteamRush.Track.FinishLineArchway>();
            if (archway != null)
            {
                archway.DebugSpawnArchway(distanceAhead);
            }

            Debug.Log($"[MockRunnerActionHandler] F12 -> Jumped to {distanceAhead:F0}m before Finish Line!");
        }

        public void ToggleLiveDemoSimulation()
        {
            var demo = LiveSessionDemoRunner.Instance ?? FindFirstObjectByType<LiveSessionDemoRunner>();
            if (demo != null)
            {
                demo.ToggleLiveDemo();
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                string stateText = demo.IsRunning ? "ON" : "OFF";
                hudManager?.ShowStatusPopup($"[Live Demo] Bot Simulation: {stateText}", demo.IsRunning);
            }
        }
    }
}

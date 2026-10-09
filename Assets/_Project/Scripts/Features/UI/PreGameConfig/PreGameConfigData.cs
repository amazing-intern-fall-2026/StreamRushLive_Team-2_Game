using System;
using System.Collections.Generic;
using SteamRush.Features.StreamIntegration;

namespace SteamRush.Features.UI.PreGameConfig
{
    [Serializable]
    public class AvailableGiftInfo
    {
        public int giftId;
        public string giftName;
        public AvailableGiftInfo() { }
        public AvailableGiftInfo(int id, string name)
        {
            giftId = id;
            giftName = name;
        }
    }

    [Serializable]
    public class PreGameGiftItemConfig
    {
        public string featureName;
        public int giftId;
        public string giftName;
        public string description;
        public bool isEnabled = true;
        public GiftActionType action;
        public float customValue = 0f;

        public PreGameGiftItemConfig() { }

        public PreGameGiftItemConfig(string featName, int id, string name, string desc, GiftActionType act, bool enabled = true, float val = 0f)
        {
            featureName = featName;
            giftId = id;
            giftName = name;
            description = desc;
            action = act;
            isEnabled = enabled;
            customValue = val > 0f ? val : GetDefaultStat(act);
        }

        public PreGameGiftItemConfig(int id, string name, string desc, GiftActionType act, bool enabled = true, float val = 0f)
            : this(GetDefaultFeatureName(act), id, name, desc, act, enabled, val) { }

        public static bool HasStatForAction(GiftActionType act)
        {
            switch (act)
            {
                case GiftActionType.Follow_Runner:
                case GiftActionType.Special_VIPRelayTicket:
                case GiftActionType.Dynamic_ByFaction:
                    return false;
                default:
                    return true;
            }
        }

        public static float GetDefaultStat(GiftActionType act)
        {
            switch (act)
            {
                case GiftActionType.Like_Energy: return 20f;
                case GiftActionType.Blue_Shield: return 15f;
                case GiftActionType.Blue_SpeedBoost: return 30f;
                case GiftActionType.Blue_FreeControl: return 30f;
                case GiftActionType.Blue_EnergyBottle: return 300f;
                case GiftActionType.Red_SpawnPickup: return 60f;
                case GiftActionType.Red_SpawnHeavyTruck: return 60f;
                case GiftActionType.Red_UnlimitedCars: return 60f;
                case GiftActionType.Red_EnergyBottle: return 500f;
                case GiftActionType.Special_GiftDance: return 5f;
                case GiftActionType.Special_RainHazard: return 60f;
                default: return 0f;
            }
        }

        public static string GetStatUnit(GiftActionType act)
        {
            switch (act)
            {
                case GiftActionType.Blue_Shield:
                case GiftActionType.Blue_SpeedBoost:
                case GiftActionType.Blue_FreeControl:
                case GiftActionType.Red_SpawnPickup:
                case GiftActionType.Red_SpawnHeavyTruck:
                case GiftActionType.Red_UnlimitedCars:
                case GiftActionType.Special_GiftDance:
                case GiftActionType.Special_RainHazard:
                    return "s";
                case GiftActionType.Blue_EnergyBottle:
                case GiftActionType.Red_EnergyBottle:
                case GiftActionType.Like_Energy:
                    return "nrg";
                default:
                    return "";
            }
        }

        public static string GetDefaultFeatureName(GiftActionType act)
        {
            switch (act)
            {
                case GiftActionType.Blue_Shield: return "Protection Shield";
                case GiftActionType.Blue_SpeedBoost: return "Speed Boost (Turbo)";
                case GiftActionType.Blue_FreeControl: return "Freedom Charm";
                case GiftActionType.Blue_EnergyBottle: return "Blue Energy";
                case GiftActionType.Red_SpawnPickup: return "Spawn Hunting Beasts";
                case GiftActionType.Red_SpawnHeavyTruck: return "Spawn Train";
                case GiftActionType.Red_UnlimitedCars: return "Car Storm (Unlimited)";
                case GiftActionType.Red_EnergyBottle: return "Red Energy";
                case GiftActionType.Special_GiftDance: return "Dance";
                case GiftActionType.Special_RainHazard: return "Rain Storm Hazard";
                case GiftActionType.Follow_Runner: return "Next Runner";
                case GiftActionType.Like_Energy: return "Team Energy (Likes)";
                default: return act.ToString();
            }
        }
    }

    [Serializable]
    public class PreGameConfigData
    {
        // === A. BASIC CONFIG ===
        public string tiktokUsername = "";
        public int targetDistanceOptionIndex = 1; // 0: 500m, 1: 1km, 2: 5km, 3: Infinite
        public float targetDistanceMeters = 1000f;
        public float finiteTargetDistanceMeters = 1000f;
        public bool isInfiniteDistance = false;
        public List<PreGameGiftItemConfig> gifts = new List<PreGameGiftItemConfig>();

        // === B. ADVANCED CONFIG ===
        // Fan & Energy
        public int laneChangeEnergyCost = 10;
        public int jumpEnergyCost = 20;
        public float freeControlDuration = 30f;
        public float sprintBuffDuration = 30f;

        // Anti & Obstacles
        public bool autoSpawnOnFullEnergy = true;
        public int maxConcurrentCars = 2;

        // Collision Penalties
        public float distancePenaltyMeters = 100f;
        public float sedanEnergyPenaltyPercent = 20f;
        public float pickupEnergyPenaltyPercent = 40f;
        public float heavyTruckEnergyPenaltyPercent = 60f;

        // === C. LIVE DEMO SIMULATION (LiveSessionDemoRunner) ===
        public bool enableLiveDemoSimulation = false;
        public bool enableSimulatedChats = false;
        public bool enableSimulatedGifts = false;
        public bool enableSimulatedLikes = false;
        public bool enableSimulatedFollowers = false;
        public bool enableSimulatedStreamDelay = false;

        // === D. DEBUG & TESTING TOOLS ===
        public bool showHowToPlayGuide = true;
        public bool showGiftInfoPanel = true;
        public bool showStopwatchTimer = true;
        public bool showTimerCircles = true;
        public bool enableDebugUI = false;

        // === E. TIKTOK LIVE BACKEND & PORT CONFIG ===
        public int backendPort = 9091;
        public int backendSocketPort = 3001;
        public string eulerApiKey = "";
        public string backendDirectory = GetDefaultBackendDirectory();

        public static string GetDefaultBackendDirectory()
        {
            string localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData))
            {
                string userProfile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                localAppData = System.IO.Path.Combine(userProfile, "AppData", "Local");
            }
            return System.IO.Path.Combine(localAppData, "backendLiveGame");
        }

        public static readonly AvailableGiftInfo[] AvailableGifts = new[]
        {
            new AvailableGiftInfo(5655, "Rose"),
            new AvailableGiftInfo(5487, "Heart"),
            new AvailableGiftInfo(5269, "TikTok"),
            new AvailableGiftInfo(5879, "Cap"),
            new AvailableGiftInfo(5338, "Donut"),
            new AvailableGiftInfo(5585, "Dumbbell"),
            new AvailableGiftInfo(6001, "Lion"),
            new AvailableGiftInfo(5661, "Sunglasses"),
            new AvailableGiftInfo(5586, "Chili"),
            new AvailableGiftInfo(6037, "Meme Dance"),
            new AvailableGiftInfo(5978, "Rain"),
            new AvailableGiftInfo(0, "Follow")
        };

        public static PreGameConfigData CreateDefault()
        {
            var data = new PreGameConfigData
            {
                tiktokUsername = "",
                targetDistanceOptionIndex = 1,
                targetDistanceMeters = 1000f,
                finiteTargetDistanceMeters = 1000f,
                isInfiniteDistance = false,
                laneChangeEnergyCost = 10,
                jumpEnergyCost = 20,
                freeControlDuration = 30f,
                sprintBuffDuration = 30f,
                autoSpawnOnFullEnergy = true,
                maxConcurrentCars = 2,
                distancePenaltyMeters = 100f,
                sedanEnergyPenaltyPercent = 20f,
                pickupEnergyPenaltyPercent = 40f,
                heavyTruckEnergyPenaltyPercent = 60f,
                enableLiveDemoSimulation = false,
                enableSimulatedChats = false,
                enableSimulatedGifts = false,
                enableSimulatedLikes = false,
                enableSimulatedFollowers = false,
                enableSimulatedStreamDelay = false,
                eulerApiKey = "",
                backendDirectory = GetDefaultBackendDirectory(),
                gifts = new List<PreGameGiftItemConfig>
                {
                    new PreGameGiftItemConfig("Team Energy (Likes)", 5487, "Heart", "x20: 20 Team Energy", GiftActionType.Like_Energy, true, 20f),
                    new PreGameGiftItemConfig("Protection Shield", 5655, "Rose", "Protection Shield", GiftActionType.Blue_Shield, true, 15f),
                    new PreGameGiftItemConfig("Speed Boost (Turbo)", 5269, "TikTok", "Turbo Speed", GiftActionType.Blue_SpeedBoost, true, 30f),
                    new PreGameGiftItemConfig("Freedom Charm", 5879, "Cap", "Freedom Charm", GiftActionType.Blue_FreeControl, true, 30f),
                    new PreGameGiftItemConfig("Blue Energy", 5338, "Donut", "Blue Energy", GiftActionType.Blue_EnergyBottle, true, 300f),
                    new PreGameGiftItemConfig("Spawn Hunting Beasts", 5585, "Dumbbell", "Hunting Beasts", GiftActionType.Red_SpawnPickup, true, 60f),
                    new PreGameGiftItemConfig("Spawn Train", 6001, "Lion", "Train", GiftActionType.Red_SpawnHeavyTruck, true, 60f),
                    new PreGameGiftItemConfig("Car Storm (Unlimited)", 5661, "Sunglasses", "Car Storm", GiftActionType.Red_UnlimitedCars, true, 60f),
                    new PreGameGiftItemConfig("Red Energy", 5586, "Chili", "Red Energy", GiftActionType.Red_EnergyBottle, true, 500f),
                    new PreGameGiftItemConfig("Dance", 6037, "Meme Dance", "Dance", GiftActionType.Special_GiftDance, true, 5f),
                    new PreGameGiftItemConfig("Rain Storm Hazard", 5978, "Rain", "Rain Storm", GiftActionType.Special_RainHazard, true, 60f),
                    new PreGameGiftItemConfig("Next Runner", 0, "Follow", "Next Runner", GiftActionType.Follow_Runner, true, 0f)
                }
            };
            return data;
        }
    }
}

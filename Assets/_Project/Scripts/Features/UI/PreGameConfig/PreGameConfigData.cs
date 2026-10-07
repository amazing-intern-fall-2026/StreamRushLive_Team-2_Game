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

        public PreGameGiftItemConfig() { }

        public PreGameGiftItemConfig(string featName, int id, string name, string desc, GiftActionType act, bool enabled = true)
        {
            featureName = featName;
            giftId = id;
            giftName = name;
            description = desc;
            action = act;
            isEnabled = enabled;
        }

        public PreGameGiftItemConfig(int id, string name, string desc, GiftActionType act, bool enabled = true)
            : this(GetDefaultFeatureName(act), id, name, desc, act, enabled) { }

        public static string GetDefaultFeatureName(GiftActionType act)
        {
            switch (act)
            {
                case GiftActionType.Blue_Shield: return "Protection Shield";
                case GiftActionType.Blue_SpeedBoost: return "Speed Boost (Turbo)";
                case GiftActionType.Blue_FreeControl: return "Freedom Charm";
                case GiftActionType.Blue_EnergyBottle: return "+300 Blue Energy";
                case GiftActionType.Red_SpawnPickup: return "Spawn Hunting Beasts";
                case GiftActionType.Red_SpawnHeavyTruck: return "Spawn Train";
                case GiftActionType.Red_UnlimitedCars: return "Car Storm (Unlimited)";
                case GiftActionType.Red_EnergyBottle: return "+Red Energy";
                case GiftActionType.Special_GiftDance: return "Meme Dance";
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
        public bool enableLiveDemoSimulation = true;
        public bool enableSimulatedChats = true;
        public bool enableSimulatedGifts = true;
        public bool enableSimulatedLikes = true;
        public bool enableSimulatedFollowers = false;
        public bool enableSimulatedStreamDelay = false;

        // === D. DEBUG & TESTING TOOLS ===
        public bool enableDebugUI = false;

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
                enableLiveDemoSimulation = true,
                enableSimulatedChats = true,
                enableSimulatedGifts = true,
                enableSimulatedLikes = true,
                enableSimulatedFollowers = false,
                enableSimulatedStreamDelay = false,
                gifts = new List<PreGameGiftItemConfig>
                {
                    new PreGameGiftItemConfig("Team Energy (Likes)", 5487, "Heart", "x20: 20 Team Energy", GiftActionType.Like_Energy, true),
                    new PreGameGiftItemConfig("Protection Shield", 5655, "Rose", "Protection Shield", GiftActionType.Blue_Shield, true),
                    new PreGameGiftItemConfig("Speed Boost (Turbo)", 5269, "TikTok", "Turbo Speed (20s)", GiftActionType.Blue_SpeedBoost, true),
                    new PreGameGiftItemConfig("Freedom Charm", 5879, "Cap", "Freedom Charm", GiftActionType.Blue_FreeControl, true),
                    new PreGameGiftItemConfig("+300 Blue Energy", 5338, "Donut", "+300 Blue Energy", GiftActionType.Blue_EnergyBottle, true),
                    new PreGameGiftItemConfig("Spawn Hunting Beasts", 5585, "Dumbbell", "Hunting Beasts", GiftActionType.Red_SpawnPickup, true),
                    new PreGameGiftItemConfig("Spawn Train", 6001, "Lion", "Train", GiftActionType.Red_SpawnHeavyTruck, true),
                    new PreGameGiftItemConfig("Car Storm (Unlimited)", 5661, "Sunglasses", "Car Storm", GiftActionType.Red_UnlimitedCars, true),
                    new PreGameGiftItemConfig("+Red Energy", 5586, "Chili", "+Red Energy", GiftActionType.Red_EnergyBottle, true),
                    new PreGameGiftItemConfig("Meme Dance", 6037, "Meme Dance", "Meme Dance", GiftActionType.Special_GiftDance, true),
                    new PreGameGiftItemConfig("Rain Storm Hazard", 5978, "Rain", "Rain Storm", GiftActionType.Special_RainHazard, true),
                    new PreGameGiftItemConfig("Next Runner", 0, "Follow", "Next Runner", GiftActionType.Follow_Runner, true)
                }
            };
            return data;
        }
    }
}

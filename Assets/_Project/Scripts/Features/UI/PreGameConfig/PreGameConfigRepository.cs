using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using SteamRush.Core.Localization;
using SteamRush.Features.StreamIntegration;
using StreamRushLive.Features.Gifts;

namespace SteamRush.Features.UI.PreGameConfig
{
    /// <summary>
    /// Single Responsibility: Loads, sanitizes, syncs, and persists PreGameConfigData
    /// to local JSON storage (persistentDataPath) and PlayerPrefs fallback.
    /// </summary>
    public class PreGameConfigRepository
    {
        public string SaveFilePath => Path.Combine(Application.persistentDataPath, "pregame_settings.json");

        public PreGameConfigData LoadConfig()
        {
            PreGameConfigData config = null;

            try
            {
                if (File.Exists(SaveFilePath))
                {
                    string json = File.ReadAllText(SaveFilePath);
                    config = JsonUtility.FromJson<PreGameConfigData>(json);
                    Debug.Log($"[PreGameConfigRepository] Loaded config from {SaveFilePath}");
                }
                else if (PlayerPrefs.HasKey("PreGameConfig_JSON"))
                {
                    string json = PlayerPrefs.GetString("PreGameConfig_JSON");
                    config = JsonUtility.FromJson<PreGameConfigData>(json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PreGameConfigRepository] Error reading config file: {ex.Message}. Reverting to defaults.");
                config = null;
            }

            if (config == null || config.gifts == null || config.gifts.Count == 0)
            {
                config = PreGameConfigData.CreateDefault();
                SaveConfig(config);
            }
            else
            {
                SanitizeAndSyncConfig(config);
            }

            return config;
        }

        public void SaveConfig(PreGameConfigData config)
        {
            if (config == null) return;

            try
            {
                if (LocalizationManager.Instance != null)
                {
                    config.language = LocalizationManager.Instance.CurrentLanguageCode;
                }

                string json = JsonUtility.ToJson(config, true);
                File.WriteAllText(SaveFilePath, json);
                PlayerPrefs.SetString("PreGameConfig_JSON", json);
                PlayerPrefs.Save();
                Debug.Log($"[PreGameConfigRepository] Saved config to {SaveFilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PreGameConfigRepository] Failed to save config: {ex.Message}");
            }
        }

        private void SanitizeAndSyncConfig(PreGameConfigData config)
        {
            SyncWithDefaultGifts(config);

            // Migrate old default path D:\Download\backendLiveGame to AppData Local
            if (string.IsNullOrEmpty(config.backendDirectory) || config.backendDirectory == @"D:\Download\backendLiveGame")
            {
                config.backendDirectory = PreGameConfigData.GetDefaultBackendDirectory();
            }

            if (!string.IsNullOrEmpty(config.tiktokUsername) && config.tiktokUsername.Equals("Username", StringComparison.OrdinalIgnoreCase))
            {
                config.tiktokUsername = "";
            }

            // Sanitize legacy target distance values
            if (config.finiteTargetDistanceMeters <= 0f || config.finiteTargetDistanceMeters >= 900000000f)
            {
                config.finiteTargetDistanceMeters = (config.targetDistanceMeters > 0f && config.targetDistanceMeters < 900000000f)
                    ? config.targetDistanceMeters
                    : 1000000f;
            }

            if (!config.isInfiniteDistance)
            {
                if (config.targetDistanceMeters >= 900000000f || config.targetDistanceMeters <= 0f)
                {
                    config.targetDistanceMeters = config.finiteTargetDistanceMeters;
                }
            }

            // Default inactivity timeout to 120s (2 minutes) if unassigned
            if (config.memberInactivityTimeoutSeconds <= 0f)
            {
                config.memberInactivityTimeoutSeconds = 120f;
            }

            // Temporarily disable manual keyboard controls
            config.enableKeyboardControl = false;
        }

        private void SyncWithDefaultGifts(PreGameConfigData config)
        {
            var def = PreGameConfigData.CreateDefault();
            var existingActions = new HashSet<GiftActionType>();
            foreach (var g in config.gifts)
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
                    config.gifts.Add(g);
                }
            }
        }
    }
}

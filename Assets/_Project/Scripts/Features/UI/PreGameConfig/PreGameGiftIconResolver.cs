using System;
using System.Collections.Generic;
using UnityEngine;

namespace SteamRush.Features.UI.PreGameConfig
{
    /// <summary>
    /// Utility for resolving TikTok gift icons from router mappings, in-memory master cache, asset paths, or resources.
    /// Preserves icons across disabling/enabling cycles in both Editor and Standalone Build runtimes.
    /// Single responsibility: Gift icon resolution.
    /// </summary>
    public static class PreGameGiftIconResolver
    {
        private static readonly Dictionary<int, Sprite> _cacheById = new Dictionary<int, Sprite>();
        private static readonly Dictionary<string, Sprite> _cacheByName = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a sprite in the master cache so it persists even if router mappings are filtered.
        /// </summary>
        public static void RegisterIcon(int giftId, string giftName, Sprite icon)
        {
            if (icon == null) return;
            if (giftId > 0)
            {
                _cacheById[giftId] = icon;
            }
            if (!string.IsNullOrWhiteSpace(giftName))
            {
                string key = giftName.Trim();
                _cacheByName[key] = icon;
            }
        }

        public static Sprite ResolveGiftIcon(int giftId, string giftName)
        {
            // 1. Check in-memory master cache
            if (giftId > 0 && _cacheById.TryGetValue(giftId, out var cachedById) && cachedById != null)
            {
                return cachedById;
            }
            if (!string.IsNullOrWhiteSpace(giftName) && _cacheByName.TryGetValue(giftName.Trim(), out var cachedByName) && cachedByName != null)
            {
                return cachedByName;
            }

            // 2. Check TikTokGiftRouter mappings (including inactive)
            var router = UnityEngine.Object.FindAnyObjectByType<StreamIntegration.TikTokGiftRouter>(FindObjectsInactive.Include);
            if (router != null && router.GiftMappings != null)
            {
                var found = router.GiftMappings.Find(m =>
                    (giftId > 0 && m.giftId == giftId) ||
                    (!string.IsNullOrEmpty(giftName) && string.Equals(m.giftName, giftName, StringComparison.OrdinalIgnoreCase)));
                if (found != null && found.giftIcon != null)
                {
                    RegisterIcon(giftId, giftName, found.giftIcon);
                    return found.giftIcon;
                }
            }

            // 3. Check Resources/TikTokGifts (Works in both Editor and Standalone .exe Builds)
            Sprite resSprite = LoadFromResources(giftId, giftName);
            if (resSprite != null)
            {
                RegisterIcon(giftId, giftName, resSprite);
                return resSprite;
            }

#if UNITY_EDITOR
            // 4. Editor fallback: AssetDatabase
            string dir = "Assets/_Project/Textures/TikTokGifts";
            if (giftId > 0)
            {
                var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{giftId}.png");
                if (sp != null)
                {
                    RegisterIcon(giftId, giftName, sp);
                    return sp;
                }
            }

            if (!string.IsNullOrEmpty(giftName))
            {
                string lower = giftName.ToLowerInvariant().Trim();
                var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{lower}.png");
                if (sp != null)
                {
                    RegisterIcon(giftId, giftName, sp);
                    return sp;
                }

                // Keyword fallback matching
                if (lower.Contains("heart") || lower.Contains("like")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5487.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/heart.png");
                else if (lower.Contains("rose")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5655.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/rose.png");
                else if (lower.Contains("tiktok") || lower.Contains("speed")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5269.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/tiktok.png");
                else if (lower.Contains("cap") || lower.Contains("hat") || lower.Contains("freedom")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5879.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/cap.png");
                else if (lower.Contains("donut")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5338.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/donut.png");
                else if (lower.Contains("dumbbell") || lower.Contains("beast") || lower.Contains("pickup")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5585.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/dumbbell.png");
                else if (lower.Contains("lion") || lower.Contains("train")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/6001.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/lion.png");
                else if (lower.Contains("sunglasses") || lower.Contains("car storm") || lower.Contains("unlimited")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5661.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/sunglasses.png");
                else if (lower.Contains("chili")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5586.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/chili.png");
                else if (lower.Contains("dance") || lower.Contains("meme")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/6037.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/dance.png");
                else if (lower.Contains("rain") || lower.Contains("weather")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5978.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/rain.png");
                else if (lower.Contains("follow") || lower.Contains("runner") || lower.Contains("next")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/follow.png");

                if (sp != null)
                {
                    RegisterIcon(giftId, giftName, sp);
                    return sp;
                }
            }
#endif

            return null;
        }

        private static Sprite LoadFromResources(int giftId, string giftName)
        {
            if (giftId > 0)
            {
                var sp = Resources.Load<Sprite>($"TikTokGifts/{giftId}");
                if (sp != null) return sp;
            }

            if (!string.IsNullOrEmpty(giftName))
            {
                string lower = giftName.ToLowerInvariant().Trim();
                var sp = Resources.Load<Sprite>($"TikTokGifts/{lower}");
                if (sp != null) return sp;

                // Keyword fallback for Resources
                if (lower.Contains("heart") || lower.Contains("like")) return Resources.Load<Sprite>("TikTokGifts/5487") ?? Resources.Load<Sprite>("TikTokGifts/heart");
                if (lower.Contains("rose")) return Resources.Load<Sprite>("TikTokGifts/5655") ?? Resources.Load<Sprite>("TikTokGifts/rose");
                if (lower.Contains("tiktok") || lower.Contains("speed")) return Resources.Load<Sprite>("TikTokGifts/5269") ?? Resources.Load<Sprite>("TikTokGifts/tiktok");
                if (lower.Contains("cap") || lower.Contains("hat") || lower.Contains("freedom")) return Resources.Load<Sprite>("TikTokGifts/5879") ?? Resources.Load<Sprite>("TikTokGifts/cap");
                if (lower.Contains("donut")) return Resources.Load<Sprite>("TikTokGifts/5338") ?? Resources.Load<Sprite>("TikTokGifts/donut");
                if (lower.Contains("dumbbell") || lower.Contains("beast") || lower.Contains("pickup")) return Resources.Load<Sprite>("TikTokGifts/5585") ?? Resources.Load<Sprite>("TikTokGifts/dumbbell");
                if (lower.Contains("lion") || lower.Contains("train")) return Resources.Load<Sprite>("TikTokGifts/6001") ?? Resources.Load<Sprite>("TikTokGifts/lion");
                if (lower.Contains("sunglasses") || lower.Contains("car storm") || lower.Contains("unlimited")) return Resources.Load<Sprite>("TikTokGifts/5661") ?? Resources.Load<Sprite>("TikTokGifts/sunglasses");
                if (lower.Contains("chili")) return Resources.Load<Sprite>("TikTokGifts/5586") ?? Resources.Load<Sprite>("TikTokGifts/chili");
                if (lower.Contains("dance") || lower.Contains("meme")) return Resources.Load<Sprite>("TikTokGifts/6037") ?? Resources.Load<Sprite>("TikTokGifts/dance");
                if (lower.Contains("rain") || lower.Contains("weather")) return Resources.Load<Sprite>("TikTokGifts/5978") ?? Resources.Load<Sprite>("TikTokGifts/rain");
                if (lower.Contains("follow") || lower.Contains("runner") || lower.Contains("next")) return Resources.Load<Sprite>("TikTokGifts/follow");
            }

            return null;
        }
    }
}

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
        private static readonly Dictionary<string, Sprite> _canonicalCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, Sprite> _cacheById = new Dictionary<int, Sprite>();
        private static readonly Dictionary<string, Sprite> _cacheByName = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        public static void ClearCache()
        {
            _canonicalCache.Clear();
            _cacheById.Clear();
            _cacheByName.Clear();
        }

        /// <summary>
        /// Registers a sprite in the master cache so it persists even if router mappings are filtered.
        /// </summary>
        public static void RegisterIcon(int giftId, string giftName, Sprite icon)
        {
            if (icon == null) return;

            string canonical = GetCanonicalGiftSpriteName(giftId, giftName);
            if (!string.IsNullOrEmpty(canonical) && _canonicalCache.ContainsKey(canonical))
            {
                return;
            }

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

        /// <summary>
        /// Resolves the canonical sprite file name without extension for any gift ID or gift name.
        /// Ensures consistent and correct sprites across Editor and Standalone builds.
        /// </summary>
        public static string GetCanonicalGiftSpriteName(int giftId, string giftName)
        {
            string lower = (giftName ?? "").Trim().ToLowerInvariant();

            // 1. Heart vs Finger Heart (Rule: Heart is Like Heart, not the coin gift!)
            if (lower.Contains("finger") && lower.Contains("heart")) return "fingerheart";
            if (lower.Contains("hand") && lower.Contains("heart")) return "handhearts";
            if (lower == "heart" || lower.Contains("like") || lower.Contains("tim")) return "heart";

            // 2. Exact and keyword gift names
            if (lower == "ice cream" || lower == "icecream" || lower == "ice cream cone" || lower == "icecreamcone") return "icecream";
            if (lower == "gg") return "gg";
            if (lower == "panda") return "panda";
            if (lower == "mic" || lower == "microphone") return "mic";
            if (lower == "cap" || lower == "hat" || lower == "freedom") return "cap";
            if (lower == "dumbbell" || lower == "dumbell") return "dumbbell";
            if (lower == "sunglasses" || lower == "glasses") return "sunglasses";
            if (lower == "rose") return "rose";
            if (lower == "tiktok" || lower == "speed") return "tiktok";
            if (lower == "donut" || lower == "doughnut") return "donut";
            if (lower == "chili" || lower == "pepper") return "chili";
            if (lower == "rain" || lower.Contains("rain storm") || lower.Contains("rainstorm")) return "rain";
            if (lower == "train") return "train";
            if (lower == "dance") return "dance";
            if (lower == "money gun" || lower == "moneygun") return "moneygun";
            if (lower == "boxing gloves" || lower == "boxinggloves" || lower == "gloves") return "boxinggloves";
            if (lower == "swan") return "swan";
            if (lower == "fireworks" || lower == "firework" || lower == "fire") return "fireworks";
            if (lower == "crown") return "crown";
            if (lower == "whale") return "whale";
            if (lower == "sports car" || lower == "sportscar" || lower == "car") return "sportscar";
            if (lower == "yacht") return "yacht";
            if (lower == "airplane" || lower == "plane" || lower == "jetplane") return "airplane";
            if (lower == "rocket") return "rocket";
            if (lower == "pirate ship" || lower == "pirateship") return "pirateship";
            if (lower == "ferris wheel" || lower == "ferriswheel") return "ferriswheel";
            if (lower == "lion") return "lion";
            if (lower == "planet") return "planet";
            if (lower == "interstellar") return "interstellar";
            if (lower == "universe") return "universe";
            if (lower == "trophy" || lower.Contains("trophy")) return "matchtrophy";
            if (lower == "gamepad") return "gamepad";
            if (lower == "vip") return "vip";
            if (lower == "follow" || lower.Contains("runner") || lower == "next") return "follow";

            // 3. Fallback by Gift ID
            switch (giftId)
            {
                case 5655: return "rose";
                case 5269: return "tiktok";
                case 5827: return "icecream";
                case 6064: return "gg";
                case 37: return "panda";
                case 5650: return "mic";
                case 6052: return "gamepad";
                case 5586: return "chili";
                case 5338: return "donut";
                case 6104:
                case 5879: return "cap";
                case 5585: return "dumbbell";
                case 5509:
                case 5661: return "sunglasses";
                case 5659: return "moneygun";
                case 6007: return "boxinggloves";
                case 5897: return "swan";
                case 6090: return "fireworks";
                case 6097: return "crown";
                case 5656: return "whale";
                case 6089: return "sportscar";
                case 6103: return "yacht";
                case 5883:
                case 5665: return "airplane";
                case 5930: return "rocket";
                case 5951: return "pirateship";
                case 5652: return "ferriswheel";
                case 6001:
                case 6223: return "lion";
                case 5954: return "planet";
                case 6149: return "interstellar";
                case 6038: return "universe";
                case 6094: return "matchtrophy";
                case 6037: return "dance";
                case 5978: return lower.Contains("train") ? "train" : "rain";
                case 5487: return lower.Contains("finger") ? "fingerheart" : "heart";
            }

            return null;
        }

        public static Sprite ResolveGiftIcon(int giftId, string giftName)
        {
            string canonical = GetCanonicalGiftSpriteName(giftId, giftName);

            // 1. Try canonical sprite first (Top priority - Single Source of Truth)
            if (!string.IsNullOrEmpty(canonical))
            {
                if (_canonicalCache.TryGetValue(canonical, out var cached) && cached != null)
                {
                    return cached;
                }

                var sp = Resources.Load<Sprite>($"TikTokGifts/{canonical}");
#if UNITY_EDITOR
                if (sp == null)
                {
                    sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Textures/TikTokGifts/{canonical}.png");
                }
#endif
                if (sp != null)
                {
                    _canonicalCache[canonical] = sp;
                    return sp;
                }
            }

            // 2. Check registered cache by exact giftName
            if (!string.IsNullOrWhiteSpace(giftName) && _cacheByName.TryGetValue(giftName.Trim(), out var cachedByName) && cachedByName != null)
            {
                return cachedByName;
            }

            // 3. Check registered cache by giftId
            if (giftId > 0 && _cacheById.TryGetValue(giftId, out var cachedById) && cachedById != null)
            {
                return cachedById;
            }

            // 4. Try loading by giftId from Resources / AssetDatabase
            if (giftId > 0)
            {
                var spId = Resources.Load<Sprite>($"TikTokGifts/{giftId}");
#if UNITY_EDITOR
                if (spId == null)
                {
                    spId = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Textures/TikTokGifts/{giftId}.png");
                }
#endif
                if (spId != null)
                {
                    _cacheById[giftId] = spId;
                    return spId;
                }
            }

            // 5. Generic name fallback
            if (!string.IsNullOrEmpty(giftName))
            {
                string lower = giftName.ToLowerInvariant().Trim();
                string clean = System.Text.RegularExpressions.Regex.Replace(lower, @"[^a-z0-9]", "");
                var sp = Resources.Load<Sprite>($"TikTokGifts/{lower}") ?? Resources.Load<Sprite>($"TikTokGifts/{clean}");
#if UNITY_EDITOR
                if (sp == null)
                {
                    string dir = "Assets/_Project/Textures/TikTokGifts";
                    sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{lower}.png") ??
                         UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{clean}.png");
                }
#endif
                if (sp != null)
                {
                    _cacheByName[giftName.Trim()] = sp;
                    return sp;
                }
            }

            return null;
        }
    }
}

using System;
using UnityEngine;

namespace SteamRush.Features.UI.PreGameConfig
{
    /// <summary>
    /// Utility for resolving TikTok gift icons from router mappings, asset paths, or resources.
    /// Single responsibility: Gift icon resolution.
    /// </summary>
    public static class PreGameGiftIconResolver
    {
        public static Sprite ResolveGiftIcon(int giftId, string giftName)
        {
            var router = UnityEngine.Object.FindFirstObjectByType<StreamIntegration.TikTokGiftRouter>();
            if (router != null && router.GiftMappings != null)
            {
                var found = router.GiftMappings.Find(m =>
                    (giftId > 0 && m.giftId == giftId) ||
                    (!string.IsNullOrEmpty(giftName) && string.Equals(m.giftName, giftName, StringComparison.OrdinalIgnoreCase)));
                if (found != null && found.giftIcon != null) return found.giftIcon;
            }

#if UNITY_EDITOR
            string dir = "Assets/_Project/Textures/TikTokGifts";
            if (giftId > 0)
            {
                var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{giftId}.png");
                if (sp != null) return sp;
            }

            if (!string.IsNullOrEmpty(giftName))
            {
                string lower = giftName.ToLowerInvariant().Trim();
                var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{lower}.png");
                if (sp != null) return sp;

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

                if (sp != null) return sp;
            }
#endif

            if (giftId > 0)
            {
                var sp = Resources.Load<Sprite>($"TikTokGifts/{giftId}");
                if (sp != null) return sp;
            }
            if (!string.IsNullOrEmpty(giftName))
            {
                var sp = Resources.Load<Sprite>($"TikTokGifts/{giftName.ToLowerInvariant().Trim()}");
                if (sp != null) return sp;
            }

            return null;
        }
    }
}

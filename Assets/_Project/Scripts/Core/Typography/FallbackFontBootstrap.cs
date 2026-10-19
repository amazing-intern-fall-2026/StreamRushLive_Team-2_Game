using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SteamRush.Core.Typography
{
    // Appends OS-installed CJK fonts (dynamic atlas) to the global TMP fallback list at startup.
    // Keeps the build small: only Noto Sans / Noto Sans Arabic ship with the project, while
    // Chinese / Japanese / Korean glyphs come from fonts already on the streamer's Windows PC.
    public static class FallbackFontBootstrap
    {
        // Order matters: first font that has the glyph wins.
        // Style is the preferred face (matches the Bold nameplate font); Regular is the fallback.
        private static readonly (string family, string style)[] OsFontFamilies =
        {
            ("Microsoft YaHei", "Bold"),      // Simplified Chinese (also covers most Han used in JP/KR names)
            ("Microsoft JhengHei", "Bold"),   // Traditional Chinese
            ("Yu Gothic", "Bold"),            // Japanese kana
            ("Meiryo", "Bold"),
            ("Malgun Gothic", "Bold"),        // Korean hangul
            ("Segoe UI Symbol", "Regular"),   // misc symbols / math alphanumerics (ships Regular only)
        };

        private static readonly List<TMP_FontAsset> _created = new List<TMP_FontAsset>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _created.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_created.Count > 0) return;

            var installed = new HashSet<string>(Font.GetOSInstalledFontNames());
            var fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks == null) return;

            foreach (var (family, style) in OsFontFamilies)
            {
                if (!installed.Contains(family)) continue;

                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(family, style, 90);
                if (asset == null && style != "Regular") asset = TMP_FontAsset.CreateFontAsset(family, "Regular", 90);
                if (asset == null)
                {
                    Debug.LogWarning($"[FallbackFontBootstrap] Could not create font asset for OS font '{family}'.");
                    continue;
                }

                asset.name = $"OS Fallback - {family}";
                asset.hideFlags = HideFlags.DontSave;
                asset.isMultiAtlasTexturesEnabled = true;
                fallbacks.Add(asset);
                _created.Add(asset);
            }

            // Remove runtime-only assets so the TMP Settings asset is not left pointing at destroyed objects
            // after leaving play mode in the Editor.
            Application.quitting += Uninstall;
            Debug.Log($"[FallbackFontBootstrap] Added {_created.Count} OS fallback font(s) to TMP global fallbacks.");
        }

        private static void Uninstall()
        {
            Application.quitting -= Uninstall;
            var fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks != null)
            {
                foreach (var asset in _created) fallbacks.Remove(asset);
            }
            _created.Clear();
        }
    }
}

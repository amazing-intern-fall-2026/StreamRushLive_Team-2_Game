#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace SteamRush.Core.Typography.EditorTools
{
    // One-click setup: builds dynamic multi-atlas TMP font assets from the Noto TTFs in
    // Assets/_Project/Fonts and registers them in the global TMP fallback list.
    public static class FallbackFontSetupEditor
    {
        private const string FontFolder = "Assets/_Project/Fonts";
        private static readonly string[] NotoFonts = { "NotoSans-Bold", "NotoSansArabic-Bold" };

        [MenuItem("SteamRush/Typography/Create Noto Fallback Font Assets")]
        public static void CreateNotoFallbacks()
        {
            var created = new List<TMP_FontAsset>();

            foreach (string baseName in NotoFonts)
            {
                string ttfPath = $"{FontFolder}/{baseName}.ttf";
                string assetPath = $"{FontFolder}/{baseName} SDF.asset";

                var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (existing != null)
                {
                    created.Add(existing);
                    continue;
                }

                var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
                if (font == null)
                {
                    Debug.LogError($"[FallbackFontSetup] Missing font file: {ttfPath}");
                    continue;
                }

                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                    font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (asset == null)
                {
                    Debug.LogError($"[FallbackFontSetup] CreateFontAsset failed for {ttfPath}");
                    continue;
                }

                asset.name = $"{baseName} SDF";
                AssetDatabase.CreateAsset(asset, assetPath);
                asset.material.name = $"{baseName} SDF Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                foreach (Texture2D atlas in asset.atlasTextures)
                {
                    atlas.name = $"{baseName} SDF Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, asset);
                }

                EditorUtility.SetDirty(asset);
                created.Add(asset);
            }

            RegisterAsGlobalFallbacks(created);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FallbackFontSetup] Registered {created.Count} Noto font asset(s) in TMP global fallbacks.");
        }

        private static void RegisterAsGlobalFallbacks(List<TMP_FontAsset> assets)
        {
            var fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks == null) return;

            // Noto first (Latin / Vietnamese / Cyrillic / Greek, then Arabic), existing entries after.
            for (int i = assets.Count - 1; i >= 0; i--)
            {
                if (!fallbacks.Contains(assets[i])) fallbacks.Insert(0, assets[i]);
            }

            EditorUtility.SetDirty(TMP_Settings.instance);
        }
    }
}
#endif

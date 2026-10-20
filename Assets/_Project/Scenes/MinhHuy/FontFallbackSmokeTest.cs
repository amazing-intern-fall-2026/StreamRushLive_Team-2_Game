using TMPro;
using UnityEngine;
using SteamRush.Core.Typography;

namespace SteamRush.MinhHuy.Typography
{
    // Test: pushes sample TikTok names through the sanitizer and logs any glyph the TMP
    // fallback chain still cannot draw. Drop on any object in a MinhHuy test scene, press Play.
    public class FontFallbackSmokeTest : MonoBehaviour
    {
        [Tooltip("Font whose fallback chain is tested (nameplate font, Fredoka-Bold SDF).")]
        [SerializeField] private TMP_FontAsset fontToTest;
        [Tooltip("Optional label to preview each sample name on screen.")]
        [SerializeField] private TMP_Text previewLabel;
        [SerializeField] private float secondsPerSample = 1.5f;

        private static readonly string[] Samples =
        {
            "Nguyễn Văn Ạ Ế Ộ Ữ",
            "Trần Thị Thuỳ Dương",
            "中文昵称测试",
            "日本語のなまえ",
            "한국어 이름",
            "مرحبا بالعالم",
            "Привет мир",
            "𝓗𝓲 𝕭𝖔𝖙 𝗕𝗼𝗹𝗱",
            "Cool😎Runner🔥",
            "★☆♥ ♪ ✔ ➤",
            "a​b‎c️d",
            "Một cái tên rất rất rất dài vượt quá giới hạn hiển thị của nameplate",
        };

        private void Start()
        {
            if (fontToTest == null && TMP_Settings.defaultFontAsset != null)
            {
                fontToTest = TMP_Settings.defaultFontAsset;
            }

            foreach (string sample in Samples)
            {
                string clean = ViewerNameSanitizer.Sanitize(sample);
                uint[] missing = null;
                bool ok = fontToTest != null
                    && fontToTest.HasCharacters(clean, out missing, true, true);
                if (ok)
                {
                    Debug.Log($"[FontFallbackSmokeTest] OK    '{sample}' -> '{clean}'");
                }
                else
                {
                    string codes = missing == null ? "n/a" : string.Join(" ", System.Array.ConvertAll(missing, m => $"U+{m:X4}"));
                    Debug.LogWarning($"[FontFallbackSmokeTest] MISSING glyphs in '{clean}' (raw '{sample}'): {codes}");
                }
            }

            if (previewLabel != null) StartCoroutine(Preview());
        }

        private System.Collections.IEnumerator Preview()
        {
            var wait = new WaitForSeconds(secondsPerSample);
            foreach (string sample in Samples)
            {
                previewLabel.text = ViewerNameSanitizer.Sanitize(sample);
                yield return wait;
            }
        }
    }
}

using System.Text;

namespace SteamRush.Core.Typography
{
    // Cleans viewer-supplied strings (TikTok nicknames, chat) before they reach TMP text.
    // Removes characters that always render as tofu (control / zero-width / bidi marks,
    // lone surrogates) and optionally emoji, while keeping every real script (Vietnamese,
    // CJK, Arabic, math-alphanumeric letters...) so the fallback fonts can draw them.
    public static class ViewerNameSanitizer
    {
        public const int DefaultMaxLength = 24;

        public const string DefaultName = "Viewer";

        // Viewer nickname for nameplates / toasts. Null or empty input is returned untouched so
        // callers can still clear a label; a nickname made only of stripped characters becomes DefaultName.
        public static string SanitizeDisplayName(string raw)
        {
            return string.IsNullOrEmpty(raw) ? raw : Sanitize(raw, DefaultMaxLength, true, DefaultName);
        }

        // Whole notification sentence (may embed a nickname): no length cap, line breaks kept.
        public static string SanitizeMessage(string raw)
        {
            return string.IsNullOrEmpty(raw) ? raw : Sanitize(raw, int.MaxValue, true, string.Empty, true);
        }

        public static string Sanitize(string raw, int maxLength = DefaultMaxLength, bool stripEmoji = true, string fallback = "", bool keepNewLines = false)
        {
            if (string.IsNullOrEmpty(raw)) return fallback;

            var sb = new StringBuilder(raw.Length);
            int codePoints = 0;
            bool lastWasSpace = true;

            for (int i = 0; i < raw.Length; i++)
            {
                int cp;
                char c = raw[i];
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= raw.Length || !char.IsLowSurrogate(raw[i + 1])) continue;
                    cp = char.ConvertToUtf32(c, raw[i + 1]);
                    i++;
                }
                else if (char.IsLowSurrogate(c))
                {
                    continue;
                }
                else
                {
                    cp = c;
                }

                if (keepNewLines && (cp == '\n' || cp == '\r'))
                {
                    sb.Append((char)cp);
                    lastWasSpace = true;
                    continue;
                }

                if (IsInvisible(cp)) continue;
                if (stripEmoji && IsEmoji(cp)) continue;

                if (cp == ' ' || cp == '\t' || cp == '\u00A0' || cp == '\u3000')
                {
                    if (lastWasSpace) continue;
                    sb.Append(' ');
                    lastWasSpace = true;
                }
                else
                {
                    sb.Append(char.ConvertFromUtf32(cp));
                    lastWasSpace = false;
                }

                if (++codePoints >= maxLength) break;
            }

            string result = sb.ToString().Trim();
            return result.Length == 0 ? fallback : result;
        }

        // Control chars, zero-width / bidi formatting marks, variation selectors, BOM, soft hyphen.
        // ZWJ/ZWNJ (200C/200D) are kept: Persian and Indic scripts need them.
        private static bool IsInvisible(int cp)
        {
            if (cp < 0x20 || (cp >= 0x7F && cp <= 0x9F)) return true;
            return cp == 0x00AD
                || cp == 0x200B
                || (cp >= 0x200E && cp <= 0x200F)
                || (cp >= 0x202A && cp <= 0x202E)
                || (cp >= 0x2060 && cp <= 0x2064)
                || (cp >= 0x2066 && cp <= 0x2069)
                || (cp >= 0xFE00 && cp <= 0xFE0F)
                || cp == 0xFEFF
                || (cp >= 0xE0000 && cp <= 0xE007F);
        }

        // Code-point ranges (not surrogate halves) so astral letters such as math-script
        // alphabets are NOT mistaken for emoji.
        private static bool IsEmoji(int cp)
        {
            return (cp >= 0x1F000 && cp <= 0x1FAFF)
                || (cp >= 0x2600 && cp <= 0x27BF)
                || (cp >= 0x2300 && cp <= 0x23FF)
                || (cp >= 0x2B50 && cp <= 0x2B55)
                || cp == 0x20E3;
        }
    }
}

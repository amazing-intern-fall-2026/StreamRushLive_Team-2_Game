using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SteamRush.Features.StreamIntegration
{
    /// <summary>
    /// Sanitizes and parses control commands from livestream comments (up to 3 commands per message).
    /// Pure C# logic class without MonoBehaviour dependency for unit testing.
    /// </summary>
    public class ChatCommandSanitizer
    {
        private static readonly char[] _separatorChars = { ',', '.', '!', '-', '/', '#', '@', ';', ':', '_', '|' };
        private static readonly Dictionary<string, string> _phraseAliases = new Dictionary<string, string>
        {
            // Switch lane phrases
            { "chuyển làn 1", "1" },
            { "chuyen lan 1", "1" },
            { "chuyển làn 2", "2" },
            { "chuyen lan 2", "2" },
            { "chuyển làn 3", "3" },
            { "chuyen lan 3", "3" },
            { "làn 1", "1" },
            { "lan 1", "1" },
            { "làn 2", "2" },
            { "lan 2", "2" },
            { "làn 3", "3" },
            { "lan 3", "3" },
            { "làn một", "1" },
            { "lan mot", "1" },
            { "làn hai", "2" },
            { "lan hai", "2" },
            { "làn ba", "3" },
            { "lan ba", "3" },

            // Directional switch phrases
            { "chuyển làn trái", "left" },
            { "chuyen lan trai", "left" },
            { "chuyển làn phải", "right" },
            { "chuyen lan phai", "right" },
            { "chuyển trái", "left" },
            { "chuyen trai", "left" },
            { "chuyển phải", "right" },
            { "chuyen phai", "right" },
            { "qua trái", "left" },
            { "qua trai", "left" },
            { "sang trái", "left" },
            { "sang trai", "left" },
            { "rẽ trái", "left" },
            { "re trai", "left" },
            { "qua phải", "right" },
            { "qua phai", "right" },
            { "sang phải", "right" },
            { "sang phai", "right" },
            { "rẽ phải", "right" },
            { "re phai", "right" },

            // Generic switch lane phrases -> switch
            { "chuyển làn", "switch" },
            { "chuyen lan", "switch" },
            { "đổi làn", "switch" },
            { "doi lan", "switch" },
            { "chuyển lane", "switch" },
            { "chuyen lane", "switch" },
            { "đổi lane", "switch" },
            { "doi lane", "switch" },
            { "switch lane", "switch" },
            { "change lane", "switch" },

            // Jump phrases
            { "nhảy lên", "jump" },
            { "nhay len", "jump" },
            { "bật lên", "jump" },
            { "bat len", "jump" },
            { "jump up", "jump" },

            // Team join phrases
            { "team blue", "blue" },
            { "team fan", "blue" },
            { "đội xanh", "blue" },
            { "doi xanh", "blue" },
            { "phe xanh", "blue" },
            { "team xanh", "blue" },
            { "team red", "red" },
            { "team anti", "red" },
            { "đội đỏ", "red" },
            { "doi do", "red" },
            { "phe đỏ", "red" },
            { "phe do", "red" },
            { "team đỏ", "red" },
            { "team do", "red" }
        };

        private static readonly Dictionary<string, string> _commandAliases = new Dictionary<string, string>
        {
            // Lane 1 / Left
            { "1", "1" },
            { "l1", "1" },
            { "lane1", "1" },
            { "lan1", "1" },
            { "left", "left" },
            { "l", "left" },
            { "trai", "left" },
            { "trái", "left" },
            { "a", "left" },

            // Lane 2 / Middle
            { "2", "2" },
            { "l2", "2" },
            { "lane2", "2" },
            { "lan2", "2" },
            { "mid", "2" },
            { "middle", "2" },
            { "giua", "2" },
            { "giữa", "2" },

            // Lane 3 / Right
            { "3", "3" },
            { "l3", "3" },
            { "lane3", "3" },
            { "lan3", "3" },
            { "right", "right" },
            { "r", "right" },
            { "phai", "right" },
            { "phải", "right" },
            { "d", "right" },

            // Jump
            { "jump", "jump" },
            { "j", "jump" },
            { "nhay", "jump" },
            { "nhảy", "jump" },
            { "up", "jump" },
            { "w", "jump" },
            { "space", "jump" },

            // Turbo / Fast
            { "fast", "fast" },
            { "speed", "fast" },
            { "turbo", "fast" },
            { "boost", "fast" },
            { "nhanh", "fast" },

            // Switch Lane
            { "switch", "switch" },
            { "chuyenlan", "switch" },
            { "chuyểnlan", "switch" },
            { "doilan", "switch" },
            { "đổilàn", "switch" },
            { "chuyen", "switch" },
            { "chuyển", "switch" },

            // Team join commands
            { "blue", "blue" },
            { "fan", "blue" },
            { "teamblue", "blue" },
            { "teamfan", "blue" },
            { "xanh", "blue" },

            { "red", "red" },
            { "anti", "red" },
            { "teamred", "red" },
            { "teamanti", "red" },
            { "do", "red" },
            { "đỏ", "red" }
        };
        private const int _maxCommands = 3;

        /// <summary>
        /// Normalizes separators to whitespace, lowercases input, and extracts up to 3 valid commands.
        /// </summary>
        public List<string> SanitizeAndParse(string rawInput)
        {
            List<string> parsedCommands = new List<string>();

            if (string.IsNullOrEmpty(rawInput))
            {
                return parsedCommands;
            }

            StringBuilder normalizedBuilder = new StringBuilder(rawInput.Length);
            foreach (char currentChar in rawInput)
            {
                bool isSeparator = false;
                for (int i = 0; i < _separatorChars.Length; i++)
                {
                    if (currentChar == _separatorChars[i])
                    {
                        isSeparator = true;
                        break;
                    }
                }

                normalizedBuilder.Append(isSeparator ? ' ' : currentChar);
            }

            string normalizedInput = normalizedBuilder.ToString().ToLowerInvariant();

            // 1. Match multi-word phrases first (e.g. "switch lane 1", "switch lane", "blue team")
            foreach (var kvp in _phraseAliases)
            {
                if (normalizedInput.Contains(kvp.Key))
                {
                    if (parsedCommands.Count < _maxCommands && !parsedCommands.Contains(kvp.Value))
                    {
                        parsedCommands.Add(kvp.Value);
                    }
                    normalizedInput = normalizedInput.Replace(kvp.Key, " ");
                }
            }

            // 2. Tokenize remaining words for single-token aliases
            string[] tokens = normalizedInput.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (string token in tokens)
            {
                if (parsedCommands.Count >= _maxCommands)
                {
                    break;
                }

                if (_commandAliases.TryGetValue(token, out string canonicalCommand))
                {
                    if (!parsedCommands.Contains(canonicalCommand))
                    {
                        parsedCommands.Add(canonicalCommand);
                    }
                }
            }

            return parsedCommands;
        }
    }
}

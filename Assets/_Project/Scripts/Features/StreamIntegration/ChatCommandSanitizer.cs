using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SteamRush.Features.StreamIntegration
{
    // Sanitizes and parses control commands from livestream comments (up to 3 commands per message).
    // Pure C# logic class without MonoBehaviour dependency for unit testing.
    public class ChatCommandSanitizer
    {
        private static readonly char[] _separatorChars = { ',', '.', '!', '-', '/', '#', '@', ';', ':', '_', '|' };
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

        // Normalizes separators to whitespace, lowercases input, and extracts up to 3 valid commands.
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
            string[] tokens = normalizedInput.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (string token in tokens)
            {
                if (parsedCommands.Count >= _maxCommands)
                {
                    break;
                }

                if (_commandAliases.TryGetValue(token, out string canonicalCommand))
                {
                    parsedCommands.Add(canonicalCommand);
                }
            }

            return parsedCommands;
        }
    }
}

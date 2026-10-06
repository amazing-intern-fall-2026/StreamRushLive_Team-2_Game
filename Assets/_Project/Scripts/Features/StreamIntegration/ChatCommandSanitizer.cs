using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SteamRush.Features.StreamIntegration
{
    // Sanitizes and parses control commands from livestream comments (up to 3 commands per message).
    // Pure C# logic class without MonoBehaviour dependency for unit testing.
    public class ChatCommandSanitizer
    {
        private static readonly char[] _separatorChars = { ',', '.', '!', '-', '/' };
        private static readonly Dictionary<string, string> _commandAliases = new Dictionary<string, string>
        {
            { "1", "1" },
            { "2", "2" },
            { "3", "3" },
            { "jump", "jump" },
            { "j", "jump" }
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

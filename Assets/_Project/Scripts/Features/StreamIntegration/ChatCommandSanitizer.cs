using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SteamRush.Features.StreamIntegration
{
    // Loc va tach lenh dieu khien tu comment livestream (GDD v1.3 muc 6: chuoi toi da 3 lenh).
    // Class logic thuan, khong MonoBehaviour - de goi/test doc lap, khong phu thuoc vong doi Unity.
    public class ChatCommandSanitizer
    {
        private static readonly char[] _separatorChars = { ',', '.', '!', '-', '/' };
        private static readonly Dictionary<string, string> _commandAliases = new Dictionary<string, string>
        {
            { "left", "left" },
            { "l", "left" },
            { "a", "left" },
            { "trai", "left" },
            { "right", "right" },
            { "r", "right" },
            { "d", "right" },
            { "phai", "right" },
            { "fast", "fast" },
            { "f", "fast" },
            { "w", "fast" },
            { "1", "1" },
            { "lan1", "1" },
            { "lane1", "1" },
            { "2", "2" },
            { "lan2", "2" },
            { "lane2", "2" },
            { "3", "3" },
            { "lan3", "3" },
            { "lane3", "3" }
        };
        private const int _maxCommands = 3;

        // Thay cac ky tu phan cach thanh khoang trang, lowercase toan bo, roi boc toi da 3 lenh
        // hop le (left/right/fast va cac alias a/d/l/r/w) theo dung thu tu xuat hien trong chuoi goc.
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

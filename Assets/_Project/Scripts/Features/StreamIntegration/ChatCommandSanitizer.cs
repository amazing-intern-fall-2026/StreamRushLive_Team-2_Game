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
        private static readonly HashSet<string> _validCommands = new HashSet<string> { "left", "right", "fast", "slow" };
        private const int _maxCommands = 3;

        // Thay cac ky tu phan cach thanh khoang trang, lowercase toan bo, roi boc toi da 3 lenh
        // hop le (left/right/fast/slow) theo dung thu tu xuat hien trong chuoi goc. Lenh la (khong
        // nam trong danh sach hop le) bi bo qua hoan toan, khong tinh vao gioi han 3 lenh.
        public List<string> SanitizeAndParse(string rawInput)
        {
            List<string> parsedCommands = new List<string>();

            if (string.IsNullOrEmpty(rawInput))
            {
                Debug.Log("[ChatCommandSanitizer] Lenh da loc: (rong)");
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

                if (_validCommands.Contains(token))
                {
                    parsedCommands.Add(token);
                }
            }

            Debug.Log($"[ChatCommandSanitizer] Lenh da loc: {string.Join(", ", parsedCommands)}");
            return parsedCommands;
        }
    }
}

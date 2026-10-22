using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SteamRush.Features.StreamIntegration
{
    /// <summary>
    /// [EN] Sanitizes and parses control commands from livestream comments (up to 3 commands per message).
    /// Pure C# logic class without MonoBehaviour dependency for unit testing.
    /// [VI] Chuẩn hóa và phân tích lệnh điều khiển từ bình luận livestream (tối đa 3 lệnh mỗi tin nhắn).
    /// Lớp logic thuần C# không phụ thuộc MonoBehaviour để phục vụ unit test.
    /// </summary>
    public class ChatCommandSanitizer
    {
        private static readonly char[] _separatorChars = { ',', '.', '!', '-', '/', '#', '@', ';', ':', '_', '|' };
        private static readonly Dictionary<string, string> _phraseAliases = new Dictionary<string, string>
        {
            // [EN] Switch lane phrases / [VI] Cụm từ chuyển làn (chuyển làn / đổi làn)
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

            // [EN] Directional switch phrases / [VI] Cụm từ chuyển hướng trái phải
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

            // [EN] Generic switch lane phrases / [VI] Cụm từ chuyển làn chung -> switch
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

            // [EN] Jump phrases / [VI] Cụm từ nhảy lên
            { "nhảy lên", "jump" },
            { "nhay len", "jump" },
            { "bật lên", "jump" },
            { "bat len", "jump" },
            { "jump up", "jump" },

            // [EN] Team join phrases / [VI] Cụm từ chọn phe (Xanh / Đỏ)
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
            // [EN] Lane 1 / Left / [VI] Làn 1 / Bên trái
            { "1", "1" },
            { "l1", "1" },
            { "lane1", "1" },
            { "lan1", "1" },
            { "left", "left" },
            { "l", "left" },
            { "trai", "left" },
            { "trái", "left" },
            { "a", "left" },

            // [EN] Lane 2 / Middle / [VI] Làn 2 / Ở giữa
            { "2", "2" },
            { "l2", "2" },
            { "lane2", "2" },
            { "lan2", "2" },
            { "mid", "2" },
            { "middle", "2" },
            { "giua", "2" },
            { "giữa", "2" },

            // [EN] Lane 3 / Right / [VI] Làn 3 / Bên phải
            { "3", "3" },
            { "l3", "3" },
            { "lane3", "3" },
            { "lan3", "3" },
            { "right", "right" },
            { "r", "right" },
            { "phai", "right" },
            { "phải", "right" },
            { "d", "right" },

            // [EN] Jump / [VI] Nhảy
            { "jump", "jump" },
            { "j", "jump" },
            { "nhay", "jump" },
            { "nhảy", "jump" },
            { "up", "jump" },
            { "w", "jump" },
            { "space", "jump" },

            // [EN] Turbo / Fast / [VI] Chạy nhanh / Tăng tốc
            { "fast", "fast" },
            { "speed", "fast" },
            { "turbo", "fast" },
            { "boost", "fast" },
            { "nhanh", "fast" },

            // [EN] Switch Lane / [VI] Chuyển đổi làn
            { "switch", "switch" },
            { "chuyenlan", "switch" },
            { "chuyểnlan", "switch" },
            { "doilan", "switch" },
            { "đổilàn", "switch" },
            { "chuyen", "switch" },
            { "chuyển", "switch" },

            // [EN] Team join commands / [VI] Lệnh chọn phe
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
        /// [EN] Normalizes separators to whitespace, lowercases input, and extracts up to 3 valid commands.
        /// [VI] Chuẩn hóa các dấu phân cách thành khoảng trắng, chuyển chữ thường và trích xuất tối đa 3 lệnh hợp lệ.
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

            // 1. [EN] Match multi-word phrases first (e.g. "chuyển làn 1", "chuyển làn", "đội xanh")
            //    [VI] Khớp các cụm từ nhiều chữ trước (ví dụ "chuyển làn 1", "chuyển làn", "đội xanh")
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

            // 2. [EN] Tokenize remaining words for single-token aliases
            //    [VI] Tách các từ còn lại để khớp các lệnh từ đơn
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

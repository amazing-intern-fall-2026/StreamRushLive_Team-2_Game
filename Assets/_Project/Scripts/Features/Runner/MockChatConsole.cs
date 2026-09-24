using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using StreamRushLive.Features.Spawning;
using SteamRush.Features.StreamIntegration;

namespace SteamRush.Features.Runner
{
    public class MockChatConsole : MonoBehaviour
    {
        [Header("Chat Input")]
        [SerializeField] private TMP_InputField chatInputField;

        [Header("Runner Components")]
        [SerializeField] private RunnerItemEffects itemEffects;
        [SerializeField] private ChatLaneRunnerController chatLaneRunner;

        [Header("Stream Integration Managers")]
        [SerializeField] private FactionTugOfWarManager factionManager;
        [SerializeField] private ChatRunnerQueueManager queueManager;
        [SerializeField] private SteamRush.Features.UI.HUDManager hudManager;
        [SerializeField] private SingleObstacleSpawner obstacleSpawner;

        [Header("Mock Settings")]
        [SerializeField] private float shieldDuration = 20f;

        [Header("Gift Icons (khu Gift/Donate riêng)")]
        [SerializeField] private Sprite shieldGiftIcon;
        [SerializeField] private Sprite energyGiftIcon;

        private readonly ChatCommandSanitizer _sanitizer = new ChatCommandSanitizer();

        [Header("Debug UI Layout & Toggle")]
        [SerializeField] private GameObject debugPanelContainer;
        [SerializeField] private Button toggleDebugButton;
        [SerializeField] private TMP_Text toggleButtonText;
        [SerializeField] private bool isDebugUIVisible = true;

        private GameObject _quickHelpBarObj;

        private void Awake()
        {
            if (itemEffects == null)
            {
                itemEffects = GetComponent<RunnerItemEffects>();
                if (itemEffects == null)
                    itemEffects = GetComponentInParent<RunnerItemEffects>();
            }

            if (chatLaneRunner == null)
            {
                chatLaneRunner = GetComponent<ChatLaneRunnerController>();
                if (chatLaneRunner == null)
                    chatLaneRunner = GetComponentInParent<ChatLaneRunnerController>();
            }

            if (factionManager == null)
            {
                factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            }

            if (queueManager == null)
            {
                queueManager = FindFirstObjectByType<ChatRunnerQueueManager>();
            }

            if (hudManager == null)
            {
                hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            }

            if (obstacleSpawner == null)
            {
                obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
            }

            if (chatInputField != null)
            {
                chatInputField.onSubmit.AddListener(OnChatSubmitted);
            }
            else
            {
                Debug.LogWarning("[MockChatConsole] Chưa gán Chat Input Field!");
            }

            SetupVerticalDebugUI();
        }

        private void SetupVerticalDebugUI()
        {
            var statusUI = FindFirstObjectByType<SteamRush.Features.UI.ChatRunnerStatusUI>();
            if (statusUI != null)
            {
                _quickHelpBarObj = statusUI.gameObject;
            }

            // 1. Cân chỉnh RectTransform phù hợp với màn hình dọc 1080x1920 (Portrait)
            if (chatInputField != null)
            {
                RectTransform inputRect = chatInputField.GetComponent<RectTransform>();
                if (inputRect != null)
                {
                    inputRect.anchorMin = new Vector2(0.5f, 0f);
                    inputRect.anchorMax = new Vector2(0.5f, 0f);
                    inputRect.pivot = new Vector2(0.5f, 0f);
                    inputRect.sizeDelta = new Vector2(780f, 50f);
                    inputRect.anchoredPosition = new Vector2(0f, 100f);
                }
            }

            if (_quickHelpBarObj != null)
            {
                RectTransform barRect = _quickHelpBarObj.GetComponent<RectTransform>();
                if (barRect != null)
                {
                    barRect.anchorMin = new Vector2(0.5f, 0f);
                    barRect.anchorMax = new Vector2(0.5f, 0f);
                    barRect.pivot = new Vector2(0.5f, 0f);
                    barRect.sizeDelta = new Vector2(780f, 80f);
                    barRect.anchoredPosition = new Vector2(0f, 15f);
                }
            }

            // 2. Tạo hoặc liên kết Nút Bật/Tắt UI Debug nếu chưa có
            if (toggleDebugButton == null)
            {
                Canvas canvas = chatInputField != null ? chatInputField.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
                if (canvas != null)
                {
                    Transform existingBtn = canvas.transform.Find("Btn_ToggleDebug");
                    if (existingBtn != null)
                    {
                        toggleDebugButton = existingBtn.GetComponent<Button>();
                        toggleButtonText = existingBtn.GetComponentInChildren<TMP_Text>();
                    }
                    else
                    {
                        CreateToggleDebugButton(canvas.transform);
                    }
                }
            }

            if (toggleDebugButton != null)
            {
                toggleDebugButton.onClick.RemoveListener(ToggleDebugUI);
                toggleDebugButton.onClick.AddListener(ToggleDebugUI);
            }

            UpdateDebugUIVisibility();
        }

        private void CreateToggleDebugButton(Transform canvasTransform)
        {
            GameObject btnObj = new GameObject("Btn_ToggleDebug", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(canvasTransform, false);

            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(130f, 36f);
            rect.anchoredPosition = new Vector2(-20f, 155f);

            Image img = btnObj.GetComponent<Image>();
            img.color = new Color(0.08f, 0.12f, 0.2f, 0.88f);

            toggleDebugButton = btnObj.GetComponent<Button>();
            ColorBlock cb = toggleDebugButton.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.2f, 0.8f, 1f, 1f);
            cb.pressedColor = new Color(0f, 0.6f, 0.9f, 1f);
            toggleDebugButton.colors = cb;

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            toggleButtonText = textObj.GetComponent<TextMeshProUGUI>();
            toggleButtonText.text = "💬 Debug [F12]";
            toggleButtonText.fontSize = 14;
            toggleButtonText.alignment = TextAlignmentOptions.Center;
            toggleButtonText.color = new Color(0.5f, 0.85f, 1f, 1f);
        }

        public void ToggleDebugUI()
        {
            isDebugUIVisible = !isDebugUIVisible;
            UpdateDebugUIVisibility();
        }

        private void UpdateDebugUIVisibility()
        {
            if (chatInputField != null)
            {
                chatInputField.gameObject.SetActive(isDebugUIVisible);
            }

            if (_quickHelpBarObj != null)
            {
                _quickHelpBarObj.SetActive(isDebugUIVisible);
            }

            if (debugPanelContainer != null)
            {
                debugPanelContainer.SetActive(isDebugUIVisible);
            }

            if (toggleDebugButton != null)
            {
                RectTransform btnRect = toggleDebugButton.GetComponent<RectTransform>();
                if (btnRect != null)
                {
                    // Khi thanh debug mở: Đẩy nút lên trên (Y: 155). Khi đóng: Nút hạ xuống góc dưới (Y: 20) gọn gàng
                    btnRect.anchoredPosition = new Vector2(-20f, isDebugUIVisible ? 155f : 20f);
                }
            }

            if (toggleButtonText != null)
            {
                toggleButtonText.text = isDebugUIVisible ? "❌ Ẩn Debug" : "💬 Debug [F12]";
            }
        }

        private void OnDestroy()
        {
            if (chatInputField != null)
            {
                chatInputField.onSubmit.RemoveListener(OnChatSubmitted);
            }

            if (toggleDebugButton != null)
            {
                toggleDebugButton.onClick.RemoveListener(ToggleDebugUI);
            }
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            // Phím tắt F12 hoặc ` (Backquote/tilde) để bật/tắt toàn bộ khung Debug
            if (Keyboard.current.f12Key.wasPressedThisFrame || Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                ToggleDebugUI();
            }

            if (Keyboard.current.f1Key.wasPressedThisFrame)
                MockDonateShield();

            if (Keyboard.current.f2Key.wasPressedThisFrame)
                MockDonateHeal();

            if (Keyboard.current.f3Key.wasPressedThisFrame)
                MockFanLikes();

            if (Keyboard.current.f4Key.wasPressedThisFrame)
                MockAntiLikes();

            if (Keyboard.current.f5Key.wasPressedThisFrame)
                MockNewFollower();

            if (Keyboard.current.f7Key.wasPressedThisFrame)
                MockActivateAntiCarUnlimited();

            if (Keyboard.current.f9Key.wasPressedThisFrame)
                MockBuyNormalTicket();

            if (Keyboard.current.f10Key.wasPressedThisFrame)
                MockBuyVipTicket();

            if (Keyboard.current.digit0Key.wasPressedThisFrame)
                MockToggleUnlimitedModeDebug();

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (chatInputField != null && chatInputField.isFocused)
                {
                    chatInputField.DeactivateInputField();
                    if (EventSystem.current != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                    }
                }
            }

            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                if (chatInputField != null)
                {
                    if (chatInputField.isFocused)
                    {
                        // Người dùng đang nhập trong ô chat và bấm Enter -> Thực thi ngay lập tức
                        string text = chatInputField.text;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            OnChatSubmitted(text);
                        }
                        else
                        {
                            ClearInputField();
                        }
                    }
                    else
                    {
                        // Nếu đang ẩn mà bấm Enter -> Tự động bật khung debug lên và focus
                        if (!isDebugUIVisible)
                        {
                            ToggleDebugUI();
                        }
                        chatInputField.ActivateInputField();
                    }
                }
            }
        }

        private int _lastSubmittedFrame = -1;
        private int _mockFollowerIndex = 0;
        private string _lastActiveFollowerName = "";
        private static readonly string[] MockFollowerList = new string[]
        {
            "Viewer_Bao",
            "Viewer_Chi",
            "Top1_Dung",
            "Mod_Giang",
            "Gamer_Huy"
        };

        private string GetOrRotateFollowerName()
        {
            string name = MockFollowerList[_mockFollowerIndex % MockFollowerList.Length];
            _mockFollowerIndex++;
            return name;
        }

        // Tách chuỗi chat thành (tên follower, nội dung lệnh)
        // Hỗ trợ: "Viewer_Chi #fan", "Bao: #anti", "Top1_Dung: 1", hoặc "#fan" (tự động gán tên follower rõ ràng)
        private (string followerName, string command) ParseFollowerAndInput(string rawInput)
        {
            string trimmed = rawInput.Trim();

            // TH 1: Có dấu ':' như "Viewer_Chi: #fan" hoặc "Bao: anti 2"
            int colonIdx = trimmed.IndexOf(':');
            if (colonIdx > 0 && colonIdx < trimmed.Length - 1)
            {
                string name = trimmed.Substring(0, colonIdx).Trim();
                string cmd = trimmed.Substring(colonIdx + 1).Trim();
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(cmd))
                {
                    _lastActiveFollowerName = name;
                    return (name, cmd);
                }
            }

            // TH 2: Dấu cách phân cách tên và lệnh, ví dụ "Viewer_Chi #fan" hoặc "Top1_Dung #anti"
            int firstSpace = trimmed.IndexOf(' ');
            if (firstSpace > 0)
            {
                string firstWord = trimmed.Substring(0, firstSpace).Trim();
                string rest = trimmed.Substring(firstSpace + 1).Trim();
                string firstLower = firstWord.ToLowerInvariant();

                bool isFirstWordCommand = firstLower == "#fan" || firstLower == "#anti" || firstLower == "anti" ||
                                          firstLower == "#blue" || firstLower == "#red" ||
                                          firstLower == "left" || firstLower == "right" || firstLower == "fast" ||
                                          firstLower == "1" || firstLower == "2" || firstLower == "3";

                if (!isFirstWordCommand && !string.IsNullOrEmpty(rest))
                {
                    _lastActiveFollowerName = firstWord;
                    return (firstWord, rest);
                }
            }

            // TH 3: Người dùng chỉ gõ lệnh trực tiếp (ví dụ "#fan", "#anti", "1", "fast")
            // Nếu là lệnh đổi phe (#fan/#anti) -> xoay vòng sang một follower mới để giả lập nhiều khán giả khác nhau tham gia
            string cmdLower = trimmed.ToLowerInvariant();
            if (cmdLower == "#fan" || cmdLower == "#blue" || cmdLower == "#anti" || cmdLower == "#red")
            {
                string newFollower = GetOrRotateFollowerName();
                _lastActiveFollowerName = newFollower;
                return (newFollower, trimmed);
            }

            // Còn lại nếu đã có follower gần nhất thì dùng, chưa có thì lấy từ danh sách
            if (string.IsNullOrEmpty(_lastActiveFollowerName))
            {
                _lastActiveFollowerName = GetOrRotateFollowerName();
            }

            return (_lastActiveFollowerName, trimmed);
        }

        private void OnChatSubmitted(string rawInput)
        {
            if (Time.frameCount == _lastSubmittedFrame)
            {
                return;
            }
            _lastSubmittedFrame = Time.frameCount;

            if (string.IsNullOrWhiteSpace(rawInput))
            {
                ClearInputField();
                return;
            }

            if (factionManager == null) factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();

            var (followerName, command) = ParseFollowerAndInput(rawInput);
            string trimmedCmd = command.Trim().ToLowerInvariant();

            // 1. Kiểm tra lệnh đổi phe: #fan / #anti / #blue / #red (hiển thị rõ tên Follower)
            if (trimmedCmd == "#fan" || trimmedCmd == "#blue")
            {
                factionManager?.SetFaction(followerName, FactionType.Fan);
                factionManager?.SetFaction("runner_player", FactionType.Fan);
                hudManager?.ShowStatusPopup($"[{followerName}] đã gia nhập phe FAN! (Ủng hộ Runner)", true);
                ClearInputField();
                return;
            }
            else if (trimmedCmd == "#anti" || trimmedCmd == "#red")
            {
                factionManager?.SetFaction(followerName, FactionType.Anti);
                factionManager?.SetFaction("runner_player", FactionType.Anti);
                hudManager?.ShowStatusPopup($"[{followerName}] đã gia nhập phe ANTI! (Cản đường Runner)", false);
                ClearInputField();
                return;
            }

            // 2. Kiểm tra lệnh spawn xe cản đường của phe Anti (100 năng lượng / xe):
            // TH A: Cú pháp trực tiếp: "#anti 1", "anti 1", "#anti 2", "anti 2", "#anti 3", "anti 3", "anti1", "anti2", "anti3"
            int antiLane = -1;
            if (trimmedCmd.StartsWith("#anti") || trimmedCmd.StartsWith("anti"))
            {
                string rest = trimmedCmd.Replace("#anti", "").Replace("anti", "").Trim();
                if (rest == "1" || rest == "lane 1" || rest == "trai" || rest == "left") antiLane = 1;
                else if (rest == "2" || rest == "lane 2" || rest == "giua" || rest == "mid" || rest == "center") antiLane = 2;
                else if (rest == "3" || rest == "lane 3" || rest == "phai" || rest == "right") antiLane = 3;
            }
            // TH B: Follower đã ở phe Anti và gõ "1", "2", "3"
            else if (factionManager != null && (factionManager.GetFaction(followerName) == FactionType.Anti || factionManager.GetFaction("runner_player") == FactionType.Anti))
            {
                if (trimmedCmd == "1" || trimmedCmd == "left" || trimmedCmd == "trai") antiLane = 1;
                else if (trimmedCmd == "2" || trimmedCmd == "center" || trimmedCmd == "mid" || trimmedCmd == "giua") antiLane = 2;
                else if (trimmedCmd == "3" || trimmedCmd == "right" || trimmedCmd == "phai") antiLane = 3;
            }

            if (antiLane != -1)
            {
                var obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
                if (obstacleSpawner != null)
                {
                    if (!obstacleSpawner.CanSpawnObstacle())
                    {
                        hudManager?.ShowStatusPopup($"[{followerName}] (Phe Anti): Đang có tối đa 2 xe cản đường cùng lúc! Hãy đợi xe trước vượt qua.", false);
                        ClearInputField();
                        return;
                    }

                    if (!obstacleSpawner.CanSpawnObstacleOnLane(antiLane))
                    {
                        string occupiedLaneName = antiLane == 1 ? "1 (Trái)" : (antiLane == 2 ? "2 (Giữa)" : "3 (Phải)");
                        hudManager?.ShowStatusPopup($"[{followerName}] (Phe Anti): Làn {occupiedLaneName} đang có vật cản cản đường!", false);
                        ClearInputField();
                        return;
                    }
                }

                if (factionManager != null)
                {
                    bool success = factionManager.TrySpawnAntiObstacleCar(followerName, antiLane);
                    if (success)
                    {
                        string laneName = antiLane == 1 ? "Trái (1)" : (antiLane == 2 ? "Giữa (2)" : "Phải (3)");
                        hudManager?.ShowStatusPopup($"[{followerName}] (Phe Anti) thả xe Làn {laneName}! (-{factionManager.AntiCarLaneCost} Anti)", false);
                    }
                    else
                    {
                        hudManager?.ShowStatusPopup($"[{followerName}] (Phe Anti) không đủ 100 năng lượng! (Hiện có {factionManager.AntiLikes})", false);
                    }
                }
                ClearInputField();
                return;
            }

            // 2.5 Kiểm tra lệnh spawn item hỗ trợ của phe Fan (50 năng lượng / item):
            // Cú pháp: "#fan 1", "fan 1", "#fan 2", "fan 2", "#fan 3", "fan 3", "fan1", "fan2", "fan3",
            //          "#shield 1", "shield 1", "#buff 1", "buff 1", "#item 1", "item 1"...
            int fanLane = -1;
            bool isFanShield = false;

            if (trimmedCmd.StartsWith("#fan") || trimmedCmd.StartsWith("fan"))
            {
                string rest = trimmedCmd.Replace("#fan", "").Replace("fan", "").Trim();
                if (rest == "1" || rest == "lane 1" || rest == "trai" || rest == "left") fanLane = 1;
                else if (rest == "2" || rest == "lane 2" || rest == "giua" || rest == "mid" || rest == "center") fanLane = 2;
                else if (rest == "3" || rest == "lane 3" || rest == "phai" || rest == "right") fanLane = 3;
            }
            else if (trimmedCmd.StartsWith("#shield") || trimmedCmd.StartsWith("shield") || trimmedCmd.StartsWith("khien"))
            {
                isFanShield = true;
                string rest = trimmedCmd.Replace("#shield", "").Replace("shield", "").Replace("khien", "").Trim();
                if (rest == "1" || rest == "lane 1" || rest == "trai" || rest == "left") fanLane = 1;
                else if (rest == "2" || rest == "lane 2" || rest == "giua" || rest == "mid" || rest == "center") fanLane = 2;
                else if (rest == "3" || rest == "lane 3" || rest == "phai" || rest == "right") fanLane = 3;
                else fanLane = 2; // mặc định làn giữa
            }
            else if (trimmedCmd.StartsWith("#buff") || trimmedCmd.StartsWith("buff") || trimmedCmd.StartsWith("#item") || trimmedCmd.StartsWith("item"))
            {
                string rest = trimmedCmd.Replace("#buff", "").Replace("buff", "").Replace("#item", "").Replace("item", "").Trim();
                if (rest == "1" || rest == "lane 1" || rest == "trai" || rest == "left") fanLane = 1;
                else if (rest == "2" || rest == "lane 2" || rest == "giua" || rest == "mid" || rest == "center") fanLane = 2;
                else if (rest == "3" || rest == "lane 3" || rest == "phai" || rest == "right") fanLane = 3;
                else fanLane = 2;
            }

            if (fanLane != -1)
            {
                if (factionManager != null)
                {
                    bool success = factionManager.TrySpawnFanItem(followerName, fanLane, isFanShield);
                    if (success)
                    {
                        string laneName = fanLane == 1 ? "Trái (1)" : (fanLane == 2 ? "Giữa (2)" : "Phải (3)");
                        string itemName = isFanShield ? "Khiên Bảo Vệ" : "Bình Năng Lượng";
                        hudManager?.ShowStatusPopup($"[{followerName}] (Phe Fan) thả {itemName} Làn {laneName}! (-{factionManager.FanItemLaneCost} Fan)", true);
                    }
                    else
                    {
                        hudManager?.ShowStatusPopup($"[{followerName}] (Phe Fan) không đủ {factionManager.FanItemLaneCost} năng lượng! (Hiện có {factionManager.FanLikes})", true);
                    }
                }
                ClearInputField();
                return;
            }

            // 3. Lọc và thực thi chuỗi lệnh di chuyển (left, right, fast, 1, 2, 3...) cho Runner (phe Fan)
            List<string> commands = _sanitizer.SanitizeAndParse(command);
            if (commands != null && commands.Count > 0)
            {
                if (chatLaneRunner == null)
                    chatLaneRunner = GetComponent<ChatLaneRunnerController>() ?? GetComponentInParent<ChatLaneRunnerController>() ?? FindFirstObjectByType<ChatLaneRunnerController>();

                if (chatLaneRunner != null)
                {
                    chatLaneRunner.ExecuteCommands(commands);
                }
                else
                {
                    Debug.LogWarning("[MockChatConsole] Không tìm thấy ChatLaneRunnerController để thực thi lệnh!");
                }
            }

            ClearInputField();
        }

        private void MockDonateShield()
        {
            if (obstacleSpawner == null)
            {
                obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
            }

            if (obstacleSpawner == null)
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy SingleObstacleSpawner.");
                return;
            }

            int randomLane = Random.Range(1, 4);
            bool success = obstacleSpawner.TriggerSpawnFanItem(randomLane, isShield: true);

            if (success)
            {
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                hudManager?.ShowStatusPopup($"[Khán giả] tặng Khiên Bảo Vệ trên Làn {randomLane}!", true);
                hudManager?.ShowGiftToast("Khán giả", $"Khiên Bảo Vệ ({shieldDuration}s)", shieldGiftIcon, new Color(0.55f, 0.85f, 1f, 1f));
                Debug.Log($"[MockChatConsole] F1 -> Spawn Shield item trên Làn {randomLane}.");
            }
            else
            {
                Debug.LogWarning("[MockChatConsole] F1 -> Spawn Shield thất bại (thiếu prefab hoặc Player reference).");
            }
        }

        private void MockDonateHeal()
        {
            if (obstacleSpawner == null)
            {
                obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
            }

            if (obstacleSpawner == null)
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy SingleObstacleSpawner.");
                return;
            }

            int randomLane = Random.Range(1, 4);
            bool success = obstacleSpawner.TriggerSpawnFanItem(randomLane, isShield: false);

            if (success)
            {
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                hudManager?.ShowStatusPopup($"[Khán giả] tặng Bình Năng Lượng trên Làn {randomLane}!", true);
                hudManager?.ShowGiftToast("Khán giả", "Bình Năng Lượng (+20%)", energyGiftIcon, new Color(0.55f, 1f, 0.5f, 1f));
                Debug.Log($"[MockChatConsole] F2 -> Spawn Energy Buff item trên Làn {randomLane}.");
            }
            else
            {
                Debug.LogWarning("[MockChatConsole] F2 -> Spawn Energy Buff thất bại (thiếu prefab hoặc Player reference).");
            }
        }

        private void MockFanLikes()
        {
            if (factionManager == null)
                factionManager = FindFirstObjectByType<FactionTugOfWarManager>();

            if (factionManager != null)
            {
                factionManager.OnChatCommand("viewer_fan", "#fan");
                for (int i = 0; i < 50; i++)
                {
                    factionManager.OnLikeReceived("viewer_fan");
                }
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                hudManager?.ShowStatusPopup($"[Viewer_Fan] nạp +50 tim cho phe FAN! (Tổng: {factionManager.FanLikes})", true);
                Debug.Log($"[MockChatConsole] F3 -> +50 Fan Likes! (Total: {factionManager.FanLikes})");
            }
            else
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy FactionTugOfWarManager.");
            }
        }

        private void MockAntiLikes()
        {
            if (factionManager == null)
                factionManager = FindFirstObjectByType<FactionTugOfWarManager>();

            if (factionManager != null)
            {
                factionManager.OnChatCommand("viewer_anti", "#anti");
                for (int i = 0; i < 100; i++)
                {
                    factionManager.OnLikeReceived("viewer_anti");
                }
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                hudManager?.ShowStatusPopup($"[Viewer_Anti] nạp +100 tim cho phe ANTI! (Tổng: {factionManager.AntiLikes})", false);
                Debug.Log($"[MockChatConsole] F4 -> +100 Anti Likes! (Total: {factionManager.AntiLikes})");
            }
            else
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy FactionTugOfWarManager.");
            }
        }

        private void MockNewFollower()
        {
            if (queueManager == null)
                queueManager = FindFirstObjectByType<ChatRunnerQueueManager>();

            if (queueManager != null)
            {
                string newId = "Follower_" + Random.Range(100, 999);
                queueManager.TryEnqueueFollower(newId);
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                hudManager?.ShowStatusPopup($"[{newId}] vừa Follow kênh và vào hàng đợi!", true);
                Debug.Log($"[MockChatConsole] F5 -> Enqueued new follower: {newId}");
            }
            else
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy ChatRunnerQueueManager.");
            }
        }

        private void MockActivateAntiCarUnlimited()
        {
            if (obstacleSpawner == null)
            {
                obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
            }

            if (obstacleSpawner == null)
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy SingleObstacleSpawner.");
                return;
            }

            if (!obstacleSpawner.IsUnlimitedModeActive)
            {
                obstacleSpawner.ActivateUnlimitedMode();
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                hudManager?.ShowStatusPopup("[Phe Anti] QUÀ XE KHÔNG GIỚI HẠN kích hoạt 60s!", false);
                Debug.Log("[MockChatConsole] F7 -> Kích hoạt Thả Xe Không Giới Hạn (60s). Không spawn xe.");
            }
            else
            {
                Debug.Log("[MockChatConsole] F7 -> Thả Xe Không Giới Hạn đang hoạt động.");
            }
        }

        // ===== [Dhuy] BEGIN - F9/F10 Vé hàng chờ (Ticket System) =====
        private void MockBuyNormalTicket()
        {
            if (queueManager == null)
            {
                queueManager = FindFirstObjectByType<ChatRunnerQueueManager>();
            }

            if (queueManager == null)
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy ChatRunnerQueueManager.");
                return;
            }

            string newId = "Follower_" + Random.Range(100, 999);
            queueManager.TryEnqueueFollower(newId);

            if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            hudManager?.ShowStatusPopup($"[{newId}] mua vé THƯỜNG, xếp cuối hàng chờ!", true);
            Debug.Log($"[MockChatConsole] F9 -> Vé thường: {newId} (cuối hàng chờ).");
        }

        private void MockBuyVipTicket()
        {
            if (queueManager == null)
            {
                queueManager = FindFirstObjectByType<ChatRunnerQueueManager>();
            }

            if (queueManager == null)
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy ChatRunnerQueueManager.");
                return;
            }

            string newId = "VIP_" + Random.Range(100, 999);
            queueManager.TryEnqueuePriorityFollower(newId);

            if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            hudManager?.ShowStatusPopup($"[{newId}] mua vé VIP, chen thẳng lên ĐẦU hàng chờ!", true);
            Debug.Log($"[MockChatConsole] F10 -> Vé VIP: {newId} (đầu hàng chờ).");
        }
        // ===== [Dhuy] END =====

        // ===== [Dhuy] BEGIN - Debug phím "0": toggle Unlimited Mode tự do để QA test =====
        private void MockToggleUnlimitedModeDebug()
        {
            if (obstacleSpawner == null)
            {
                obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
            }

            if (obstacleSpawner == null)
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy SingleObstacleSpawner.");
                return;
            }

            bool newState = !obstacleSpawner.IsUnlimitedModeActive;
            obstacleSpawner.SetUnlimitedModeDebug(newState);

            if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            hudManager?.ShowStatusPopup(newState ? "[DEBUG] Unlimited Mode: BẬT (phím 0)" : "[DEBUG] Unlimited Mode: TẮT (phím 0)", false);
            Debug.Log($"[MockChatConsole] Phím 0 -> [DEBUG] Unlimited Mode = {newState}");
        }
        // ===== [Dhuy] END =====

        private void ClearInputField()
        {
            if (chatInputField == null)
            {
                return;
            }

            chatInputField.text = "";
            chatInputField.DeactivateInputField();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}
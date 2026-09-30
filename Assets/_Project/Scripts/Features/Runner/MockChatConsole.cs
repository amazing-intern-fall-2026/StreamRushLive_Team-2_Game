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
        [SerializeField] private GiftDanceController giftDance;

        [Header("Stream Integration Managers")]
        [SerializeField] private FactionTugOfWarManager factionManager;
        [SerializeField] private ChatRunnerQueueManager queueManager;
        [SerializeField] private SteamRush.Features.UI.HUDManager hudManager;
        [SerializeField] private SingleObstacleSpawner obstacleSpawner;

        [Header("Mock Settings")]
        [SerializeField] private float shieldDuration = 15f;

        private readonly ChatCommandSanitizer _sanitizer = new ChatCommandSanitizer();

        [Header("Debug UI Layout & Toggle")]
        [SerializeField] private GameObject debugPanelContainer;
        [SerializeField] private Button toggleDebugButton;
        [SerializeField] private TMP_Text toggleButtonText;
        [SerializeField] private bool isDebugUIVisible = true;

        public static MockChatConsole Instance { get; private set; }

        private GameObject _quickHelpBarObj;
        private GameObject _energyDebugPanelObj;

        private void Awake()
        {
            Instance = this;
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

            if (giftDance == null)
            {
                giftDance = GetComponent<GiftDanceController>();
                if (giftDance == null)
                    giftDance = GetComponentInParent<GiftDanceController>();
                if (giftDance == null)
                    giftDance = FindFirstObjectByType<GiftDanceController>();
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

            if (_energyDebugPanelObj == null)
            {
                Canvas canvas = chatInputField != null ? chatInputField.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
                if (canvas != null)
                {
                    Transform existingPanel = canvas.transform.Find("Panel_EnergyDebug");
                    if (existingPanel != null)
                    {
                        _energyDebugPanelObj = existingPanel.gameObject;
                    }
                    else
                    {
                        CreateEnergyDebugButtons(canvas.transform);
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

        private void CreateEnergyDebugButtons(Transform canvasTransform)
        {
            _energyDebugPanelObj = new GameObject("Panel_EnergyDebug", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            _energyDebugPanelObj.transform.SetParent(canvasTransform, false);

            RectTransform rect = _energyDebugPanelObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(780f, 36f);
            rect.anchoredPosition = new Vector2(0f, 155f);

            HorizontalLayoutGroup hlg = _energyDebugPanelObj.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            CreateDebugButton(_energyDebugPanelObj.transform, "Btn_FanPlus", "Blue Team +1% [F6 / ]]", new Color(0.12f, 0.45f, 0.85f, 0.95f), () => DebugIncreaseFanEnergy(10));
            CreateDebugButton(_energyDebugPanelObj.transform, "Btn_FanMinus", "Blue Team -1% [Shift+F6 / []", new Color(0.08f, 0.25f, 0.55f, 0.95f), () => DebugDecreaseFanEnergy(10));
            CreateDebugButton(_energyDebugPanelObj.transform, "Btn_AntiPlus", "Red Team +1% [F11 / +]", new Color(0.85f, 0.28f, 0.15f, 0.95f), () => DebugIncreaseAntiEnergy(10));
            CreateDebugButton(_energyDebugPanelObj.transform, "Btn_AntiMinus", "Red Team -1% [Shift+F11 / -]", new Color(0.55f, 0.15f, 0.08f, 0.95f), () => DebugDecreaseAntiEnergy(10));
            CreateDebugButton(_energyDebugPanelObj.transform, "Btn_Finish", "🏁 Finish [F12]", new Color(0.92f, 0.65f, 0.1f, 0.95f), () => MockTriggerFinishLineApproach(50f));
        }

        private void CreateDebugButton(Transform parent, string name, string label, Color bgColor, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            Image img = btnObj.GetComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(action);

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 12;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 9;
            tmp.fontSizeMax = 13;
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

            if (_energyDebugPanelObj != null)
            {
                _energyDebugPanelObj.SetActive(isDebugUIVisible);
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
                    btnRect.anchoredPosition = new Vector2(-20f, isDebugUIVisible ? 195f : 20f);
                }
            }

            if (toggleButtonText != null)
            {
                toggleButtonText.text = isDebugUIVisible ? "❌ Hide Debug" : "💬 Debug [~]";
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

            bool isShift = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;

            if (Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                ToggleDebugUI();
            }

            if (Keyboard.current.f12Key.wasPressedThisFrame)
            {
                MockTriggerFinishLineApproach(50f);
            }

            // F1: Spawn Khiên Bảo Vệ. Shift+F1: Bình Thao Tác Tự Do (Free-Control Buff 30s, GDD v1.4.1 mục 3).
            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                if (isShift) MockActivateFreeControl();
                else MockDonateShield();
            }

            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                if (isShift) MockToggleSprintBuffDebug();
                else MockActivateFanSprintBuff();
            }

            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                if (isShift) MockFanEnergyBottle();
                else MockSpawnSedanCar();
            }

            if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                if (isShift) MockAntiEnergyBottle();
                else MockActivatePickupTruckPhase();
            }

            if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                if (isShift) MockNewFollower();
                else MockActivateHeavyTruckPhase();
            }

            if (Keyboard.current.f7Key.wasPressedThisFrame)
                MockActivateAntiCarUnlimited();

            if (Keyboard.current.f8Key.wasPressedThisFrame)
            {
                if (isShift) MockWeatherHazard();
                else MockGiftDance();
            }

            if (Keyboard.current.f10Key.wasPressedThisFrame)
                MockBuyVipTicket();

            if (Keyboard.current.digit8Key.wasPressedThisFrame)
                MockWeatherHazard();

            if (Keyboard.current.digit9Key.wasPressedThisFrame)
                MockToggleSprintBuffDebug();

            if (Keyboard.current.digit0Key.wasPressedThisFrame)
                MockToggleUnlimitedModeDebug();

            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                if (isShift) DebugDecreaseFanEnergy(10);
                else DebugIncreaseFanEnergy(10);
            }

            if (Keyboard.current.f11Key.wasPressedThisFrame)
            {
                if (isShift) DebugDecreaseAntiEnergy(10);
                else DebugIncreaseAntiEnergy(10);
            }

            bool isChatFocused = chatInputField != null && chatInputField.isFocused;
            if (!isChatFocused)
            {
                if (Keyboard.current.leftBracketKey.wasPressedThisFrame)
                    DebugDecreaseFanEnergy(10);
                if (Keyboard.current.rightBracketKey.wasPressedThisFrame)
                    DebugIncreaseFanEnergy(10);

                if (Keyboard.current.minusKey.wasPressedThisFrame || (Keyboard.current.numpadMinusKey != null && Keyboard.current.numpadMinusKey.wasPressedThisFrame))
                    DebugDecreaseAntiEnergy(10);
                if (Keyboard.current.equalsKey.wasPressedThisFrame || (Keyboard.current.numpadPlusKey != null && Keyboard.current.numpadPlusKey.wasPressedThisFrame))
                    DebugIncreaseAntiEnergy(10);
            }

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

        private (string followerName, string command) ParseFollowerAndInput(string rawInput)
        {
            string trimmed = rawInput.Trim();

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

            string cmdLower = trimmed.ToLowerInvariant();
            if (cmdLower == "#fan" || cmdLower == "#blue" || cmdLower == "blue" || cmdLower == "fan" ||
                cmdLower == "#anti" || cmdLower == "#red" || cmdLower == "red" || cmdLower == "anti")
            {
                string newFollower = GetOrRotateFollowerName();
                _lastActiveFollowerName = newFollower;
                return (newFollower, trimmed);
            }

            if (string.IsNullOrEmpty(_lastActiveFollowerName))
            {
                _lastActiveFollowerName = GetOrRotateFollowerName();
            }

            return (_lastActiveFollowerName, trimmed);
        }

        public void SimulateViewerChat(string viewerName, string command)
        {
            _lastSubmittedFrame = -1;
            OnChatSubmitted($"{viewerName} {command}");
        }

        public void ExecuteChatCommand(string rawInput)
        {
            _lastSubmittedFrame = -1;
            OnChatSubmitted(rawInput);
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

            if (trimmedCmd == "blue" || trimmedCmd == "#blue")
            {
                bool isSwitching = factionManager != null && factionManager.HasFaction(followerName) && factionManager.GetFaction(followerName) == FactionType.Anti;
                factionManager?.SetFaction(followerName, FactionType.Fan);
                string actionText = isSwitching ? "Switched to Blue Team" : "Joined Blue Team";
                hudManager?.ShowFanAction(followerName, actionText);
                ClearInputField();
                return;
            }
            else if (trimmedCmd == "red" || trimmedCmd == "#red")
            {
                bool isSwitching = factionManager != null && factionManager.HasFaction(followerName) && factionManager.GetFaction(followerName) == FactionType.Fan;
                factionManager?.SetFaction(followerName, FactionType.Anti);
                string actionText = isSwitching ? "Switched to Red Team" : "Joined Red Team";
                hudManager?.ShowAntiAction(followerName, actionText);
                ClearInputField();
                return;
            }

            int antiLane = -1;
            if (factionManager != null && factionManager.GetFaction(followerName) == FactionType.Anti)
            {
                if (trimmedCmd == "1") antiLane = 1;
                else if (trimmedCmd == "2") antiLane = 2;
                else if (trimmedCmd == "3") antiLane = 3;
            }

            if (antiLane != -1)
            {
                var obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
                if (obstacleSpawner != null)
                {
                    if (!obstacleSpawner.CanSpawnObstacle())
                    {
                        hudManager?.ShowAntiAction(followerName, "Max 2 Cars On Track!");
                        ClearInputField();
                        return;
                    }

                    if (!obstacleSpawner.CanSpawnObstacleOnLane(antiLane))
                    {
                        string occupiedLaneName = antiLane == 1 ? "1 (Left)" : (antiLane == 2 ? "2 (Mid)" : "3 (Right)");
                        hudManager?.ShowAntiAction(followerName, $"Lane {occupiedLaneName} Occupied!");
                        ClearInputField();
                        return;
                    }
                }

                if (factionManager != null)
                {
                    bool success = factionManager.TrySpawnAntiObstacleCar(followerName, antiLane);
                    if (success)
                    {
                        string laneName = antiLane == 1 ? "Left (1)" : (antiLane == 2 ? "Mid (2)" : "Right (3)");
                        hudManager?.ShowAntiAction(followerName, $"Spawned Car (Lane {laneName})");
                    }
                    else
                    {
                        hudManager?.ShowAntiAction(followerName, $"Need 100 Energy ({factionManager.AntiLikes})");
                    }
                }
                ClearInputField();
                return;
            }

            List<string> commands = _sanitizer.SanitizeAndParse(command);
            if (commands != null && commands.Count > 0)
            {
                if (chatLaneRunner == null)
                    chatLaneRunner = GetComponent<ChatLaneRunnerController>() ?? GetComponentInParent<ChatLaneRunnerController>() ?? FindFirstObjectByType<ChatLaneRunnerController>();

                if (chatLaneRunner != null)
                {
                    if (chatLaneRunner.IsControlLocked)
                    {
                        ClearInputField();
                        return;
                    }

                    chatLaneRunner.ExecuteCommands(commands);

                    string firstCmd = commands[0].ToLowerInvariant();
                    string actionDesc = firstCmd switch
                    {
                        "1" => "Lane 1 (Left)",
                        "2" => "Lane 2 (Mid)",
                        "3" => "Lane 3 (Right)",
                        "jump" or "j" => "Jump",
                        _ => $"Cmd: {firstCmd}"
                    };
                    hudManager?.ShowFanAction(followerName, actionDesc);
                }
                else
                {
                    Debug.LogWarning("[MockChatConsole] Không tìm thấy ChatLaneRunnerController để thực thi lệnh!");
                }
            }

            ClearInputField();
        }

        public void MockDonateShield(string sender = "Khán Giả")
        {
            if (chatLaneRunner == null)
            {
                chatLaneRunner = FindFirstObjectByType<ChatLaneRunnerController>();
            }

            if (itemEffects == null)
            {
                if (chatLaneRunner != null)
                {
                    itemEffects = chatLaneRunner.GetComponent<RunnerItemEffects>()
                        ?? chatLaneRunner.GetComponentInChildren<RunnerItemEffects>();
                }
                if (itemEffects == null)
                {
                    itemEffects = FindFirstObjectByType<RunnerItemEffects>();
                }
            }

            if (itemEffects != null)
            {
                float duration = shieldDuration > 0f ? shieldDuration : 15f;
                itemEffects.ActivateShield(duration);
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                hudManager?.ShowFanAction(sender, $"Khiên Bảo Vệ ({duration:F0}s)");
                hudManager?.ShowStatusPopup($"Khiên Kích Hoạt ({duration:F0}s)!", true);
                Debug.Log($"[MockChatConsole] F1 -> {sender} tặng Khiên: Kích hoạt trực tiếp Khiên bảo vệ {duration:F0}s cho Runner.");
            }
            else
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy RunnerItemEffects để kích hoạt Khiên!");
            }
        }

        // Quà Bình Tăng Tốc (Sprint Buff - Blue Team, F2): Chạy nhanh 30s không tốn năng lượng
        public void MockActivateFanSprintBuff(string sender = "Blue Team")
        {
            if (chatLaneRunner == null)
            {
                chatLaneRunner = FindFirstObjectByType<ChatLaneRunnerController>();
            }

            if (chatLaneRunner == null)
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy ChatLaneRunnerController.");
                return;
            }

            if (!chatLaneRunner.IsSprintBuffActive)
            {
                chatLaneRunner.ActivateSprintBuff(30f);
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                hudManager?.ShowFanAction(sender, "Speed Boost (30s)");
            }
        }

        // Quà Bình Thao Tác Tự Do (Free-Control Buff - Phe Fan, Shift+F1, GDD v1.4.1 mục 3):
        // Đổi Làn và Nhảy tiêu tốn 0% năng lượng trong 30s, kể cả khi Fan đang ở mức 0%.
        private void MockActivateFreeControl()
        {
            if (chatLaneRunner == null)
            {
                chatLaneRunner = FindFirstObjectByType<ChatLaneRunnerController>();
            }

            if (chatLaneRunner == null)
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy ChatLaneRunnerController.");
                return;
            }

            chatLaneRunner.ActivateFreeControl(30f);
            if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            hudManager?.ShowStatusPopup("[Phe Fan] BÌNH THAO TÁC TỰ DO kích hoạt 30s! Đổi Làn & Nhảy miễn phí năng lượng!", true);
        }

        // Phím 9 hoặc Shift+F2: Toggle Sprint Buff tự do để QA test
        private void MockToggleSprintBuffDebug()
        {
            if (chatLaneRunner == null)
            {
                chatLaneRunner = FindFirstObjectByType<ChatLaneRunnerController>();
            }

            if (chatLaneRunner == null)
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy ChatLaneRunnerController.");
                return;
            }

            bool newState = !chatLaneRunner.IsSprintBuffActive;
            chatLaneRunner.SetSprintBuffDebug(newState);

            if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            hudManager?.ShowFanAction("DEBUG", newState ? "Sprint: ON" : "Sprint: OFF");
        }

        public void MockSpawnSedanCar(string sender = "Red Team")
        {
            if (obstacleSpawner == null) obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
            if (obstacleSpawner != null)
            {
                bool success = obstacleSpawner.TriggerSpawnCarTier(StreamRushLive.Features.Spawning.VehicleTier.SedanCar);
                if (success)
                {
                    if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                    hudManager?.ShowAntiAction(sender, "Spawned Sedan");
                }
            }
        }

        public void MockSpawnPickupTruck(string sender = "Red Team")
        {
            if (obstacleSpawner == null) obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
            if (obstacleSpawner != null)
            {
                bool success = obstacleSpawner.TriggerSpawnCarTier(StreamRushLive.Features.Spawning.VehicleTier.PickupTruck);
                if (success)
                {
                    if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                    hudManager?.ShowAntiAction(sender, "Spawned Pickup");
                }
            }
        }

public void MockActivatePickupTruckPhase(string sender = "Phe Anti")
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

            obstacleSpawner.ActivatePickupTruckPhase();

            if (hudManager == null)
            {
                hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            }

            hudManager?.ShowAntiAction(
                sender,
                "Pickup Truck Rush (60s)");

            hudManager?.ShowStatusPopup(
            "Giai đoạn Xe Bán Tải bắt đầu - 60s",
            false);

            Debug.Log("[MockChatConsole] F4 -> Pickup Truck Phase 60s activated.");
        }

        public void MockSpawnHeavyTruck(string sender = "Red Team")
        {
            if (obstacleSpawner == null) obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
            if (obstacleSpawner != null)
            {
                bool success = obstacleSpawner.TriggerSpawnCarTier(StreamRushLive.Features.Spawning.VehicleTier.HeavyTruck);
                if (success)
                {
                    if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                    hudManager?.ShowAntiAction(sender, "Spawned Heavy Truck");
                }
            }
        }

        public void MockActivateHeavyTruckPhase(string sender = "Phe Anti")
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

            obstacleSpawner.ActivateHeavyTruckPhase();

            if (hudManager == null)
            {
                hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            }

            hudManager?.ShowAntiAction(
                sender,
                "Heavy Truck Onslaught (60s)");

            hudManager?.ShowStatusPopup(
            "Giai đoạn Xe Tải Hạng Nặng bắt đầu - 60s",
            false);

            Debug.Log("[MockChatConsole] F5 -> Heavy Truck Phase 60s activated.");
        }

        public void MockFanEnergyBottle(string sender = "Viewer_Blue")
        {
            if (factionManager == null) factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            if (factionManager != null)
            {
                factionManager.OnChatCommand(sender, "#fan");
                factionManager.DebugAdjustFanEnergy(300);
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                hudManager?.ShowFanAction(sender, "Bình Năng Lượng (+300)");
                hudManager?.ShowStatusPopup("+30% Blue Team Energy (+300)!", true);
                Debug.Log($"[MockChatConsole] {sender} tặng Bình Năng Lượng Blue Team +30% (+300). (Total: {factionManager.FanLikes})");
            }
            else
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy FactionTugOfWarManager.");
            }
        }

        public void MockAntiEnergyBottle(string sender = "Viewer_Red")
        {
            if (factionManager == null) factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            if (factionManager != null)
            {
                factionManager.OnChatCommand(sender, "#anti");
                factionManager.DebugAdjustAntiEnergy(500);
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                hudManager?.ShowAntiAction(sender, "Bình Năng Lượng (+500)");
                hudManager?.ShowStatusPopup("+50% Red Team Energy (+500)!", false);
                Debug.Log($"[MockChatConsole] {sender} tặng Bình Năng Lượng Red Team +50% (+500). (Total: {factionManager.AntiLikes})");
            }
            else
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy FactionTugOfWarManager.");
            }
        }

        public void MockNewFollower(string customUserId = null)
        {
            if (queueManager == null)
                queueManager = FindFirstObjectByType<ChatRunnerQueueManager>();

            if (queueManager != null)
            {
                string newId = string.IsNullOrEmpty(customUserId) ? ("Follower_" + Random.Range(100, 999)) : customUserId;
                queueManager.TryEnqueueFollower(newId);
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                hudManager?.ShowFanAction(newId, "Followed & Queued");
                Debug.Log($"[MockChatConsole] F5 -> Enqueued new follower: {newId}");
            }
            else
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy ChatRunnerQueueManager.");
            }
        }

        public void MockActivateAntiCarUnlimited(string sender = "Red Team")
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
                hudManager?.ShowAntiAction(sender, "Unlimited Cars (60s)");
                Debug.Log("[MockChatConsole] F7 -> Unlimited Cars (60s) activated.");
            }
            else
            {
                Debug.Log("[MockChatConsole] F7 -> Unlimited Cars already active.");
            }
        }

        public void MockGiftDance(string sender = "Khán Giả")
        {
            if (giftDance == null)
            {
                giftDance = GetComponent<GiftDanceController>()
                    ?? GetComponentInParent<GiftDanceController>()
                    ?? FindFirstObjectByType<GiftDanceController>();
            }

            if (giftDance == null)
            {
                Debug.LogWarning("[MockChatConsole] Không tìm thấy GiftDanceController.");
                return;
            }

            if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();

            if (giftDance.TriggerDance())
            {
                hudManager?.ShowFanAction(sender, $"Dance ({giftDance.Duration:F0}s)");
            }
        }

        public void MockWeatherHazard(string sender = "Viewer_Red")
        {
            var weatherManager = SteamRush.Features.Environment.WeatherHazardManager.Instance ?? FindFirstObjectByType<SteamRush.Features.Environment.WeatherHazardManager>();
            if (weatherManager == null)
            {
                var go = new GameObject("WeatherHazardManager");
                weatherManager = go.AddComponent<SteamRush.Features.Environment.WeatherHazardManager>();
            }

            weatherManager.TriggerWeatherHazard(60f);

            if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            hudManager?.ShowAntiAction(sender, "Rain & Fog (60s)");
            Debug.Log($"[MockChatConsole] {sender} Weather Hazard 60s (FX_Rain & Fog -3.5f).");
        }

        public void MockBuyVipTicket(string customUserId = null)
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

            string newId = string.IsNullOrEmpty(customUserId) ? ("VIP_" + Random.Range(100, 999)) : customUserId;
            queueManager.TryEnqueuePriorityFollower(newId);

            if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            hudManager?.ShowFanAction(newId, "VIP Baton Pass");
        }

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
            hudManager?.ShowStatusPopup(newState ? "[DEBUG] Unlimited Mode: ON" : "[DEBUG] Unlimited Mode: OFF", false);
        }

        private SteamRush.Features.UI.FactionTugOfWarUI _cachedFactionUI;
        private SteamRush.Features.UI.FactionTugOfWarUI GetFactionUI()
        {
            if (_cachedFactionUI == null) _cachedFactionUI = FindFirstObjectByType<SteamRush.Features.UI.FactionTugOfWarUI>();
            return _cachedFactionUI;
        }

        private int GetFactionPercent(int likes, bool isFan)
        {
            var ui = GetFactionUI();
            int max = ui != null ? (isFan ? ui.FanMaxValue : ui.AntiMaxValue) : 1000;
            if (max <= 0) max = 1000;
            return Mathf.Clamp(Mathf.RoundToInt(100f * likes / max), 0, 100);
        }

        public void DebugIncreaseFanEnergy(int amount = 10)
        {
            if (factionManager == null) factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            if (factionManager != null)
            {
                factionManager.DebugAdjustFanEnergy(amount);
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                int percent = GetFactionPercent(factionManager.FanLikes, isFan: true);
                hudManager?.ShowStatusPopup($"[Debug] Blue Team +{amount} (+1%) ({factionManager.FanLikes} | {percent}%)", true);
            }
        }

        public void DebugDecreaseFanEnergy(int amount = 10)
        {
            if (factionManager == null) factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            if (factionManager != null)
            {
                factionManager.DebugAdjustFanEnergy(-amount);
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                int percent = GetFactionPercent(factionManager.FanLikes, isFan: true);
                hudManager?.ShowStatusPopup($"[Debug] Blue Team -{amount} (-1%) ({factionManager.FanLikes} | {percent}%)", true);
            }
        }

        public void DebugIncreaseAntiEnergy(int amount = 10)
        {
            if (factionManager == null) factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            if (factionManager != null)
            {
                factionManager.DebugAdjustAntiEnergy(amount);
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                int percent = GetFactionPercent(factionManager.AntiLikes, isFan: false);
                hudManager?.ShowStatusPopup($"[Debug] Red Team +{amount} (+1%) ({factionManager.AntiLikes} | {percent}%)", false);
            }
        }

        public void DebugDecreaseAntiEnergy(int amount = 10)
        {
            if (factionManager == null) factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            if (factionManager != null)
            {
                factionManager.DebugAdjustAntiEnergy(-amount);
                if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
                int percent = GetFactionPercent(factionManager.AntiLikes, isFan: false);
                hudManager?.ShowStatusPopup($"[Debug] Red Team -{amount} (-1%) ({factionManager.AntiLikes} | {percent}%)", false);
            }
        }

        [ContextMenu("Debug Trigger Finish Line (50m ahead)")]
        public void MockTriggerFinishLineApproach(float distanceAhead = 50f)
        {
            var progressTracker = FindFirstObjectByType<SteamRush.Track.TrackProgressTracker>();
            if (progressTracker != null)
            {
                progressTracker.DebugJumpNearGoal(distanceAhead);
            }

            var archway = FindFirstObjectByType<SteamRush.Track.FinishLineArchway>();
            if (archway != null)
            {
                archway.DebugSpawnArchway(distanceAhead);
            }

            Debug.Log($"[MockChatConsole] F12 -> Jumped to {distanceAhead:F0}m before Finish Line!");
        }

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
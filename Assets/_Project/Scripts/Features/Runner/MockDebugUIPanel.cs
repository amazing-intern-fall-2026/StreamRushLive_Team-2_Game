using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Manages procedural vertical debug buttons and panel visibility toggling.
    /// Extracted from MockChatConsole to adhere to the Single Responsibility Principle (SRP).
    /// </summary>
    public class MockDebugUIPanel : MonoBehaviour
    {
        [Header("UI Containers")]
        [SerializeField] private TMP_InputField chatInputField;
        [SerializeField] private GameObject debugPanelContainer;
        [SerializeField] private Button toggleDebugButton;
        [SerializeField] private TMP_Text toggleButtonText;
        [SerializeField] private bool isDebugUIVisible = false;

        private GameObject _quickHelpBarObj;
        private GameObject _energyDebugPanelObj;

        public bool IsDebugUIVisible => isDebugUIVisible;
        public TMP_InputField ChatInputField => chatInputField;

        public void Initialize(
            TMP_InputField inputField,
            Action onToggleDebug,
            Action<int> onFanEnergyChanged,
            Action<int> onAntiEnergyChanged,
            Action onFreeControl,
            Action onLiveDemo,
            Action onFinishApproach)
        {
            chatInputField = inputField;
            SetupVerticalDebugUI(onToggleDebug, onFanEnergyChanged, onAntiEnergyChanged, onFreeControl, onLiveDemo, onFinishApproach);
        }

        public void SetupVerticalDebugUI(
            Action onToggleDebug,
            Action<int> onFanEnergyChanged,
            Action<int> onAntiEnergyChanged,
            Action onFreeControl,
            Action onLiveDemo,
            Action onFinishApproach)
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

            Canvas canvas = chatInputField != null ? chatInputField.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                if (toggleDebugButton == null)
                {
                    CreateToggleDebugButton(canvas.transform, onToggleDebug);
                }

                if (_energyDebugPanelObj == null)
                {
                    CreateEnergyDebugButtons(canvas.transform, onFanEnergyChanged, onAntiEnergyChanged, onFreeControl, onLiveDemo, onFinishApproach);
                }
            }

            UpdateDebugUIVisibility();
        }

        private void CreateEnergyDebugButtons(
            Transform canvasTransform,
            Action<int> onFanEnergyChanged,
            Action<int> onAntiEnergyChanged,
            Action onFreeControl,
            Action onLiveDemo,
            Action onFinishApproach)
        {
            _energyDebugPanelObj = new GameObject("Panel_EnergyDebugRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            _energyDebugPanelObj.transform.SetParent(canvasTransform, false);

            RectTransform rect = _energyDebugPanelObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(780f, 32f);
            rect.anchoredPosition = new Vector2(0f, 155f);

            HorizontalLayoutGroup hlg = _energyDebugPanelObj.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            CreateDebugButton(_energyDebugPanelObj.transform, "Btn_FanPlus", "Blue +1% [F6]", new Color(0.12f, 0.45f, 0.85f, 0.95f), () => onFanEnergyChanged?.Invoke(10));
            CreateDebugButton(_energyDebugPanelObj.transform, "Btn_FanMinus", "Blue -1% [Shift+F6]", new Color(0.08f, 0.25f, 0.55f, 0.95f), () => onFanEnergyChanged?.Invoke(-10));
            CreateDebugButton(_energyDebugPanelObj.transform, "Btn_AntiPlus", "Red +1% [F11]", new Color(0.85f, 0.28f, 0.15f, 0.95f), () => onAntiEnergyChanged?.Invoke(10));
            CreateDebugButton(_energyDebugPanelObj.transform, "Btn_AntiMinus", "Red -1% [Shift+F11]", new Color(0.55f, 0.15f, 0.08f, 0.95f), () => onAntiEnergyChanged?.Invoke(-10));
            CreateDebugButton(_energyDebugPanelObj.transform, "Btn_FreeControl", "Free 30s [Shift+F1]", new Color(0.08f, 0.65f, 0.72f, 0.95f), () => onFreeControl?.Invoke());
            CreateDebugButton(_energyDebugPanelObj.transform, "Btn_LiveDemo", "Bot Demo [L]", new Color(0.15f, 0.55f, 0.6f, 0.95f), () => onLiveDemo?.Invoke());
            CreateDebugButton(_energyDebugPanelObj.transform, "Btn_Finish", "Finish [F12]", new Color(0.92f, 0.65f, 0.1f, 0.95f), () => onFinishApproach?.Invoke());
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

        private void CreateToggleDebugButton(Transform canvasTransform, Action onToggleDebug)
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
            toggleDebugButton.onClick.AddListener(() => onToggleDebug?.Invoke());

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            toggleButtonText = textObj.GetComponent<TextMeshProUGUI>();
            toggleButtonText.text = "Debug [F12]";
            toggleButtonText.fontSize = 14;
            toggleButtonText.alignment = TextAlignmentOptions.Center;
            toggleButtonText.color = new Color(0.5f, 0.85f, 1f, 1f);
        }

        public void ToggleDebugUI()
        {
            isDebugUIVisible = !isDebugUIVisible;
            UpdateDebugUIVisibility();
        }

        public void SetDebugUIVisible(bool visible)
        {
            isDebugUIVisible = visible;
            UpdateDebugUIVisibility();
        }

        public void UpdateDebugUIVisibility()
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
                bool allowDebug = isDebugUIVisible;
                if (!isDebugUIVisible && SteamRush.Features.UI.PreGameConfig.PreGameConfigManager.Instance != null &&
                    SteamRush.Features.UI.PreGameConfig.PreGameConfigManager.Instance.CurrentConfig != null)
                {
                    allowDebug = SteamRush.Features.UI.PreGameConfig.PreGameConfigManager.Instance.CurrentConfig.enableDebugUI;
                }

                toggleDebugButton.gameObject.SetActive(allowDebug);
                RectTransform btnRect = toggleDebugButton.GetComponent<RectTransform>();
                if (btnRect != null)
                {
                    btnRect.anchoredPosition = new Vector2(-20f, isDebugUIVisible ? 195f : 20f);
                }
            }

            if (toggleButtonText != null)
            {
                toggleButtonText.text = isDebugUIVisible ? "Hide Debug" : "Debug [~]";
            }
        }
    }
}

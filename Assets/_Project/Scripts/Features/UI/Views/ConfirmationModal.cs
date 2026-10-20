using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SteamRush.Core.Localization;
using SteamRush.Features.UI.PreGameConfig;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Reusable 2-button confirmation modal ([Confirm] / [Cancel]).
    /// Built entirely in code on its own overlay Canvas, so it needs no prefab or scene wiring
    /// and always renders above the PreGameConfig modal and the HUD.
    /// Uses no tweens and no scaled time, so it works while Time.timeScale == 0.
    /// </summary>
    public class ConfirmationModal : MonoBehaviour
    {
        private static ConfirmationModal _instance;

        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _messageText;
        private TextMeshProUGUI _confirmText;
        private TextMeshProUGUI _cancelText;
        private Action _onConfirm;
        private Action _onCancel;

        public static bool IsShowing => _instance != null && _instance.gameObject.activeSelf;

        /// <summary>Picks the Vietnamese or English string according to the current app language.</summary>
        public static string T(string vietnamese, string english)
        {
            var loc = LocalizationManager.Instance;
            bool isVietnamese = loc != null && loc.CurrentLanguage == AppLanguage.Vietnamese;
            return isVietnamese ? vietnamese : english;
        }

        public static void Show(string title, string message, string confirmLabel, string cancelLabel,
                                Action onConfirm, Action onCancel = null)
        {
            if (_instance == null)
            {
                _instance = Create();
            }

            _instance.Open(title, message, confirmLabel, cancelLabel, onConfirm, onCancel);
        }

        private static ConfirmationModal Create()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                Debug.LogWarning("[ConfirmationModal] No EventSystem found in the scene - buttons will not respond to clicks.");
            }

            var go = new GameObject("ConfirmationModal");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000; // above PreGameConfig modal, HUD and victory popup

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            var modal = go.AddComponent<ConfirmationModal>();
            modal.Build();
            go.SetActive(false);
            return modal;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private float _timeScaleBeforeOpen = 1f;

        private void Open(string title, string message, string confirmLabel, string cancelLabel,
                          Action onConfirm, Action onCancel)
        {
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            // Pause game timescale while the confirmation dialog is up
            _timeScaleBeforeOpen = Time.timeScale;
            Time.timeScale = 0f;

            _titleText.text = title;
            _messageText.text = message;
            _confirmText.text = confirmLabel;
            _cancelText.text = cancelLabel;

            gameObject.SetActive(true);
        }

        private void HandleConfirm()
        {
            var callback = _onConfirm;
            Close();
            AudioManager.Instance?.PlaySFX(SFXType.UIButtonClick);
            callback?.Invoke();
        }

        private void HandleCancel()
        {
            var callback = _onCancel;
            Close();
            AudioManager.Instance?.PlaySFX(SFXType.UIButtonClick);
            callback?.Invoke();
        }

        private void Close()
        {
            _onConfirm = null;
            _onCancel = null;
            // Restore previous timescale if not reloading scene
            Time.timeScale = _timeScaleBeforeOpen;
            gameObject.SetActive(false);
        }

        #region UI construction

        private void Build()
        {
            var theme = HudTheme.Current;
            Color gold = theme != null ? (Color)theme.gold : (Color)new Color32(0xFC, 0xDA, 0x21, 0xFF);
            Sprite panelFrame = theme != null ? theme.panelFrame : null;
            Sprite pillGold = theme != null ? theme.pillGold : null;
            Sprite pillDark = theme != null ? theme.pillDark : null;
            TMP_FontAsset fontTitle = theme != null && theme.fontHeavy != null ? theme.fontHeavy : (theme != null ? theme.fontBold : null);
            TMP_FontAsset fontBody = theme != null && theme.fontBold != null ? theme.fontBold : null;

            // Full-screen dim backdrop (blocks clicks on everything behind the modal)
            var backdrop = CreateRect("Backdrop", transform);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = Vector2.zero;
            backdrop.offsetMax = Vector2.zero;
            var backdropImg = backdrop.gameObject.AddComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0.75f);
            backdropImg.raycastTarget = true;

            // Card Panel
            var card = CreateRect("Card", backdrop);
            Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 470f));
            var cardImg = card.gameObject.AddComponent<Image>();

            if (panelFrame != null)
            {
                cardImg.sprite = panelFrame;
                cardImg.type = Image.Type.Sliced;
                cardImg.pixelsPerUnitMultiplier = 0.75f;
                cardImg.color = Color.white;
            }
            else
            {
                cardImg.color = new Color32(0x12, 0x2B, 0x36, 0xFA);
                var cardOutline = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
                cardOutline.effectColor = gold;
                cardOutline.effectDistance = new Vector2(3f, -3f);
            }

            // Header Title
            _titleText = CreateText("Title", card, 44f, gold, FontStyles.Bold, fontTitle);
            Place(_titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -65f), new Vector2(740f, 70f));

            // Message Body - Crisp pure white with bold weight and dark outline shadow for high readability
            _messageText = CreateText("Message", card, 28f, Color.white, FontStyles.Bold, fontBody);
            _messageText.textWrappingMode = TextWrappingModes.Normal;
            _messageText.lineSpacing = 16f;
            _messageText.outlineWidth = 0.2f;
            _messageText.outlineColor = new Color32(0x06, 0x1A, 0x24, 0xEE);
            Place(_messageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 15f), new Vector2(740f, 180f));

            // Buttons Container / Action Buttons
            var confirmBtn = CreateButton("BtnConfirm", card, pillGold, Color.white,
                                          new Color32(0x1A, 0x1A, 0x1A, 0xFF), out _confirmText, fontTitle);
            Place(confirmBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-170f, 75f), new Vector2(310f, 78f));
            confirmBtn.onClick.AddListener(HandleConfirm);

            var cancelBtn = CreateButton("BtnCancel", card, pillDark, Color.white,
                                         new Color32(0xBF, 0xE3, 0xE8, 0xFF), out _cancelText, fontTitle);
            Place(cancelBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(170f, 75f), new Vector2(310f, 78f));
            cancelBtn.onClick.AddListener(HandleCancel);

            // Fallback colours when the HUD theme has no pill sprites
            if (pillGold == null) confirmBtn.GetComponent<Image>().color = gold;
            if (pillDark == null) cancelBtn.GetComponent<Image>().color = new Color32(0x20, 0x61, 0x72, 0xFF);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        private static TextMeshProUGUI CreateText(string name, RectTransform parent, float size, Color color, FontStyles style, TMP_FontAsset font = null)
        {
            var rt = CreateRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateButton(string name, RectTransform parent, Sprite sprite, Color tint,
                                           Color labelColor, out TextMeshProUGUI label, TMP_FontAsset font = null)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 0.8f;
            }
            img.color = tint;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            // Clean button hover and press transitions
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = colors;

            label = CreateText("Label", rt, 26f, labelColor, FontStyles.Bold, font);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10f, 6f);
            label.rectTransform.offsetMax = new Vector2(-10f, -6f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return btn;
        }

        #endregion
    }
}
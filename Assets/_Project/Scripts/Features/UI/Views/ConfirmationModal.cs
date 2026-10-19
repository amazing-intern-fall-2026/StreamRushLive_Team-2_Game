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

        private void Open(string title, string message, string confirmLabel, string cancelLabel,
                          Action onConfirm, Action onCancel)
        {
            _onConfirm = onConfirm;
            _onCancel = onCancel;

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
            gameObject.SetActive(false);
        }

        #region UI construction

        private void Build()
        {
            var theme = HudTheme.Current;
            Color gold = theme != null ? (Color)theme.gold : (Color)new Color32(0xFC, 0xDA, 0x21, 0xFF);
            Color textPrimary = theme != null ? (Color)theme.textPrimary : Color.white;
            Sprite pillGold = theme != null ? theme.pillGold : null;
            Sprite pillDark = theme != null ? theme.pillDark : null;

            // Full-screen dim backdrop (blocks clicks on everything behind the modal)
            var backdrop = CreateRect("Backdrop", transform);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = Vector2.zero;
            backdrop.offsetMax = Vector2.zero;
            var backdropImg = backdrop.gameObject.AddComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0.7f);
            backdropImg.raycastTarget = true;

            // Card
            var card = CreateRect("Card", backdrop);
            Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 440f));
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.color = new Color32(0x12, 0x2B, 0x36, 0xFF);
            var cardOutline = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
            cardOutline.effectColor = gold;
            cardOutline.effectDistance = new Vector2(3f, -3f);

            // Title
            _titleText = CreateText("Title", card, 46f, gold, FontStyles.Bold);
            Place(_titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(740f, 84f));

            // Message
            _messageText = CreateText("Message", card, 31f, textPrimary, FontStyles.Normal);
            _messageText.textWrappingMode = TextWrappingModes.Normal;
            Place(_messageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(720f, 190f));

            // Buttons
            var confirmBtn = CreateButton("BtnConfirm", card, pillGold, Color.white,
                                          new Color32(0x1A, 0x1A, 0x1A, 0xFF), out _confirmText);
            Place(confirmBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-170f, 72f), new Vector2(310f, 88f));
            confirmBtn.onClick.AddListener(HandleConfirm);

            var cancelBtn = CreateButton("BtnCancel", card, pillDark, Color.white,
                                         new Color32(0xBF, 0xE3, 0xE8, 0xFF), out _cancelText);
            Place(cancelBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(170f, 72f), new Vector2(310f, 88f));
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

        private static TextMeshProUGUI CreateText(string name, RectTransform parent, float size, Color color, FontStyles style)
        {
            var rt = CreateRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateButton(string name, RectTransform parent, Sprite sprite, Color tint,
                                           Color labelColor, out TextMeshProUGUI label)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            img.color = tint;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            label = CreateText("Label", rt, 30f, labelColor, FontStyles.Bold);
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
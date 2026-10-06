using DG.Tweening;
using EasyTextEffects;
using TMPro;
using UnityEngine;

namespace SteamRush.Features.UI.Views
{
    // View: floating status popup tweening upward and fading out adhering to SRP.
    public class StatusPopupController : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private UnityEngine.UI.Image iconImage;
        [SerializeField] private CanvasGroup canvasGroup;
        // Optional character pop animation support
        [SerializeField] private TextEffect textEffect;
        [SerializeField] private float floatDistance = 50f;
        [Tooltip("Total popup duration in seconds (default: 3.5s).")]
        [SerializeField] private float duration = 3.5f;
        [Tooltip("Opaque hold duration prior to fadeout (default: 2.5s).")]
        [SerializeField] private float holdDuration = 2.5f;

        [Header("Text Settings")]
        [Tooltip("Prevent word wrapping, keeping text on a single line.")]
        [SerializeField] private bool noWrap = true;
        [Tooltip("Display optional icon alongside text.")]
        [SerializeField] private bool showIcon = false;

        // Strips unicode emojis to avoid TextMeshPro font missing glyph warnings
        private static readonly System.Text.RegularExpressions.Regex EmojiRegex = new System.Text.RegularExpressions.Regex(
            @"[\uD83C-\uDBFF\uDC00-\uDFFF\u2600-\u27BF\u2300-\u23FF\u2B50-\u2B55\uFE0F]",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        // Faction color coding: Blue Team (buff) / Red Team (debuff) mapped from HudTheme.
        private static Color BuffColor => HudTheme.Current.blue;
        private static Color DebuffColor => HudTheme.Current.red;

        // Inherit base position from template at spawn time
        private Vector2 startAnchoredPosition;

        private void Awake()
        {
            startAnchoredPosition = ((RectTransform)transform).anchoredPosition;
            ApplyNoWrap();
            ApplyCartoonOutline();
        }

        private void ApplyNoWrap()
        {
            if (label != null && noWrap)
            {
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        // Black outline styling for stylized game UI text
        private void ApplyCartoonOutline()
        {
            if (label == null)
            {
                return;
            }

            Material material = label.fontMaterial;
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        }

        // Hide icon element when no icon sprite is supplied
        public void Play(
            string message,
            bool isBuff,
            Sprite icon = null,
            Color? iconColor = null,
            System.Action onComplete = null,
            float customDuration = -1f,
            float customHold = -1f)
        {
            var rect = (RectTransform)transform;
            rect.anchoredPosition = startAnchoredPosition;

            float activeDuration = customDuration > 0f ? customDuration : duration;
            float activeHold = customHold > 0f ? customHold : holdDuration;
            activeHold = Mathf.Min(activeHold, activeDuration * 0.8f);

            // Cleanse residual emojis from string
            if (!string.IsNullOrEmpty(message))
            {
                message = EmojiRegex.Replace(message, "").Trim();
            }

            bool isBlue = isBuff;
            if (!string.IsNullOrEmpty(message))
            {
                string lower = message.ToLowerInvariant();
                if (lower.Contains("blue") || lower.Contains("fan") || lower.Contains("free control") || lower.Contains("shield") || lower.Contains("sprint"))
                {
                    isBlue = true;
                }
                else if (lower.Contains("red") || lower.Contains("anti") || lower.Contains("car") || lower.Contains("truck") || lower.Contains("sedan") || lower.Contains("pickup"))
                {
                    isBlue = false;
                }

                // Strip [Red Team] and [Blue Team] prefixes
                message = System.Text.RegularExpressions.Regex.Replace(message, @"\[(Blue|Red)\s*Team\]\s*:?\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            }

            if (label != null)
            {
                ApplyNoWrap();
                label.text = message;
                label.color = isBlue ? BuffColor : DebuffColor;
            }

            if (textEffect != null)
            {
                // Refresh bat buoc phai goi SAU khi doi label.text, vi no doc do dai chu hien tai
                // de tinh so ky tu can ap hieu ung - goi truoc se ap sai len chu "Preview" cua template.
                textEffect.Refresh();
                textEffect.StartManualEffects();
            }

            if (iconImage != null)
            {
                bool shouldShow = showIcon && icon != null;
                iconImage.gameObject.SetActive(shouldShow);
                if (shouldShow)
                {
                    iconImage.sprite = icon;
                    iconImage.color = iconColor ?? Color.white;
                }
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            DOTween.Kill(rect);

            // Sequence setup: Append initial tween then Join subsequent animations
            Sequence sequence = DOTween.Sequence().SetTarget(rect);
            sequence.Append(rect.DOAnchorPosY(startAnchoredPosition.y + floatDistance, activeDuration).SetEase(Ease.OutCubic));
            if (canvasGroup != null)
            {
                float fadeTime = Mathf.Max(0.2f, activeDuration - activeHold);
                sequence.Insert(activeHold, canvasGroup.DOFade(0f, fadeTime).SetEase(Ease.InQuad));
            }
            sequence.OnComplete(() =>
            {
                onComplete?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}

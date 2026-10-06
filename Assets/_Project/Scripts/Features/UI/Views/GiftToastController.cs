using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI.Views
{
    // View: displays single gift toast notification adhering to SRP.
    public class GiftToastController : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text viewerNameText;
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Outline cardOutline;
        [SerializeField] private float showDuration = 4.2f;
        [SerializeField] private float animDuration = 0.35f;
        [SerializeField] private bool hideIconIfNull = false;

        public void SetDuration(float showDur, float animDur = -1f)
        {
            showDuration = showDur;
            if (animDur > 0f) animDuration = animDur;
        }

        // Strips unicode emojis to avoid TextMeshPro font missing glyph warnings
        private static readonly System.Text.RegularExpressions.Regex EmojiRegex = new System.Text.RegularExpressions.Regex(
            @"[\uD83C-\uDBFF\uDC00-\uDFFF\u2600-\u27BF\u2300-\u23FF\u2B50-\u2B55\uFE0F]",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        // Subscribed by GiftToastQueue to remove from active list upon dismiss
        // (both on natural expiry and force dismiss).
        public event System.Action<GiftToastController> Dismissed;
        private bool _dismissed;

        // accentColor: theme color for event/warning (blue = positive, red = hazard).
        // showItemName: set to false for compact gift notifications (icon + viewer name only).
        public void Play(string viewerName, string itemName, Sprite icon, Color? iconColor = null, Color? accentColor = null, bool showItemName = true)
        {
            Color? resolvedIconColor = iconColor ?? accentColor;

            if (iconImage != null)
            {
                if (icon != null)
                {
                    iconImage.gameObject.SetActive(true);
                    iconImage.sprite = icon;
                    // Tint monochrome icon sprite to match item theme
                    iconImage.color = resolvedIconColor ?? Color.white;
                }
                else if (hideIconIfNull)
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            if (accentColor.HasValue)
            {
                Color c = accentColor.Value;
                bool isBlueTone = (c.b > c.r) || (c.g > c.r);

                // Donation and celebration toasts use bright blue/red pill skins from the theme kit.
                HudTheme theme = HudTheme.Current;
                Image bgImage = GetComponent<Image>();
                if (bgImage != null)
                {
                    Sprite pill = theme.FactionPill(isBlueTone);
                    if (pill != null)
                    {
                        bgImage.sprite = pill;
                        bgImage.type = Image.Type.Sliced;
                    }
                    bgImage.color = Color.white;
                }

                if (cardOutline != null)
                {
                    cardOutline.enabled = false;
                }

                if (itemNameText != null)
                {
                    itemNameText.color = theme.textPrimary;
                }
            }

            if (viewerNameText != null)
            {
                viewerNameText.text = string.IsNullOrEmpty(viewerName) ? viewerName : EmojiRegex.Replace(viewerName, "").Trim();
            }

            if (itemNameText != null)
            {
                itemNameText.gameObject.SetActive(showItemName);
                itemNameText.text = string.IsNullOrEmpty(itemName) ? itemName : EmojiRegex.Replace(itemName, "").Trim();
            }

            // Use scale + fade animations inside VerticalLayoutGroup to avoid layout conflicts
            transform.localScale = Vector3.one * 0.85f;
            canvasGroup.alpha = 0f;
            _dismissed = false;

            DOTween.Kill(transform);

            Sequence sequence = DOTween.Sequence().SetTarget(transform);
            sequence.Append(canvasGroup.DOFade(1f, animDuration));
            sequence.Join(transform.DOScale(1f, animDuration).SetEase(Ease.OutBack));
            sequence.AppendInterval(showDuration);
            sequence.Append(canvasGroup.DOFade(0f, animDuration));
            sequence.Join(transform.DOScale(0.85f, animDuration).SetEase(Ease.InBack));
            sequence.OnComplete(() => Dismiss());
        }

        // Called by GiftToastQueue when active toasts exceed max limit (donation spam)
        // to immediately fade out and make room for newer toasts.
        public void ForceDismiss()
        {
            if (this == null || _dismissed) return;

            try
            {
                if (transform != null)
                {
                    DOTween.Kill(transform);
                    Sequence sequence = DOTween.Sequence().SetTarget(transform);
                    if (canvasGroup != null)
                    {
                        sequence.Append(canvasGroup.DOFade(0f, animDuration * 0.6f));
                    }
                    sequence.Join(transform.DOScale(0.85f, animDuration * 0.6f).SetEase(Ease.InBack));
                    sequence.OnComplete(() => Dismiss());
                    return;
                }
            }
            catch (System.Exception)
            {
                // Target object already destroyed
            }

            Dismiss();
        }

        private void OnDestroy()
        {
            try
            {
                if (this != null && transform != null)
                {
                    DOTween.Kill(transform);
                }
            }
            catch (System.Exception) { }

            if (!_dismissed)
            {
                _dismissed = true;
                Dismissed?.Invoke(this);
            }
        }

        private void Dismiss()
        {
            if (_dismissed) return;
            _dismissed = true;

            Dismissed?.Invoke(this);
            if (this != null && gameObject != null)
            {
                Destroy(gameObject);
            }
        }
    }
}

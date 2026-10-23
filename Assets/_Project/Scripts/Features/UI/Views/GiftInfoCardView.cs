using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SteamRush.Features.StreamIntegration;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Single Responsibility: Displays and manages the visual state and animations
    /// for a single TikTok gift card inside the GiftInfoPanel.
    /// Uses pre-wired serialized components from GiftInfoCard.prefab instead of procedural construction.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GiftInfoCardView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button _button;
        [SerializeField] private Image _bgImage;
        [SerializeField] private Image _bodyImage;
        [SerializeField] private Outline _outline;
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _descText;

        private RectTransform _rectTransform;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        private Coroutine _bounceRoutine;
        private Vector3 _originalScale = Vector3.one;

        private void Awake()
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_button == null) _button = GetComponent<Button>();
            _originalScale = transform.localScale;
        }

        /// <summary>
        /// Binds the gift data, colors, icons, and click actions to this card.
        /// </summary>
        public void Bind(
            TikTokGiftMapping mapping,
            Sprite resolvedIcon,
            bool showIcon,
            string conciseDesc,
            Color textColor,
            Color fillColor,
            Color borderColor,
            Sprite flatSprite,
            Action onClick)
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();

            // 1. Body and Outline
            if (_bodyImage != null)
            {
                if (flatSprite != null)
                {
                    _bodyImage.sprite = flatSprite;
                    _bodyImage.type = Image.Type.Sliced;
                }
                fillColor.a = 1f;
                _bodyImage.color = fillColor;
            }

            if (_outline != null)
            {
                _outline.effectColor = borderColor;
            }

            // 2. Icon
            if (_iconImage != null)
            {
                _iconImage.gameObject.SetActive(showIcon);
                if (showIcon)
                {
                    _iconImage.sprite = resolvedIcon;
                    _iconImage.color = resolvedIcon != null ? Color.white : Color.clear;
                }
            }

            // 3. Description Label
            if (_descText != null)
            {
                _descText.text = conciseDesc;
                _descText.color = textColor;
                _descText.fontSizeMax = showIcon ? 13.5f : 16f;

                RectTransform descRt = _descText.rectTransform;
                if (showIcon)
                {
                    descRt.anchorMin = new Vector2(0.04f, 0.05f);
                    descRt.anchorMax = new Vector2(0.96f, 0.38f);
                }
                else
                {
                    descRt.anchorMin = new Vector2(0.06f, 0.06f);
                    descRt.anchorMax = new Vector2(0.94f, 0.94f);
                }
                descRt.offsetMin = Vector2.zero;
                descRt.offsetMax = Vector2.zero;
            }

            // 4. Click Listener
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                if (onClick != null)
                {
                    _button.onClick.AddListener(() => onClick.Invoke());
                }
            }
        }

        public void SetIcon(Sprite sprite)
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = sprite;
                _iconImage.color = sprite != null ? Color.white : Color.clear;
            }
        }

        public void AnimateBounce()
        {
            if (_bounceRoutine != null)
            {
                StopCoroutine(_bounceRoutine);
            }
            _bounceRoutine = StartCoroutine(BounceRoutine());
        }

        private IEnumerator BounceRoutine()
        {
            float elapsed = 0f;
            float duration = 0.35f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.22f;
                transform.localScale = _originalScale * scale;
                yield return null;
            }

            transform.localScale = _originalScale;
            _bounceRoutine = null;
        }

#if UNITY_EDITOR
        public void ConfigureReferences(Button btn, Image bg, Image body, Outline outline, Image icon, TextMeshProUGUI desc)
        {
            _button = btn;
            _bgImage = bg;
            _bodyImage = body;
            _outline = outline;
            _iconImage = icon;
            _descText = desc;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}

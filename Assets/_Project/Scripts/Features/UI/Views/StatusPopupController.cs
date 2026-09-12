using DG.Tweening;
using EasyTextEffects;
using TMPro;
using UnityEngine;

namespace SteamRush.Features.UI.Views
{
    // View: 1 popup buff/debuff nổi lên rồi mờ dần, tự huỷ khi xong - không truy cập module khác (SRP).
    public class StatusPopupController : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private UnityEngine.UI.Image iconImage;
        [SerializeField] private CanvasGroup canvasGroup;
        // Hiệu ứng "pop" từng ký tự khi chữ xuất hiện (asset Easy Text Effects, free/MIT) - bổ sung cho tween nổi lên + mờ dần bên dưới.
        [SerializeField] private TextEffect textEffect;
        [SerializeField] private float floatDistance = 40f;
        [SerializeField] private float duration = 1.2f;

        [Header("Text Settings")]
        [Tooltip("Bật No Wrap để chữ luôn nằm trên 1 dòng duy nhất, không tự động xuống dòng.")]
        [SerializeField] private bool noWrap = true;
        [Tooltip("Bật để hiển thị ô icon bên cạnh chữ. Tắt để ẩn hoàn toàn các icon.")]
        [SerializeField] private bool showIcon = false;

        // Regex lọc sạch các ký tự emoji/icon unicode khỏi chuỗi text tránh lỗi font TextMeshPro
        private static readonly System.Text.RegularExpressions.Regex EmojiRegex = new System.Text.RegularExpressions.Regex(
            @"[\uD83C-\uDBFF\uDC00-\uDFFF\u2600-\u27BF\u2300-\u23FF\u2B50-\u2B55\uFE0F]",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        // Màu tạm thời phân biệt buff/debuff khi chưa có icon (GDD: buff = tích cực, debuff = cảnh báo).
        private static readonly Color BuffColor = new Color(0.4f, 0.85f, 0.45f);
        private static readonly Color DebuffColor = new Color(0.95f, 0.35f, 0.3f);

        // Vị trí gốc lấy từ template lúc spawn - không hardcode Vector2.zero vì template có thể đặt ở bất kỳ đâu phía trên đầu runner.
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

        // Viền đen đậm quanh chữ cho cảm giác cartoon/game-UI (kiểu Geometry Dash), không phải đổi font.
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

        // icon: null khi chưa có icon phù hợp (vd. debuff đang chờ bổ sung icon riêng) - ẩn hẳn ô icon thay vì để trống.
        public void Play(string message, bool isBuff, Sprite icon = null, Color? iconColor = null)
        {
            var rect = (RectTransform)transform;
            rect.anchoredPosition = startAnchoredPosition;

            // Xóa mọi emoji/icon còn sót lại trong chữ để văn bản luôn sạch đẹp
            if (!string.IsNullOrEmpty(message))
            {
                message = EmojiRegex.Replace(message, "").Trim();
            }

            if (label != null)
            {
                ApplyNoWrap();
                label.text = message;
                label.color = isBuff ? BuffColor : DebuffColor;
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

            // Append tween đầu tiên, các tween sau mới Join - Join làm tween đầu tiên trên Sequence rỗng
            // khiến DOTween tính sai tổng thời lượng, OnComplete bắn gần như ngay lập tức (popup huỷ trong 1 frame).
            Sequence sequence = DOTween.Sequence().SetTarget(rect);
            sequence.Append(rect.DOAnchorPosY(startAnchoredPosition.y + floatDistance, duration).SetEase(Ease.OutCubic));
            if (canvasGroup != null)
            {
                sequence.Join(canvasGroup.DOFade(0f, duration).SetEase(Ease.InQuad));
            }
            sequence.OnComplete(() => Destroy(gameObject));
        }
    }
}

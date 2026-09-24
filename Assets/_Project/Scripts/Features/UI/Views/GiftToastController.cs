using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI.Views
{
    // View: 1 toast báo quà tặng (icon + tên viewer + tên vật thể trong game), tự huỷ khi xong - không truy cập module khác (SRP).
    public class GiftToastController : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text viewerNameText;
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Outline cardOutline;
        [SerializeField] private float showDuration = 3.5f;
        [SerializeField] private float animDuration = 0.3f;

        // accentColor: mau chu de canh bao/su kien (vd. do = nguy hiem, xanh = tich cuc) - to vien the (cardOutline)
        // va mac dinh cho icon neu iconColor khong truyen rieng. null = giu nguyen mau mac dinh tren template
        // (dung cho khu Gift: moi qua co mau icon rieng, khong can vien doi mau).
        public void Play(string viewerName, string itemName, Sprite icon, Color? iconColor = null, Color? accentColor = null)
        {
            Color? resolvedIconColor = iconColor ?? accentColor;

            // icon null khi chưa có icon quà thật (đang chờ curate) - giữ nguyên icon mặc định trên template thay vì để trống.
            if (iconImage != null && icon != null)
            {
                iconImage.sprite = icon;
                // Icon nguồn là hình trắng/nền trong suốt (game-icons.net) - cần tint màu để có màu sắc phù hợp vật phẩm.
                iconImage.color = resolvedIconColor ?? Color.white;
            }

            if (cardOutline != null && accentColor.HasValue)
            {
                Color c = accentColor.Value;
                cardOutline.effectColor = new Color(c.r, c.g, c.b, cardOutline.effectColor.a);
            }

            if (viewerNameText != null)
            {
                viewerNameText.text = viewerName;
            }

            if (itemNameText != null)
            {
                itemNameText.text = itemName;
            }

            // Dùng scale + fade thay vì di chuyển RectTransform vì toast nằm trong VerticalLayoutGroup
            // (layout tự set anchoredPosition/sizeDelta mỗi frame - animate 2 giá trị đó sẽ bị layout ghi đè).
            transform.localScale = Vector3.one * 0.85f;
            canvasGroup.alpha = 0f;

            DOTween.Kill(transform);

            Sequence sequence = DOTween.Sequence().SetTarget(transform);
            sequence.Append(canvasGroup.DOFade(1f, animDuration));
            sequence.Join(transform.DOScale(1f, animDuration).SetEase(Ease.OutBack));
            sequence.AppendInterval(showDuration);
            sequence.Append(canvasGroup.DOFade(0f, animDuration));
            sequence.Join(transform.DOScale(0.85f, animDuration).SetEase(Ease.InBack));
            sequence.OnComplete(() => Destroy(gameObject));
        }
    }
}

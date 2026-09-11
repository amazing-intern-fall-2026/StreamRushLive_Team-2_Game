using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI.Views
{
    // View: chỉ hiển thị thanh tiến trình 100km, không truy cập module khác (SRP).
    public class ProgressBarController : MonoBehaviour
    {
        [SerializeField] private Image fillBar;
        [SerializeField] private TMP_Text labelKm;

        // currentKm: quãng đường hiện tại (km). GDD: TOTAL_DISTANCE = 100_000m = 100km.
        public void SetProgress(float currentKm)
        {
            float clampedKm = Mathf.Clamp(currentKm, 0f, 100f);

            if (fillBar != null)
            {
                // Resize theo chiều rộng thay vì dùng fillAmount, vì Image kiểu Filled
                // không hỗ trợ 9-slice (2 đầu bo tròn sẽ bị kéo méo).
                var fillRect = fillBar.rectTransform;
                float fullWidth = ((RectTransform)fillRect.parent).rect.width;
                fillRect.sizeDelta = new Vector2(fullWidth * (clampedKm / 100f), fillRect.sizeDelta.y);
            }

            if (labelKm != null)
            {
                labelKm.text = $"{clampedKm:F1} / 100 km";
            }
        }
    }
}

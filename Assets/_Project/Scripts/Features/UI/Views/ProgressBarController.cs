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

        // Avatar Runner truot ngang theo % tien do doc theo thanh - cung co che voi Fan/Anti handle
        // (FactionTugOfWarUI.UpdateHandlePosition), chi khac truc (ngang thay vi doc).
        [SerializeField] private RectTransform avatarHandle;

        // GDD v1.3.1 muc 7: thanh cu ly hien theo met CHANG hien tai (0 -> RelayDistanceMeters,
        // mac dinh 100m), KHONG con hien theo km tong toan chang 100km nhu ban cu. Tang khi chay,
        // giam khi va cham (TrackProgressTracker.ReduceDistance goi lai ham nay voi gia tri moi).
        public void SetLegProgress(float currentMeters, float targetMeters)
        {
            float clampedMeters = Mathf.Clamp(currentMeters, 0f, targetMeters);
            float ratio = targetMeters > 0f ? clampedMeters / targetMeters : 0f;

            if (fillBar != null)
            {
                // Resize theo chieu rong thay vi dung fillAmount, vi Image kieu Filled
                // khong ho tro 9-slice (2 dau bo tron se bi keo meo).
                var fillRect = fillBar.rectTransform;
                float fullWidth = ((RectTransform)fillRect.parent).rect.width;
                fillRect.sizeDelta = new Vector2(fullWidth * ratio, fillRect.sizeDelta.y);

                if (avatarHandle != null)
                {
                    Vector2 pos = avatarHandle.anchoredPosition;
                    pos.x = fullWidth * ratio;
                    avatarHandle.anchoredPosition = pos;
                }
            }

            if (labelKm != null)
            {
                if (targetMeters >= 1000f)
                {
                    float targetKm = targetMeters / 1000f;
                    if (clampedMeters < 1000f)
                    {
                        labelKm.text = $"{clampedMeters:F0}m/{targetKm:F0}km";
                    }
                    else
                    {
                        float currentKm = clampedMeters / 1000f;
                        labelKm.text = $"{currentKm:F2}km/{targetKm:F0}km";
                    }
                }
                else
                {
                    labelKm.text = $"{clampedMeters:F0}m/{targetMeters:F0}m";
                }
            }
        }
    }
}

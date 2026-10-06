using DG.Tweening;
using TMPro;
using UnityEngine;

namespace SteamRush.Features.UI.Views
{
    // View: popup vinh danh hoan thanh cuoc dua (GDD v1.4.1 muc 7). Hien 1 lan khi
    // VictoryCeremonyController goi Show(), o lai tren man hinh vinh vien (khong tu dismiss) vi
    // day la man hinh ket thuc phien live, khong con gi khac dien ra sau do.
    public class VictoryPopupController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private float animDuration = 0.4f;

        // KHONG SetActive(false) trong Awake(): GameObject nay da duoc luu san o trang thai inactive
        // trong scene. Awake() chi chay lan dau object duoc active - neu object bat dau inactive thi
        // Awake() se khong chay luc load scene, ma chi chay dung luc Show() goi SetActive(true) lan
        // dau, khien no tu tat nguoc lai ngay lap tuc va lam Show() vo hieu luc (da xay ra thuc te).

        // Nhan totalDistanceMeters/goalDistanceMeters (met, khong phai km) de dung chung 1 logic
        // format m/km voi ProgressBarController.SetLegProgress - tranh truong hop goal nho (vd luc
        // test) bi lam tron ve "0" do dung {:F0} tren don vi km qua som.
        public void Show(float totalDistanceMeters, float goalDistanceMeters, string elapsedTimeText)
        {
            gameObject.SetActive(true);

            if (statsText != null)
            {
                HudTheme theme = HudTheme.Current;
                string label = ColorUtility.ToHtmlStringRGB(theme.textSecondary);
                string gold = ColorUtility.ToHtmlStringRGB(theme.gold);
                string white = ColorUtility.ToHtmlStringRGB(theme.textPrimary);
                // Dòng trống dùng <size=30%> để khoảng cách gọn - cho phép chữ số liệu to mà panel vẫn nằm trên avatar Runner.
                statsText.text = $"<size=62%><color=#{label}>TOTAL DISTANCE</color></size>\n<size=125%><b><color=#{gold}>{FormatDistance(totalDistanceMeters, goalDistanceMeters)}</color></b></size>\n<size=30%> </size>\n<size=62%><color=#{label}>CLEAR TIME</color></size>\n<size=125%><b><color=#{white}>{elapsedTimeText}</color></b></size>\n<size=30%> </size>\n<size=55%><color=#{gold}>STREAM RUSH LIVE - RUN COMPLETED</color></size>";
            }

            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (panel != null) panel.localScale = Vector3.one * 0.7f;

            DOTween.Kill(transform);
            Sequence sequence = DOTween.Sequence().SetTarget(transform);
            if (canvasGroup != null) sequence.Append(canvasGroup.DOFade(1f, animDuration));
            if (panel != null) sequence.Join(panel.DOScale(1f, animDuration).SetEase(Ease.OutBack));
        }

        private static string FormatDistance(float currentMeters, float targetMeters)
        {
            float clampedMeters = Mathf.Clamp(currentMeters, 0f, targetMeters);

            if (targetMeters >= 1000f)
            {
                float targetKm = targetMeters / 1000f;
                if (clampedMeters < 1000f)
                {
                    return $"{clampedMeters:F0}m / {targetKm:N0} km";
                }
                float currentKm = clampedMeters / 1000f;
                if (currentKm >= targetKm)
                {
                    return $"{targetKm:N0} km";
                }
                return $"{currentKm:F1} km / {targetKm:N0} km";
            }

            return $"{clampedMeters:F0}m / {targetMeters:F0}m";
        }
    }
}

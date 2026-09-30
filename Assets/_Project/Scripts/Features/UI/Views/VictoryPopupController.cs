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
                statsText.text = $"Tổng cự ly: {FormatDistance(totalDistanceMeters, goalDistanceMeters)}\nThời gian hoàn thành: {elapsedTimeText}";
            }

            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (panel != null) panel.localScale = Vector3.one * 0.7f;

            DOTween.Kill(transform);
            Sequence sequence = DOTween.Sequence().SetTarget(transform);
            if (canvasGroup != null) sequence.Append(canvasGroup.DOFade(1f, animDuration));
            if (panel != null) sequence.Join(panel.DOScale(1f, animDuration).SetEase(Ease.OutBack));
        }

        // Dung chung quy uoc voi ProgressBarController.SetLegProgress: duoi 1000m hien theo met,
        // tu 1000m tro len hien theo km - tranh {:F0} lam tron goal nho ve "0" nhu bug da gap.
        private static string FormatDistance(float currentMeters, float targetMeters)
        {
            float clampedMeters = Mathf.Clamp(currentMeters, 0f, targetMeters);

            if (targetMeters >= 1000f)
            {
                float targetKm = targetMeters / 1000f;
                if (clampedMeters < 1000f)
                {
                    return $"{clampedMeters:F0}m / {targetKm:F0}km";
                }
                float currentKm = clampedMeters / 1000f;
                return $"{currentKm:F2}km / {targetKm:F0}km";
            }

            return $"{clampedMeters:F0}m / {targetMeters:F0}m";
        }
    }
}

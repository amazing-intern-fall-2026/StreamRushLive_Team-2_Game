using DG.Tweening;
using TMPro;
using UnityEngine;

namespace SteamRush.Features.UI.Views
{
    // View: celebratory completion popup (GDD v1.4.1 Section 7). Shown once when
    // VictoryCeremonyController calls Show(), remains permanently on screen as session concludes.
    public class VictoryPopupController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private float animDuration = 0.4f;

        // DO NOT call SetActive(false) in Awake(): this GameObject is pre-stored inactive in the scene.
        // Awake() only runs the first time the object becomes active - setting it inactive would disable it immediately.

        // Accepts totalDistanceMeters and goalDistanceMeters (meters) matching ProgressBarController formatting logic.
        public void Show(float totalDistanceMeters, float goalDistanceMeters, string elapsedTimeText)
        {
            gameObject.SetActive(true);

            if (statsText != null)
            {
                HudTheme theme = HudTheme.Current;
                string label = ColorUtility.ToHtmlStringRGB(theme.textSecondary);
                string gold = ColorUtility.ToHtmlStringRGB(theme.gold);
                string white = ColorUtility.ToHtmlStringRGB(theme.textPrimary);
                // Blank spacing line uses <size=30%> to keep layout compact above Runner avatar.
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

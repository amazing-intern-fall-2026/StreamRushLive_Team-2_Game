using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI.Views
{
    // View: displays 100km progress bar adhering to SRP.
    public class ProgressBarController : MonoBehaviour
    {
        [SerializeField] private Image fillBar;
        [SerializeField] private TMP_Text labelKm;

        // Runner avatar handle slides horizontally along progress bar based on percentage
        [SerializeField] private RectTransform avatarHandle;
        [SerializeField] private Image avatarPlaceholder;

        // Updates the avatar displayed on the progress bar.
        public void SetRunnerAvatar(Sprite runnerAvatar)
        {
            if (avatarPlaceholder == null)
            {
                GameObject placeholderObject = GameObject.Find("AvatarPlaceholder");
                if (placeholderObject != null)
                {
                    avatarPlaceholder = placeholderObject.GetComponent<Image>();
                }
            }

            if (avatarPlaceholder == null)
            {
                Debug.LogWarning("[ProgressBar] AvatarPlaceholder is not assigned or found");
                return;
            }

            if (runnerAvatar == null)
            {
                Debug.LogWarning("[ProgressBar] Received NULL avatar");
                return;
            }

            Debug.Log($"[ProgressBar] Updating AvatarPlaceholder with: {runnerAvatar.name}");

            avatarPlaceholder.sprite = runnerAvatar;
            avatarPlaceholder.enabled = true;
        }

        // Displays current leg distance progress in meters (0 -> RelayDistanceMeters)
        public void SetLegProgress(float currentMeters, float targetMeters)
        {
            float clampedMeters = Mathf.Clamp(currentMeters, 0f, targetMeters);
            float ratio = targetMeters > 0f ? clampedMeters / targetMeters : 0f;

            if (fillBar != null)
            {
                // Resize width instead of fillAmount to preserve 9-slice rounded corners
                var fillRect = fillBar.rectTransform;
                float fullWidth = ((RectTransform)fillRect.parent).rect.width;
                fillRect.sizeDelta = new Vector2(
                    fullWidth * ratio,
                    fillRect.sizeDelta.y
                );

                if (avatarHandle != null)
                {
                    Vector2 pos = avatarHandle.anchoredPosition;
                    pos.x = fullWidth * ratio;
                    avatarHandle.anchoredPosition = pos;
                }
            }

            if (labelKm != null)
            {
                if (targetMeters >= 900000000f)
                {
                    if (clampedMeters < 1000f)
                        labelKm.text = $"{clampedMeters:F0}m / ∞";
                    else
                        labelKm.text = $"{clampedMeters / 1000f:F2}km / ∞";
                }
                else if (targetMeters >= 1000f)
                {
                    float targetKm = targetMeters / 1000f;
                    string targetKmStr = targetKm.ToString(
                        "0.##",
                        System.Globalization.CultureInfo.InvariantCulture
                    );

                    if (clampedMeters < 1000f)
                    {
                        labelKm.text = $"{clampedMeters:F0}m/{targetKmStr}km";
                    }
                    else
                    {
                        float currentKm = clampedMeters / 1000f;
                        labelKm.text = $"{currentKm:F2}km/{targetKmStr}km";
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

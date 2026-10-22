using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamRush.Features.Runner;

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
        [SerializeField] private Sprite defaultAvatar;
        private Sprite _initialAvatar;

        private void Awake()
        {
            EnsureReferences();
        }

        private void Start()
        {
            EnsureReferences();

            // Synchronize initial runner avatar on start if QueueManager is already running.
            var queueManager = ChatRunnerQueueManager.Instance;
            if (queueManager != null && queueManager.CurrentRunnerAvatar != null)
            {
                SetRunnerAvatar(queueManager.CurrentRunnerAvatar);
            }
        }

        private void OnValidate()
        {
            EnsureReferences();
        }

        // Ensures all UI references are resolved automatically if not assigned in Inspector.
        public void EnsureReferences()
        {
            if (avatarHandle == null)
            {
                Transform h = transform.Find("AvatarHandle");
                if (h != null)
                {
                    avatarHandle = h.GetComponent<RectTransform>();
                }
            }

            if (avatarPlaceholder == null)
            {
                // Find avatar image in AvatarHandle hierarchy.
                Transform imgTr = transform.Find("AvatarHandle/AvatarMask/AvatarImage") ??
                                  transform.Find("AvatarHandle/AvatarMask/AvatarPlaceholder") ??
                                  transform.Find("AvatarHandle/AvatarImage") ??
                                  transform.Find("AvatarHandle/AvatarPlaceholder") ??
                                  transform.Find("AvatarImage") ??
                                  transform.Find("AvatarPlaceholder");

                if (imgTr != null)
                {
                    avatarPlaceholder = imgTr.GetComponent<Image>();
                }
                else if (avatarHandle != null)
                {
                    // Deep search in AvatarHandle children for avatar image component.
                    var mask = avatarHandle.GetComponentInChildren<Mask>(true);
                    if (mask != null)
                    {
                        foreach (Transform child in mask.transform)
                        {
                            var childImg = child.GetComponent<Image>();
                            if (childImg != null)
                            {
                                avatarPlaceholder = childImg;
                                break;
                            }
                        }
                    }

                    if (avatarPlaceholder == null)
                    {
                        var images = avatarHandle.GetComponentsInChildren<Image>(true);
                        foreach (var img in images)
                        {
                            string n = img.gameObject.name.ToLower();
                            if (n.Contains("avatar") || n.Contains("image") || n.Contains("placeholder"))
                            {
                                avatarPlaceholder = img;
                                break;
                            }
                        }
                    }
                }
            }

            if (fillBar == null)
            {
                Transform f = transform.Find("Fill");
                if (f != null)
                {
                    fillBar = f.GetComponent<Image>();
                }
            }

            if (labelKm == null)
            {
                labelKm = GetComponentInChildren<TMP_Text>();
            }

            // Cache default/initial placeholder sprite.
            if (avatarPlaceholder != null && avatarPlaceholder.sprite != null && _initialAvatar == null)
            {
                _initialAvatar = avatarPlaceholder.sprite;
            }
        }

        // Updates the avatar displayed on the progress bar handle.
        public void SetRunnerAvatar(Sprite runnerAvatar)
        {
            EnsureReferences();

            if (avatarPlaceholder == null)
            {
                Debug.LogWarning("[ProgressBar] AvatarPlaceholder/AvatarImage component is not assigned or found in children.");
                return;
            }

            // If runner avatar is provided, display it; otherwise fallback to default avatar so previous runner's avatar is replaced.
            Sprite displaySprite = runnerAvatar != null 
                ? runnerAvatar 
                : (defaultAvatar != null ? defaultAvatar : _initialAvatar);

            if (displaySprite != null)
            {
                avatarPlaceholder.sprite = displaySprite;
                avatarPlaceholder.enabled = true;
            }
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

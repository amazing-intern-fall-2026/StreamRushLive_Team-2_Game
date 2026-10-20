using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamRush.Features.UI.PreGameConfig;

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

        [Header("Reset / Replay Button")]
        [SerializeField] private Button btnResetGame;
        [SerializeField] private Image btnIconImage;
        [SerializeField] private Sprite replayIcon;

        private bool _isShowing;

        private void Awake()
        {
            EnsureResetButton();
        }

        private void Start()
        {
            // If the popup started active in the scene before Show() was called, hide it until victory!
            if (!_isShowing)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            EnsureResetButton();
        }

        public void EnsureResetButton()
        {
            if (panel == null)
            {
                panel = transform.Find("Panel") as RectTransform;
            }

            if (panel != null && panel.sizeDelta.y < 510f)
            {
                panel.sizeDelta = new Vector2(panel.sizeDelta.x, 510f);
            }

            if (panel != null)
            {
                var existingBtn = panel.Find("BtnResetGame");
                if (existingBtn != null)
                {
                    btnResetGame = existingBtn.GetComponent<Button>();
                }
                else
                {
                    var btnGo = new GameObject("BtnResetGame", typeof(RectTransform), typeof(Image), typeof(Button));
                    btnGo.transform.SetParent(panel, false);
                    btnResetGame = btnGo.GetComponent<Button>();
                }

                if (btnResetGame != null)
                {
                    var rt = btnResetGame.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0f, 48f);
                    rt.sizeDelta = new Vector2(68f, 68f);

                    var img = btnResetGame.GetComponent<Image>();
                    var theme = HudTheme.Current;
                    if (theme != null && theme.pillGold != null)
                    {
                        img.sprite = theme.pillGold;
                        img.type = Image.Type.Sliced;
                    }
                    img.color = Color.white;

                    // Remove text if any exists on the button
                    var oldText = btnResetGame.GetComponentInChildren<TMP_Text>(true);
                    if (oldText != null)
                    {
                        if (Application.isPlaying) Destroy(oldText.gameObject);
                        else DestroyImmediate(oldText.gameObject);
                    }

                    // Setup Icon child
                    var iconTr = btnResetGame.transform.Find("Icon");
                    GameObject iconGo;
                    if (iconTr == null)
                    {
                        iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                        iconGo.transform.SetParent(btnResetGame.transform, false);
                    }
                    else
                    {
                        iconGo = iconTr.gameObject;
                    }

                    var iconRt = iconGo.GetComponent<RectTransform>();
                    iconRt.anchorMin = Vector2.zero;
                    iconRt.anchorMax = Vector2.one;
                    iconRt.offsetMin = new Vector2(14f, 14f);
                    iconRt.offsetMax = new Vector2(-14f, -14f);

                    btnIconImage = iconGo.GetComponent<Image>();
                    if (replayIcon == null)
                    {
                        replayIcon = Resources.Load<Sprite>("Icons/icon_replay");
                    }
                    if (replayIcon != null)
                    {
                        btnIconImage.sprite = replayIcon;
                    }
                    btnIconImage.color = new Color32(0x1A, 0x1A, 0x1A, 0xFF); // sleek dark charcoal on gold
                    btnIconImage.preserveAspect = true;

                    btnResetGame.onClick.RemoveListener(OnResetButtonClicked);
                    btnResetGame.onClick.AddListener(OnResetButtonClicked);
                }
            }
        }

        private void OnResetButtonClicked()
        {
            AudioManager.Instance?.PlaySFX(SFXType.UIButtonClick);
            if (ConfirmationModal.IsShowing) return;

            // Keep the victory popup visible if the streamer cancels.
            ConfirmationModal.Show(
                ConfirmationModal.T("Bắt đầu lại?", "Restart the game?"),
                ConfirmationModal.T(
                    "Bắt đầu lại sẽ làm mới tiến trình màn chơi từ vạch xuất phát. Bạn có muốn tiếp tục?",
                    "Restarting will reset the run from the starting line. Do you want to continue?"),
                ConfirmationModal.T("XÁC NHẬN / RESET", "CONFIRM / RESET"),
                ConfirmationModal.T("HỦY", "CANCEL"),
                onConfirm: () =>
                {
                    _isShowing = false;
                    gameObject.SetActive(false);
                    PreGameConfigManager.ResetGameAndOpenSettings();
                });
        }

        // Accepts totalDistanceMeters and goalDistanceMeters (meters) matching ProgressBarController formatting logic.
        public void Show(float totalDistanceMeters, float goalDistanceMeters, string elapsedTimeText)
        {
            _isShowing = true;
            EnsureResetButton();
            gameObject.SetActive(true);

            if (statsText != null)
            {
                HudTheme theme = HudTheme.Current;
                string label = ColorUtility.ToHtmlStringRGB(theme.textSecondary);
                string gold = ColorUtility.ToHtmlStringRGB(theme.gold);
                string white = ColorUtility.ToHtmlStringRGB(theme.textPrimary);

                var loc = SteamRush.Core.Localization.LocalizationManager.Instance;
                string totalDistLbl = loc != null ? loc.Get("VIC_TOTAL_DIST", "TOTAL DISTANCE") : "TOTAL DISTANCE";
                string clearTimeLbl = loc != null ? loc.Get("VIC_CLEAR_TIME", "CLEAR TIME") : "CLEAR TIME";
                string footerLbl = loc != null ? loc.Get("VIC_FOOTER", "STREAM RUSH LIVE - RUN COMPLETED") : "STREAM RUSH LIVE - RUN COMPLETED";

                var title = transform.Find("Panel/TitleText")?.GetComponent<TMP_Text>();
                if (title != null && loc != null) title.text = loc.Get("VIC_TITLE", title.text);

                var subtitle = transform.Find("Panel/SubtitleText")?.GetComponent<TMP_Text>();
                if (subtitle != null && loc != null) subtitle.text = loc.Get("VIC_SUBTITLE", subtitle.text);

                // Blank spacing line uses <size=30%> to keep layout compact above Runner avatar.
                statsText.text = $"<size=62%><color=#{label}>{totalDistLbl}</color></size>\n<size=125%><b><color=#{gold}>{FormatDistance(totalDistanceMeters, goalDistanceMeters)}</color></b></size>\n<size=30%> </size>\n<size=62%><color=#{label}>{clearTimeLbl}</color></size>\n<size=125%><b><color=#{white}>{elapsedTimeText}</color></b></size>\n<size=30%> </size>\n<size=55%><color=#{gold}>{footerLbl}</color></size>";
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
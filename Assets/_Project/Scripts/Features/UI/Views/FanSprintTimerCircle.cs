using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamRush.Features.Runner;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Vòng tròn đếm ngược thời gian tác dụng của quà 'Bình Tăng Tốc (Sprint Buff)' (F2)
    /// nằm đối xứng phía bên thanh năng lượng Fan (Phe Xanh).
    /// Có hiệu ứng Radial Fill 360 độ quét dần theo thời gian thực và đếm ngược số giây (30s).
    /// </summary>
    public class FanSprintTimerCircle : MonoBehaviour
    {
        private static FanSprintTimerCircle _instance;
        public static FanSprintTimerCircle Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<FanSprintTimerCircle>(FindObjectsInactive.Include);
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [Header("References")]
        [SerializeField] private ChatLaneRunnerController runnerController;
        [SerializeField] private RectTransform containerRect;
        [SerializeField] private Image bgCircleImage;
        [SerializeField] private Image radialFillRing;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text iconText;
        [SerializeField] private CanvasGroup canvasGroup;

        // Sprite vong + mau nhan lay tu HudTheme (HudTheme.GetTimerSkin) - khong con tu ve sprite/mau o day.
        private float _totalDuration = 30f;
        private float _remainingTime = 0f;
        private bool _isActive = false;

        public bool IsActive => _isActive;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (runnerController == null)
            {
                runnerController = FindFirstObjectByType<ChatLaneRunnerController>();
            }

            BuildUIIfMissing();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Start()
        {
            // Mặc định ẩn khi chưa kích hoạt Sprint Buff
            if (!_isActive && canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private void Update()
        {
            if (runnerController == null)
            {
                runnerController = FindFirstObjectByType<ChatLaneRunnerController>();
            }

            // Theo dõi trạng thái từ ChatLaneRunnerController
            if (runnerController != null)
            {
                if (runnerController.IsSprintBuffActive && !_isActive)
                {
                    ActivateTimer(30f);
                }
                else if (!runnerController.IsSprintBuffActive && _isActive)
                {
                    DeactivateTimer();
                }
            }

            if (!_isActive) return;

            _remainingTime -= Time.deltaTime;
            if (_remainingTime <= 0f)
            {
                _remainingTime = 0f;
                DeactivateTimer();
                return;
            }

            // Cập nhật Radial Fill Ring (0..1)
            if (radialFillRing != null && _totalDuration > 0f)
            {
                radialFillRing.fillAmount = Mathf.Clamp01(_remainingTime / _totalDuration);
            }

            // Cập nhật số giây còn lại
            if (timerText != null)
            {
                timerText.text = $"{Mathf.CeilToInt(_remainingTime)}s";
            }
        }

        /// <summary>
        /// Kích hoạt vòng tròn đếm ngược
        /// </summary>
        public void ActivateTimer(float duration)
        {
            if (this == null) return;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            _totalDuration = duration > 0 ? duration : 30f;
            _remainingTime = _totalDuration;
            _isActive = true;

            BuildUIIfMissing();

            if (containerRect != null)
            {
                DOTween.Kill(containerRect);
                containerRect.localScale = Vector3.one;
            }

            if (canvasGroup != null)
            {
                DOTween.Kill(canvasGroup);
                canvasGroup.alpha = 1f;
            }

            TimerCircleVerticalStackManager.RegisterFanCircle(this, containerRect);
        }

        /// <summary>
        /// Tắt vòng tròn đếm ngược
        /// </summary>
        public void DeactivateTimer()
        {
            if (this == null) return;
            _isActive = false;

            TimerCircleVerticalStackManager.UnregisterFanCircle(this);

            if (containerRect != null)
            {
                DOTween.Kill(containerRect);
                containerRect.localScale = Vector3.one;
            }

            if (canvasGroup != null)
            {
                DOTween.Kill(canvasGroup);
                canvasGroup.DOFade(0f, 0.2f);
            }
        }

        private void OnDisable()
        {
            TimerCircleVerticalStackManager.UnregisterFanCircle(this);
        }

        [ContextMenu("Rebuild UI")]
        public void RebuildUI()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            containerRect = null;
            bgCircleImage = null;
            radialFillRing = null;
            timerText = null;
            iconText = null;
            BuildUIIfMissing();
        }

        private void BuildUIIfMissing()
        {
            if (this == null) return;
            if (containerRect != null && radialFillRing != null && timerText != null) return;

            Transform parentTransform = null;
            var factionUI = FindFirstObjectByType<FactionTugOfWarUI>();
            if (factionUI != null)
            {
                parentTransform = factionUI.transform;
            }
            else
            {
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null) parentTransform = canvas.transform;
            }

            if (parentTransform != null && transform.parent != parentTransform)
            {
                transform.SetParent(parentTransform, false);
            }

            containerRect = GetComponent<RectTransform>();
            if (containerRect == null) containerRect = gameObject.AddComponent<RectTransform>();

            containerRect.anchorMin = new Vector2(0f, 0.80f);
            containerRect.anchorMax = new Vector2(0f, 0.80f);
            containerRect.pivot = new Vector2(0f, 0.5f);
            containerRect.anchoredPosition = new Vector2(52f, 0f);
            containerRect.sizeDelta = new Vector2(116f, 116f);

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            TimerCircleSkin skin = HudTheme.Current.GetTimerSkin(TimerCircleKind.FanSprint);

            GameObject bgObj = new GameObject("Circle_Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgObj.transform.SetParent(containerRect, false);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgCircleImage = bgObj.GetComponent<Image>();
            bgCircleImage.sprite = skin.disc;
            bgCircleImage.color = skin.bgTint;

            GameObject ringObj = new GameObject("Circle_RadialFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ringObj.transform.SetParent(containerRect, false);
            RectTransform ringRect = ringObj.GetComponent<RectTransform>();
            ringRect.anchorMin = Vector2.zero;
            ringRect.anchorMax = Vector2.one;
            ringRect.sizeDelta = Vector2.zero;
            radialFillRing = ringObj.GetComponent<Image>();
            radialFillRing.sprite = skin.ring;
            radialFillRing.type = Image.Type.Filled;
            radialFillRing.fillMethod = Image.FillMethod.Radial360;
            radialFillRing.fillOrigin = (int)Image.Origin360.Top;
            radialFillRing.fillClockwise = true;
            radialFillRing.fillAmount = 1f;
            radialFillRing.color = Color.white;

            GameObject innerHole = new GameObject("Circle_Inner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            innerHole.transform.SetParent(containerRect, false);
            RectTransform innerRect = innerHole.GetComponent<RectTransform>();
            innerRect.anchorMin = new Vector2(0.5f, 0.5f);
            innerRect.anchorMax = new Vector2(0.5f, 0.5f);
            innerRect.pivot = new Vector2(0.5f, 0.5f);
            innerRect.sizeDelta = new Vector2(88f, 88f);
            Image innerImg = innerHole.GetComponent<Image>();
            innerImg.sprite = skin.disc;
            innerImg.color = skin.innerTint;

            GameObject iconObj = new GameObject("Label_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            iconObj.transform.SetParent(innerHole.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 16f);
            iconRect.sizeDelta = new Vector2(80f, 22f);
            iconText = iconObj.GetComponent<TextMeshProUGUI>();
            iconText.text = "SPEED";
            iconText.fontSize = 13;
            iconText.fontStyle = FontStyles.Bold;
            iconText.color = skin.label;
            iconText.alignment = TextAlignmentOptions.Center;
            if (skin.font != null) iconText.font = skin.font;

            GameObject textObj = new GameObject("Timer_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(innerHole.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0f, -12f);
            textRect.sizeDelta = new Vector2(80f, 32f);
            timerText = textObj.GetComponent<TextMeshProUGUI>();
            timerText.text = "30s";
            timerText.fontSize = 26;
            timerText.fontStyle = FontStyles.Bold;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.color = Color.white;
            if (skin.font != null) timerText.font = skin.font;
        }
    }
}

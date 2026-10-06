using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StreamRushLive.Features.Spawning;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Vòng tròn đếm ngược thời gian tác dụng của quà 'Khiên Bảo Vệ' (F1 / Donate Shield)
    /// nằm thẳng hàng phía dưới vòng tròn 'Tăng Tốc (Sprint Buff)' bên phe Fan.
    /// Có hiệu ứng Radial Fill 360 độ quét dần theo thời gian thực và đếm ngược 15 giây.
    /// </summary>
    public class ShieldTimerCircle : MonoBehaviour
    {
        private static ShieldTimerCircle _instance;
        public static ShieldTimerCircle Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ShieldTimerCircle>(FindObjectsInactive.Include);
                }
                if (_instance == null)
                {
                    GameObject go = new GameObject("ShieldTimerCircle");
                    _instance = go.AddComponent<ShieldTimerCircle>();
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
        [SerializeField] private RunnerItemEffects runnerItemEffects;
        [SerializeField] private RectTransform containerRect;
        [SerializeField] private Image bgCircleImage;
        [SerializeField] private Image radialFillRing;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text iconText;
        [SerializeField] private CanvasGroup canvasGroup;

        // Sprite vong + mau nhan lay tu HudTheme (HudTheme.GetTimerSkin) - khong con tu ve sprite/mau o day.

        private float _totalDuration = 15f;
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

            if (runnerItemEffects == null)
            {
                runnerItemEffects = FindFirstObjectByType<RunnerItemEffects>();
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
            // Mặc định ẩn khi chưa kích hoạt Shield
            if (!_isActive && canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private void Update()
        {
            if (runnerItemEffects == null)
            {
                runnerItemEffects = FindFirstObjectByType<RunnerItemEffects>();
            }

            // Đồng bộ trạng thái với RunnerItemEffects
            if (runnerItemEffects != null)
            {
                if (runnerItemEffects.IsShieldActive && !_isActive)
                {
                    ActivateTimer(15f);
                }
                else if (!runnerItemEffects.IsShieldActive && _isActive)
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
        /// Kích hoạt vòng tròn đếm ngược khiên (mặc định 15s)
        /// </summary>
        public void ActivateTimer(float duration = 15f)
        {
            if (this == null) return;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            _totalDuration = duration > 0 ? duration : 15f;
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
        /// Tắt vòng tròn đếm ngược khiên (hết giờ hoặc đã chặn va chạm)
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

            // Định vị bên dưới FanSprintTimerCircle (Y=95.6f), khoảng cách ~128px
            containerRect.anchorMin = new Vector2(0f, 0.80f);
            containerRect.anchorMax = new Vector2(0f, 0.80f);
            containerRect.pivot = new Vector2(0f, 0.5f);
            containerRect.anchoredPosition = new Vector2(53f, -32f);
            containerRect.sizeDelta = new Vector2(116f, 116f);

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            TimerCircleSkin skin = HudTheme.Current.GetTimerSkin(TimerCircleKind.Shield);

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
            iconText.text = "SHIELD";
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
            timerText.text = "15s";
            timerText.fontSize = 26;
            timerText.fontStyle = FontStyles.Bold;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.color = Color.white;
            if (skin.font != null) timerText.font = skin.font;
        }

    }
}

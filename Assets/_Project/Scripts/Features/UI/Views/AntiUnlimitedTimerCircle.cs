using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StreamRushLive.Features.Spawning;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Vòng tròn đếm ngược thời gian tác dụng của 'Thả Xe Không Giới Hạn' (F7)
    /// nằm ngay phía trên thanh năng lượng Anti (Phe Đỏ).
    /// Có hiệu ứng Radial Fill 360 độ quét dần theo thời gian thực và đếm ngược số giây.
    /// </summary>
    public class AntiUnlimitedTimerCircle : MonoBehaviour
    {
        private static AntiUnlimitedTimerCircle _instance;
        public static AntiUnlimitedTimerCircle Instance => _instance;

        [Header("References")]
        [SerializeField] private SingleObstacleSpawner obstacleSpawner;
        [SerializeField] private RectTransform containerRect;
        [SerializeField] private Image bgCircleImage;
        [SerializeField] private Image radialFillRing;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text iconText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Colors & Timing")]
        [SerializeField] private Color activeRingColor = new Color(1f, 0.2f, 0.1f, 1f);
        [SerializeField] private Color warningRingColor = new Color(1f, 0.8f, 0.1f, 1f);
        [SerializeField] private Color bgColor = new Color(0.08f, 0.04f, 0.06f, 0.92f);

        private float _totalDuration = 60f;
        private float _remainingTime = 0f;
        private bool _isActive = false;
        private Tween _pulseTween;

        private static Sprite _circleSprite;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (obstacleSpawner == null)
            {
                obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
            }

            BuildUIIfMissing();
        }

        private void Start()
        {
            // Mặc định ẩn khi chưa kích hoạt Unlimited Mode
            if (!_isActive && canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private void Update()
        {
            // Theo dõi trạng thái từ SingleObstacleSpawner
            if (obstacleSpawner != null)
            {
                if (obstacleSpawner.IsUnlimitedModeActive && !_isActive)
                {
                    ActivateTimer(60f);
                }
                else if (!obstacleSpawner.IsUnlimitedModeActive && _isActive)
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
                radialFillRing.color = _remainingTime <= 10f ? warningRingColor : activeRingColor;
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
            _totalDuration = duration > 0 ? duration : 60f;
            _remainingTime = _totalDuration;
            _isActive = true;

            BuildUIIfMissing();

            if (canvasGroup != null)
            {
                DOTween.Kill(canvasGroup);
                DOTween.Kill(containerRect);

                containerRect.localScale = Vector3.one * 0.5f;
                canvasGroup.DOFade(1f, 0.3f);
                containerRect.DOScale(1f, 0.35f).SetEase(Ease.OutBack);
            }

            // Hiệu ứng nhịp đập cảnh báo
            StartPulseAnimation();
        }

        /// <summary>
        /// Tắt vòng tròn đếm ngược
        /// </summary>
        public void DeactivateTimer()
        {
            _isActive = false;

            if (_pulseTween != null)
            {
                _pulseTween.Kill();
                _pulseTween = null;
            }

            if (canvasGroup != null)
            {
                DOTween.Kill(canvasGroup);
                DOTween.Kill(containerRect);

                containerRect.DOScale(0.6f, 0.25f).SetEase(Ease.InBack);
                canvasGroup.DOFade(0f, 0.25f);
            }
        }

        private void StartPulseAnimation()
        {
            if (_pulseTween != null) _pulseTween.Kill();

            if (containerRect != null)
            {
                _pulseTween = containerRect.DOScale(1.06f, 0.6f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
        }

        private void BuildUIIfMissing()
        {
            if (containerRect != null) return;

            // 1. Tìm FactionTugOfWarUI hoặc Canvas để đặt vị trí chính xác trên đầu thanh Anti
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

            if (parentTransform == null) return;

            transform.SetParent(parentTransform, false);

            containerRect = GetComponent<RectTransform>();
            if (containerRect == null) containerRect = gameObject.AddComponent<RectTransform>();

            // Vị trí: Đặt ngay góc trên bên phải, phía trên đỉnh thanh năng lượng Anti (X: -42, Y đỉnh thanh Anti)
            containerRect.anchorMin = new Vector2(1f, 0.68f);
            containerRect.anchorMax = new Vector2(1f, 0.68f);
            containerRect.pivot = new Vector2(1f, 0.5f);
            containerRect.anchoredPosition = new Vector2(-16f, 0f);
            containerRect.sizeDelta = new Vector2(76f, 76f);

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            Sprite circleSp = GetOrCreateCircleSprite();

            // 2. Background Circle
            GameObject bgObj = new GameObject("Circle_Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgObj.transform.SetParent(containerRect, false);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgCircleImage = bgObj.GetComponent<Image>();
            bgCircleImage.sprite = circleSp;
            bgCircleImage.color = bgColor;

            // 3. Radial Fill Ring (Quét 360 độ từ đỉnh theo chiều kim đồng hồ)
            GameObject ringObj = new GameObject("Circle_RadialFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ringObj.transform.SetParent(containerRect, false);
            RectTransform ringRect = ringObj.GetComponent<RectTransform>();
            ringRect.anchorMin = Vector2.zero;
            ringRect.anchorMax = Vector2.one;
            ringRect.sizeDelta = Vector2.zero;
            radialFillRing = ringObj.GetComponent<Image>();
            radialFillRing.sprite = circleSp;
            radialFillRing.type = Image.Type.Filled;
            radialFillRing.fillMethod = Image.FillMethod.Radial360;
            radialFillRing.fillOrigin = (int)Image.Origin360.Top;
            radialFillRing.fillClockwise = true;
            radialFillRing.fillAmount = 1f;
            radialFillRing.color = activeRingColor;

            // 4. Center Inner Mask/Hole (tạo hình vành khuyên tròn hiện đại)
            GameObject innerHole = new GameObject("Circle_Inner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            innerHole.transform.SetParent(containerRect, false);
            RectTransform innerRect = innerHole.GetComponent<RectTransform>();
            innerRect.anchorMin = new Vector2(0.5f, 0.5f);
            innerRect.anchorMax = new Vector2(0.5f, 0.5f);
            innerRect.pivot = new Vector2(0.5f, 0.5f);
            innerRect.sizeDelta = new Vector2(58f, 58f);
            Image innerImg = innerHole.GetComponent<Image>();
            innerImg.sprite = circleSp;
            innerImg.color = new Color(0.12f, 0.04f, 0.06f, 0.98f);

            // 5. Icon Text (🚨 hoặc 🚗)
            GameObject iconObj = new GameObject("Icon_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            iconObj.transform.SetParent(innerHole.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.65f);
            iconRect.anchorMax = new Vector2(0.5f, 0.65f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(40f, 24f);
            iconText = iconObj.GetComponent<TextMeshProUGUI>();
            iconText.text = "🚨";
            iconText.fontSize = 16;
            iconText.alignment = TextAlignmentOptions.Center;

            // 6. Countdown Timer Text (60s, 59s...)
            GameObject textObj = new GameObject("Timer_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(innerHole.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.32f);
            textRect.anchorMax = new Vector2(0.5f, 0.32f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(54f, 22f);
            timerText = textObj.GetComponent<TextMeshProUGUI>();
            timerText.text = "60s";
            timerText.fontSize = 14;
            timerText.fontStyle = FontStyles.Bold;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.color = Color.white;
        }

        private static Sprite GetOrCreateCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;

            int res = 128;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float radius = (res - 2) * 0.5f;
            Vector2 center = new Vector2(res * 0.5f, res * 0.5f);

            Color[] colors = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius - dist + 1f);
                    colors[y * res + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();

            _circleSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
            return _circleSprite;
        }
    }
}

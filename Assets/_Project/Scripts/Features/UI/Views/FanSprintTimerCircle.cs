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
            _circleSprite = null;
        }

        [Header("References")]
        [SerializeField] private ChatLaneRunnerController runnerController;
        [SerializeField] private RectTransform containerRect;
        [SerializeField] private Image bgCircleImage;
        [SerializeField] private Image radialFillRing;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text iconText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Colors & Timing")]
        [SerializeField] private Color activeRingColor = new Color(0.22f, 0.74f, 1f, 1f); // Electric Blue/Cyan
        [SerializeField] private Color bgColor = new Color(0.04f, 0.08f, 0.16f, 0.92f); // Deep Navy Blue
        [SerializeField] private Color innerColor = new Color(0.03f, 0.06f, 0.12f, 0.98f);

        private float _totalDuration = 30f;
        private float _remainingTime = 0f;
        private bool _isActive = false;

        private static Sprite _circleSprite;

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
                radialFillRing.color = activeRingColor;
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
        }

        /// <summary>
        /// Tắt vòng tròn đếm ngược
        /// </summary>
        public void DeactivateTimer()
        {
            if (this == null) return;
            _isActive = false;

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

            // 1. Tìm FactionTugOfWarUI hoặc Canvas để đặt vị trí đối xứng bên thân thanh Fan
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

            // Vị trí: Đặt bên phải cạnh thân thanh năng lượng Fan (đối xứng hoàn hảo với AntiUnlimitedTimerCircle)
            // Kích thước 116x116 tối ưu cho màn hình dọc (1080x1920)
            containerRect.anchorMin = new Vector2(0f, 0.80f);
            containerRect.anchorMax = new Vector2(0f, 0.80f);
            containerRect.pivot = new Vector2(0f, 0.5f);
            containerRect.anchoredPosition = new Vector2(52f, 0f);
            containerRect.sizeDelta = new Vector2(116f, 116f);

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            Sprite circleSp = GetOrCreateCircleSprite();

            // 2. Background Circle (116x116)
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

            // 4. Center Inner Mask/Hole (vành khuyên tròn 88x88 -> độ dày viền ring 14px)
            GameObject innerHole = new GameObject("Circle_Inner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            innerHole.transform.SetParent(containerRect, false);
            RectTransform innerRect = innerHole.GetComponent<RectTransform>();
            innerRect.anchorMin = new Vector2(0.5f, 0.5f);
            innerRect.anchorMax = new Vector2(0.5f, 0.5f);
            innerRect.pivot = new Vector2(0.5f, 0.5f);
            innerRect.sizeDelta = new Vector2(88f, 88f);
            Image innerImg = innerHole.GetComponent<Image>();
            innerImg.sprite = circleSp;
            innerImg.color = innerColor;

            // 5. Label Text ("TĂNG TỐC")
            GameObject iconObj = new GameObject("Label_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            iconObj.transform.SetParent(innerHole.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 16f);
            iconRect.sizeDelta = new Vector2(80f, 22f);
            iconText = iconObj.GetComponent<TextMeshProUGUI>();
            iconText.text = "TĂNG TỐC";
            iconText.fontSize = 13;
            iconText.fontStyle = FontStyles.Bold;
            iconText.color = activeRingColor;
            iconText.alignment = TextAlignmentOptions.Center;

            // 6. Countdown Timer Text (30s, 29s... to rõ cho màn hình dọc)
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
        }

        private static Sprite GetOrCreateCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;

            int res = 256;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float radius = (res - 4) * 0.5f;
            Vector2 center = new Vector2(res * 0.5f, res * 0.5f);

            Color[] colors = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius - dist + 1.5f);
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

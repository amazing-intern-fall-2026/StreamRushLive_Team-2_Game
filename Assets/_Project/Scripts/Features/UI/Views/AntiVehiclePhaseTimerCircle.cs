using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StreamRushLive.Features.Spawning;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Vòng tròn đếm ngược Giai đoạn Xe Bán Tải / Xe Tải Hạng Nặng.
    /// Theo dõi trực tiếp trạng thái từ SingleObstacleSpawner.
    /// </summary>
    public class AntiVehiclePhaseTimerCircle : MonoBehaviour
    {
        private static AntiVehiclePhaseTimerCircle _instance;

        public static AntiVehiclePhaseTimerCircle Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<AntiVehiclePhaseTimerCircle>(
                        FindObjectsInactive.Include);
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
        [SerializeField] private SingleObstacleSpawner obstacleSpawner;
        [SerializeField] private RectTransform containerRect;
        [SerializeField] private Image bgCircleImage;
        [SerializeField] private Image radialFillRing;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text iconText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Position")]
        [SerializeField] private Vector2 anchoredPosition = new Vector2(-52f, -120f);
        [SerializeField] private Vector2 circleSize = new Vector2(116f, 116f);

        [Header("Colors & Timing")]
        [SerializeField] private Color activeRingColor = new Color(1f, 0.2f, 0.1f, 1f);
        [SerializeField] private Color bgColor = new Color(0.08f, 0.04f, 0.06f, 0.92f);
        [SerializeField] private Color innerColor = new Color(0.10f, 0.03f, 0.05f, 0.98f);

        private float _totalDuration = 60f;
        private float _remainingTime = 0f;
        private bool _isActive = false;

        private VehicleTier? _displayedPhase;

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

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Start()
        {
            if (!_isActive && canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private void Update()
        {
            if (obstacleSpawner == null)
            {
                obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
            }

            if (obstacleSpawner == null)
            {
                return;
            }

            VehicleTier? currentPhase = obstacleSpawner.ActiveVehiclePhase;

            // Không có Phase
            if (!currentPhase.HasValue)
            {
                if (_isActive)
                {
                    DeactivateTimer();
                }

                return;
            }

            // Phase mới được kích hoạt hoặc chuyển từ Pickup -> Heavy / Heavy -> Pickup
            if (!_isActive || _displayedPhase != currentPhase.Value)
            {
                ActivateTimer(
                    currentPhase.Value,
                    obstacleSpawner.VehiclePhaseDuration);
            }

            // Lấy thời gian trực tiếp từ SingleObstacleSpawner
            _remainingTime = obstacleSpawner.VehiclePhaseRemainingTime;

            if (_totalDuration > 0f && radialFillRing != null)
            {
                radialFillRing.fillAmount =
                    Mathf.Clamp01(_remainingTime / _totalDuration);

                radialFillRing.color = activeRingColor;
            }

            if (timerText != null)
            {
                timerText.text = $"{Mathf.CeilToInt(_remainingTime)}s";
            }
        }

        /// <summary>
        /// Hiện timer cho Phase Pickup hoặc Heavy.
        /// </summary>
        public void ActivateTimer(VehicleTier phase, float duration)
        {
            _displayedPhase = phase;
            _totalDuration = duration > 0f ? duration : 60f;
            _remainingTime = _totalDuration;
            _isActive = true;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

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

            UpdatePhaseLabel(phase);

            if (radialFillRing != null)
            {
                radialFillRing.fillAmount = 1f;
                radialFillRing.color = activeRingColor;
            }

            if (timerText != null)
            {
                timerText.text = $"{Mathf.CeilToInt(_remainingTime)}s";
            }
        }

        /// <summary>
        /// Ẩn timer khi Phase kết thúc.
        /// </summary>
        public void DeactivateTimer()
        {
            _isActive = false;
            _displayedPhase = null;

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

        private void UpdatePhaseLabel(VehicleTier phase)
        {
            if (iconText == null)
            {
                return;
            }

            switch (phase)
            {
                case VehicleTier.PickupTruck:
                    iconText.text = "PICKUP";
                    break;

                case VehicleTier.HeavyTruck:
                    iconText.text = "HEAVY";
                    break;

                default:
                    iconText.text = "VEHICLE";
                    break;
            }
        }

        [ContextMenu("Rebuild UI")]
        public void RebuildUI()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;

                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
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
            if (this == null)
            {
                return;
            }

            if (containerRect != null &&
                radialFillRing != null &&
                timerText != null)
            {
                return;
            }

            Transform parentTransform = null;

            var factionUI = FindFirstObjectByType<FactionTugOfWarUI>();

            if (factionUI != null)
            {
                parentTransform = factionUI.transform;
            }
            else
            {
                Canvas canvas = FindFirstObjectByType<Canvas>();

                if (canvas != null)
                {
                    parentTransform = canvas.transform;
                }
            }

            if (parentTransform != null &&
                transform.parent != parentTransform)
            {
                transform.SetParent(parentTransform, false);
            }

            containerRect = GetComponent<RectTransform>();

            if (containerRect == null)
            {
                containerRect = gameObject.AddComponent<RectTransform>();
            }

            containerRect.anchorMin = new Vector2(1f, 0.80f);
            containerRect.anchorMax = new Vector2(1f, 0.80f);
            containerRect.pivot = new Vector2(1f, 0.5f);
            containerRect.anchoredPosition = anchoredPosition;
            containerRect.sizeDelta = circleSize;

            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            Sprite circleSp = GetOrCreateCircleSprite();

            // Background
            GameObject bgObj = new GameObject(
                "Circle_Bg",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            bgObj.transform.SetParent(containerRect, false);

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            bgCircleImage = bgObj.GetComponent<Image>();
            bgCircleImage.sprite = circleSp;
            bgCircleImage.color = bgColor;

            // Radial Fill
            GameObject ringObj = new GameObject(
                "Circle_RadialFill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

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

            // Inner Circle
            GameObject innerHole = new GameObject(
                "Circle_Inner",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            innerHole.transform.SetParent(containerRect, false);

            RectTransform innerRect =
                innerHole.GetComponent<RectTransform>();

            innerRect.anchorMin = new Vector2(0.5f, 0.5f);
            innerRect.anchorMax = new Vector2(0.5f, 0.5f);
            innerRect.pivot = new Vector2(0.5f, 0.5f);
            innerRect.sizeDelta = new Vector2(88f, 88f);

            Image innerImg = innerHole.GetComponent<Image>();
            innerImg.sprite = circleSp;
            innerImg.color = innerColor;

            // Phase Label
            GameObject iconObj = new GameObject(
                "Label_Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            iconObj.transform.SetParent(innerHole.transform, false);

            RectTransform iconRect =
                iconObj.GetComponent<RectTransform>();

            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 16f);
            iconRect.sizeDelta = new Vector2(80f, 22f);

            iconText = iconObj.GetComponent<TextMeshProUGUI>();
            iconText.text = "PICKUP";
            iconText.fontSize = 13;
            iconText.fontStyle = FontStyles.Bold;
            iconText.color = activeRingColor;
            iconText.alignment = TextAlignmentOptions.Center;

            // Timer Text
            GameObject textObj = new GameObject(
                "Timer_Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            textObj.transform.SetParent(innerHole.transform, false);

            RectTransform textRect =
                textObj.GetComponent<RectTransform>();

            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0f, -12f);
            textRect.sizeDelta = new Vector2(80f, 32f);

            timerText = textObj.GetComponent<TextMeshProUGUI>();
            timerText.text = "60s";
            timerText.fontSize = 26;
            timerText.fontStyle = FontStyles.Bold;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.color = Color.white;
        }

        private static Sprite GetOrCreateCircleSprite()
        {
            if (_circleSprite != null)
            {
                return _circleSprite;
            }

            int res = 256;

            Texture2D tex = new Texture2D(
                res,
                res,
                TextureFormat.RGBA32,
                false);

            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float radius = (res - 4) * 0.5f;
            Vector2 center = new Vector2(
                res * 0.5f,
                res * 0.5f);

            Color[] colors = new Color[res * res];

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dist = Vector2.Distance(
                        new Vector2(x, y),
                        center);

                    float alpha =
                        Mathf.Clamp01(radius - dist + 1.5f);

                    colors[y * res + x] =
                        new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();

            _circleSprite = Sprite.Create(
                tex,
                new Rect(0, 0, res, res),
                new Vector2(0.5f, 0.5f),
                100f);

            return _circleSprite;
        }
    }
}
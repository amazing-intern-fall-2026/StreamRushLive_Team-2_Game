using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamRush.Features.Runner;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Countdown timer circle for Free Control Buff (Fan / Blue Team).
    /// Dynamically positioned in the left vertical stack below other active buff circles.
    /// </summary>
    public class FreeControlTimerCircle : MonoBehaviour
    {
        private static FreeControlTimerCircle _instance;
        public static FreeControlTimerCircle Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<FreeControlTimerCircle>(FindObjectsInactive.Include);
                }
                if (_instance == null)
                {
                    // If not found in the scene, create an empty GameObject and attach this component.
                    // Sub-UI elements (Circle_Bg, Circle_RadialFill, etc.) are dynamically built in BuildUIIfMissing.
                    var go = new GameObject("FreeControlTimerCircle (Auto)");
                    _instance = go.AddComponent<FreeControlTimerCircle>();
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
        // Cyan glow styling for Free Control HUD element
        [SerializeField] private Color activeRingColor = new Color(0.10f, 0.92f, 0.92f, 1f); // Cyan Glow
        [SerializeField] private Color bgColor = new Color(0.04f, 0.10f, 0.10f, 0.92f); // Deep Teal
        [SerializeField] private Color innerColor = new Color(0.03f, 0.08f, 0.08f, 0.98f);

        private float _totalDuration = 30f;
        private float _remainingTime = 0f;
        private bool _isActive = false;

        public bool IsActive => _isActive;

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
            // Initially hidden until Free Control Buff activates
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

            // Track status from ChatLaneRunnerController
            if (runnerController != null)
            {
                if (runnerController.IsFreeControlActive && !_isActive)
                {
                    ActivateTimer(_totalDuration > 0f ? _totalDuration : 30f);
                }
                else if (!runnerController.IsFreeControlActive && _isActive)
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

            // Update Radial Fill Ring (0..1)
            if (radialFillRing != null && _totalDuration > 0f)
            {
                radialFillRing.fillAmount = Mathf.Clamp01(_remainingTime / _totalDuration);
                radialFillRing.color = activeRingColor;
            }

            // Update remaining seconds
            if (timerText != null)
            {
                timerText.text = $"{Mathf.CeilToInt(_remainingTime)}s";
            }
        }

        /// <summary>
        /// Activates countdown circle.
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
        /// Deactivates countdown circle.
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

            // 1. Resolve parent Canvas or FactionTugOfWarUI
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
            // Bring to top of Canvas draw order to prevent being covered by debug or backdrop panels.
            transform.SetAsLastSibling();

            containerRect = GetComponent<RectTransform>();
            if (containerRect == null) containerRect = gameObject.AddComponent<RectTransform>();

            // Positioning: Align with FanSprintTimerCircle RectTransform and stack underneath (-256px offset).
            // Reset Z axis to 0 to prevent camera clipping.
            var fanSprint = FanSprintTimerCircle.Instance;
            RectTransform fanSprintRect = fanSprint != null ? fanSprint.GetComponent<RectTransform>() : null;

            if (fanSprintRect != null)
            {
                containerRect.anchorMin = fanSprintRect.anchorMin;
                containerRect.anchorMax = fanSprintRect.anchorMax;
                containerRect.pivot = fanSprintRect.pivot;
                containerRect.sizeDelta = fanSprintRect.sizeDelta;
                // Stack below FanSprintTimerCircle and ShieldTimerCircle (-128px per circle)
                containerRect.anchoredPosition3D = fanSprintRect.anchoredPosition3D + new Vector3(0f, -256f, 0f);
            }
            else
            {
                // Fallback when FanSprintTimerCircle is not found in scene
                containerRect.anchorMin = new Vector2(0f, 0.80f);
                containerRect.anchorMax = new Vector2(0f, 0.80f);
                containerRect.pivot = new Vector2(0f, 0.5f);
                containerRect.sizeDelta = new Vector2(116f, 116f);
                containerRect.anchoredPosition3D = new Vector3(53f, -160f, 0f);
            }

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

            // 3. Radial Fill Ring (clockwise 360-degree sweep)
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

            // 4. Center inner cutout for ring effect
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

            // 5. Label Text
            GameObject iconObj = new GameObject("Label_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            iconObj.transform.SetParent(innerHole.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 16f);
            iconRect.sizeDelta = new Vector2(80f, 22f);
            iconText = iconObj.GetComponent<TextMeshProUGUI>();
            iconText.text = "FREE";
            iconText.fontSize = 13;
            iconText.fontStyle = FontStyles.Bold;
            iconText.color = activeRingColor;
            iconText.alignment = TextAlignmentOptions.Center;

            // 6. Countdown Timer Text (seconds)
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
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StreamRushLive.Features.Spawning;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Countdown timer circle for Anti Vehicle Phase (Pickup Truck / Heavy Truck).
    /// Aligned on the right side of the screen (Anti / Red Team).
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
                    _instance = FindFirstObjectByType<AntiVehiclePhaseTimerCircle>(FindObjectsInactive.Include);
                }
                if (_instance == null)
                {
                    GameObject go = new GameObject("AntiVehiclePhaseTimerCircle");
                    _instance = go.AddComponent<AntiVehiclePhaseTimerCircle>();
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
        [SerializeField] private SingleObstacleSpawner obstacleSpawner;
        [SerializeField] private RectTransform containerRect;
        [SerializeField] private Image bgCircleImage;
        [SerializeField] private Image radialFillRing;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text iconText;
        [SerializeField] private CanvasGroup canvasGroup;

        // Circle sprite and label colors are retrieved from HudTheme (HudTheme.GetTimerSkin).

        private float _totalDuration = 60f;
        private float _remainingTime = 0f;
        private bool _isActive = false;

        public bool IsActive => _isActive;

        private VehicleTier? _displayedPhase;

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

            // No active phase
            if (!currentPhase.HasValue)
            {
                if (_isActive)
                {
                    DeactivateTimer();
                }

                return;
            }

            // Phase activated or transitioned between Pickup <-> Heavy
            if (!_isActive || _displayedPhase != currentPhase.Value)
            {
                ActivateTimer(
                    currentPhase.Value,
                    obstacleSpawner.VehiclePhaseDuration);
            }

            // Retrieve remaining time directly from SingleObstacleSpawner
            _remainingTime = obstacleSpawner.VehiclePhaseRemainingTime;

            if (_totalDuration > 0f && radialFillRing != null)
            {
                radialFillRing.fillAmount = Mathf.Clamp01(_remainingTime / _totalDuration);
            }

            if (timerText != null)
            {
                timerText.text = $"{Mathf.CeilToInt(_remainingTime)}s";
            }
        }

        /// <summary>
        /// Shows timer circle for active vehicle phase (Pickup or Heavy).
        /// </summary>
        public void ActivateTimer(VehicleTier phase, float duration)
        {
            _displayedPhase = phase;
            _totalDuration = duration > 0f ? duration : 60f;
            _remainingTime = _totalDuration;
            _isActive = true;

            TimerCircleSkin phaseSkin = HudTheme.Current.GetTimerSkin(
                phase == VehicleTier.HeavyTruck ? TimerCircleKind.VehicleHeavy : TimerCircleKind.VehiclePickup);

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
                radialFillRing.sprite = phaseSkin.ring;
                radialFillRing.color = Color.white;
            }

            if (timerText != null)
            {
                timerText.text = $"{Mathf.CeilToInt(_remainingTime)}s";
            }

            TimerCircleVerticalStackManager.RegisterAntiCircle(this, containerRect);
        }

        /// <summary>
        /// Hides timer circle when vehicle phase concludes.
        /// </summary>
        public void DeactivateTimer()
        {
            _isActive = false;
            _displayedPhase = null;

            TimerCircleVerticalStackManager.UnregisterAntiCircle(this);

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
            TimerCircleVerticalStackManager.UnregisterAntiCircle(this);
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
                    iconText.text = "BEASTS";
                    iconText.color = HudTheme.Current.GetTimerSkin(TimerCircleKind.VehiclePickup).label;
                    break;

                case VehicleTier.HeavyTruck:
                    iconText.text = "TRAIN";
                    iconText.color = HudTheme.Current.GetTimerSkin(TimerCircleKind.VehicleHeavy).label;
                    break;

                default:
                    iconText.text = "HAZARD";
                    iconText.color = HudTheme.Current.GetTimerSkin(TimerCircleKind.VehicleHeavy).label;
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

        public void BuildUIIfMissing()
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

            if (transform.parent == null && parentTransform != null)
            {
                transform.SetParent(parentTransform, false);
            }

            containerRect = GetComponent<RectTransform>();
            if (containerRect == null)
            {
                containerRect = gameObject.AddComponent<RectTransform>();
            }

            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            TimerCircleSkin skin = HudTheme.Current.GetTimerSkin(TimerCircleKind.VehiclePickup);

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
            bgCircleImage.sprite = skin.disc;
            bgCircleImage.color = skin.bgTint;

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
            radialFillRing.sprite = skin.ring;
            radialFillRing.type = Image.Type.Filled;
            radialFillRing.fillMethod = Image.FillMethod.Radial360;
            radialFillRing.fillOrigin = (int)Image.Origin360.Top;
            radialFillRing.fillClockwise = true;
            radialFillRing.fillAmount = 1f;
            radialFillRing.color = Color.white;

            // Inner Circle
            GameObject innerHole = new GameObject(
                "Circle_Inner",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            innerHole.transform.SetParent(containerRect, false);

            RectTransform innerRect = innerHole.GetComponent<RectTransform>();
            innerRect.anchorMin = new Vector2(0.5f, 0.5f);
            innerRect.anchorMax = new Vector2(0.5f, 0.5f);
            innerRect.pivot = new Vector2(0.5f, 0.5f);
            innerRect.sizeDelta = new Vector2(88f, 88f);

            Image innerImg = innerHole.GetComponent<Image>();
            innerImg.sprite = skin.disc;
            innerImg.color = skin.innerTint;

            // Phase Label
            GameObject iconObj = new GameObject(
                "Label_Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            iconObj.transform.SetParent(innerHole.transform, false);

            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 16f);
            iconRect.sizeDelta = new Vector2(80f, 22f);

            iconText = iconObj.GetComponent<TextMeshProUGUI>();
            iconText.text = "PICKUP";
            iconText.fontSize = 13;
            iconText.fontStyle = FontStyles.Bold;
            iconText.color = skin.label;
            iconText.alignment = TextAlignmentOptions.Center;
            if (skin.font != null) iconText.font = skin.font;

            // Timer Text
            GameObject textObj = new GameObject(
                "Timer_Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            textObj.transform.SetParent(innerHole.transform, false);

            RectTransform textRect = textObj.GetComponent<RectTransform>();
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
            if (skin.font != null) timerText.font = skin.font;
        }
    }
}

using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamRush.Features.Runner;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Vòng tròn đếm ngược thời gian tác dụng của quà 'Bình Thao Tác Tự Do (Free-Control Buff)'
    /// (Shift+F1), nằm cạnh thanh năng lượng Fan (Phe Xanh), xếp NGAY DƯỚI vòng Bình Tăng Tốc
    /// (FanSprintTimerCircle - F2) để không đè lên nhau khi cả 2 buff cùng chạy (GDD v1.4.1 mục 3
    /// và mục 6: "cạnh ngoài hiển thị vòng tròn đếm ngược Bình Tăng Tốc (30s) & Bình Thao Tác Tự
    /// Do (30s)"). Cấu trúc dựng UI bằng code y hệt FanSprintTimerCircle, chỉ đổi màu (Cyan thay vì
    /// Electric Blue), nhãn ("TỰ DO" thay vì "TĂNG TỐC") và vị trí anchor.
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
                    // Không tìm thấy sẵn trong scene (không cần kéo thả GameObject thủ công) -
                    // tự tạo 1 GameObject rỗng và gắn component này vào, y hệt cách các UI con bên
                    // trong (Circle_Bg, Circle_RadialFill...) đã tự dựng bằng code ở BuildUIIfMissing.
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
        }

        [Header("References")]
        [SerializeField] private ChatLaneRunnerController runnerController;
        [SerializeField] private RectTransform containerRect;
        [SerializeField] private Image bgCircleImage;
        [SerializeField] private Image radialFillRing;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text iconText;
        [SerializeField] private CanvasGroup canvasGroup;

        // GDD v1.4.1 mục 3: vòng tròn đếm ngược Free Control. Sprite vòng + màu nhãn lấy từ HudTheme
        // (HudTheme.GetTimerSkin) - không còn tự vẽ sprite/màu ở đây.
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
            // Mặc định ẩn khi chưa kích hoạt Free-Control Buff
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

            // Theo dõi trạng thái từ ChatLaneRunnerController (đặt bởi ActivateFreeControl/FreeControlRoutine)
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

            // 1. Tìm FactionTugOfWarUI hoặc Canvas để đặt vị trí cạnh thân thanh Fan
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
            // Đưa lên trên cùng thứ tự vẽ trong Canvas, tránh bị panel Debug (backdrop đen) hoặc
            // các UI khác vẽ đè lên sau này che mất vòng tròn.
            transform.SetAsLastSibling();

            containerRect = GetComponent<RectTransform>();
            if (containerRect == null) containerRect = gameObject.AddComponent<RectTransform>();

            // Vị trí: LẤY TRỰC TIẾP RectTransform thật của FanSprintTimerCircle lúc đang chạy (chứ
            // không đoán một mốc % cố định) - vì vị trí thật của nó trong scene có thể đã được
            // chỉnh tay khác với giá trị mặc định trong code gốc. Sao y hệt anchor/pivot/size của
            // nó, chỉ lùi xuống dưới một khoảng pixel cố định (Y -140) để xếp ngay dưới, không đè
            // lên nhau. Dùng anchoredPosition3D (không phải anchoredPosition/Vector2) để RESET
            // luôn trục Z về đúng 0 - tránh việc object thừa hưởng Z rác từ cha cũ trước khi bị
            // SetParent vào đây (SetParent(parent, false) chỉ giữ nguyên local position cũ, không
            // tự reset Z), có thể đẩy vòng ra khỏi tầm nhìn camera dù Alpha vẫn = 1.
            var fanSprint = FanSprintTimerCircle.Instance;
            RectTransform fanSprintRect = fanSprint != null ? fanSprint.GetComponent<RectTransform>() : null;

            if (fanSprintRect != null)
            {
                containerRect.anchorMin = fanSprintRect.anchorMin;
                containerRect.anchorMax = fanSprintRect.anchorMax;
                containerRect.pivot = fanSprintRect.pivot;
                containerRect.sizeDelta = fanSprintRect.sizeDelta;
                // Xếp bên dưới cả FanSprintTimerCircle và ShieldTimerCircle (-128px mỗi vòng)
                containerRect.anchoredPosition3D = fanSprintRect.anchoredPosition3D + new Vector3(0f, -256f, 0f);
            }
            else
            {
                // Fallback khi không tìm thấy FanSprintTimerCircle trong scene (hiếm khi xảy ra)
                containerRect.anchorMin = new Vector2(0f, 0.80f);
                containerRect.anchorMax = new Vector2(0f, 0.80f);
                containerRect.pivot = new Vector2(0f, 0.5f);
                containerRect.sizeDelta = new Vector2(116f, 116f);
                containerRect.anchoredPosition3D = new Vector3(53f, -160f, 0f);
            }

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            TimerCircleSkin skin = HudTheme.Current.GetTimerSkin(TimerCircleKind.FreeControl);

            // 2. Background Circle (116x116)
            GameObject bgObj = new GameObject("Circle_Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgObj.transform.SetParent(containerRect, false);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgCircleImage = bgObj.GetComponent<Image>();
            bgCircleImage.sprite = skin.disc;
            bgCircleImage.color = skin.bgTint;

            // 3. Radial Fill Ring (Quét 360 độ từ đỉnh theo chiều kim đồng hồ)
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

            // 4. Center Inner Mask/Hole (vành khuyên tròn 88x88 -> độ dày viền ring 14px)
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

            // 5. Label Text ("TỰ DO")
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
            iconText.color = skin.label;
            iconText.alignment = TextAlignmentOptions.Center;
            if (skin.font != null) iconText.font = skin.font;

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
            if (skin.font != null) timerText.font = skin.font;
        }

    }
}
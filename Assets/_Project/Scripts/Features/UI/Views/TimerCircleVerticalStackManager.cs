using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Quản lý trục dọc động (Dynamic Vertical Stack) cho tất cả các popup Timer Circle:
    /// - Phe Fan (bên trái): FanSprintTimerCircle, ShieldTimerCircle, FreeControlTimerCircle.
    /// - Phe Anti (bên phải): AntiUnlimitedTimerCircle, AntiVehiclePhaseTimerCircle.
    /// 
    /// Quy tắc hiển thị:
    /// 1. Vòng tròn đầu tiên kích hoạt luôn xuất hiện ở vị trí TRÊN CÙNG.
    /// 2. Khi có thêm vòng tròn mới, nó sẽ tự động trôi dần xuống dưới các vòng tròn trước đó theo trục dọc.
    /// 3. Khi một vòng tròn hết giờ biến mất, các vòng tròn bên dưới sẽ tự động trượt mượt mà lên trên lấp vào khoảng trống.
    /// </summary>
    public class TimerCircleVerticalStackManager : MonoBehaviour
    {
        private static TimerCircleVerticalStackManager _instance;

        public static TimerCircleVerticalStackManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<TimerCircleVerticalStackManager>(FindObjectsInactive.Include);
                }

                if (_instance == null)
                {
                    var factionUI = FindFirstObjectByType<SteamRush.Features.UI.FactionTugOfWarUI>();
                    GameObject go = new GameObject("TimerCircleVerticalStackManager");
                    if (factionUI != null)
                    {
                        go.transform.SetParent(factionUI.transform, false);
                    }
                    _instance = go.AddComponent<TimerCircleVerticalStackManager>();
                }

                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        private class StackEntry
        {
            public MonoBehaviour Circle;
            public RectTransform Rect;

            public StackEntry(MonoBehaviour circle, RectTransform rect)
            {
                Circle = circle;
                Rect = rect;
            }
        }

        [Header("Fan Side Stack (Left)")]
        [SerializeField] private float fanBaseX = 53f;
        [SerializeField] private float fanTopY = 95.6f;
        [SerializeField] private float fanStepY = -128f;

        [Header("Anti Side Stack (Right)")]
        [SerializeField] private float antiBaseX = -53f;
        [SerializeField] private float antiTopY = 103.6f;
        [SerializeField] private float antiStepY = -128f;

        [Header("Animation Settings")]
        [SerializeField] private float slideDuration = 0.35f;
        [SerializeField] private Ease slideEase = Ease.OutCubic;

        private readonly List<StackEntry> _activeFanStack = new List<StackEntry>();
        private readonly List<StackEntry> _activeAntiStack = new List<StackEntry>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #region Public Static API

        /// <summary>
        /// Đăng ký một vòng tròn buff bên phe Fan vào trục dọc bên trái.
        /// </summary>
        public static void RegisterFanCircle(MonoBehaviour circle, RectTransform rect)
        {
            if (circle == null || rect == null) return;
            Instance.RegisterCircleInternal(circle, rect, isFanSide: true);
        }

        /// <summary>
        /// Hủy đăng ký vòng tròn buff bên phe Fan khi hết thời gian hoặc bị hủy.
        /// </summary>
        public static void UnregisterFanCircle(MonoBehaviour circle)
        {
            if (circle == null) return;
            if (_instance != null)
            {
                _instance.UnregisterCircleInternal(circle, isFanSide: true);
            }
        }

        /// <summary>
        /// Đăng ký một vòng tròn hazard bên phe Anti vào trục dọc bên phải.
        /// </summary>
        public static void RegisterAntiCircle(MonoBehaviour circle, RectTransform rect)
        {
            if (circle == null || rect == null) return;
            Instance.RegisterCircleInternal(circle, rect, isFanSide: false);
        }

        /// <summary>
        /// Hủy đăng ký vòng tròn hazard bên phe Anti khi hết thời gian.
        /// </summary>
        public static void UnregisterAntiCircle(MonoBehaviour circle)
        {
            if (circle == null) return;
            if (_instance != null)
            {
                _instance.UnregisterCircleInternal(circle, isFanSide: false);
            }
        }

        #endregion

        #region Internal Layout Logic

        private void RegisterCircleInternal(MonoBehaviour circle, RectTransform rect, bool isFanSide)
        {
            List<StackEntry> stack = isFanSide ? _activeFanStack : _activeAntiStack;
            CleanupNullEntries(stack);

            int existingIndex = stack.FindIndex(e => e.Circle == circle);
            if (existingIndex >= 0)
            {
                // Đã có trong stack -> giữ nguyên hoặc cập nhật lại rect
                stack[existingIndex].Rect = rect;
                RepositionStack(isFanSide, animateNew: false);
                return;
            }

            // Thêm mới vào cuối stack
            var newEntry = new StackEntry(circle, rect);
            stack.Add(newEntry);

            float baseX = isFanSide ? fanBaseX : antiBaseX;
            float topY = isFanSide ? fanTopY : antiTopY;
            float stepY = isFanSide ? fanStepY : antiStepY;

            int targetIndex = stack.Count - 1;
            float targetY = topY + (targetIndex * stepY);

            // Đảm bảo anchor và pivot chuẩn
            Vector2 anchor = isFanSide ? new Vector2(0f, 0.80f) : new Vector2(1f, 0.80f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = isFanSide ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);

            DOTween.Kill(rect);

            if (targetIndex == 0)
            {
                // Vòng tròn đầu tiên: xuất hiện ngay tại vị trí TRÊN CÙNG
                rect.anchoredPosition = new Vector2(baseX, targetY);
                rect.localScale = new Vector3(0.85f, 0.85f, 1f);
                rect.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
            }
            else
            {
                // Vòng tròn thứ 2 trở đi: xuất hiện từ trên rồi trôi dần xuống vị trí bên dưới
                float startY = topY + ((targetIndex - 1) * stepY);
                rect.anchoredPosition = new Vector2(baseX, startY);
                rect.localScale = new Vector3(0.9f, 0.9f, 1f);

                rect.DOAnchorPosY(targetY, slideDuration).SetEase(slideEase);
                rect.DOScale(Vector3.one, slideDuration).SetEase(Ease.OutBack);
            }
        }

        private void UnregisterCircleInternal(MonoBehaviour circle, bool isFanSide)
        {
            List<StackEntry> stack = isFanSide ? _activeFanStack : _activeAntiStack;
            CleanupNullEntries(stack);

            int removeIndex = stack.FindIndex(e => e.Circle == circle);
            if (removeIndex >= 0)
            {
                stack.RemoveAt(removeIndex);
                // Các vòng tròn bên dưới tự động trượt lên lấp đầy khoảng trống
                RepositionStack(isFanSide, animateNew: true);
            }
        }

        private void RepositionStack(bool isFanSide, bool animateNew)
        {
            List<StackEntry> stack = isFanSide ? _activeFanStack : _activeAntiStack;
            CleanupNullEntries(stack);

            float baseX = isFanSide ? fanBaseX : antiBaseX;
            float topY = isFanSide ? fanTopY : antiTopY;
            float stepY = isFanSide ? fanStepY : antiStepY;

            for (int i = 0; i < stack.Count; i++)
            {
                var entry = stack[i];
                if (entry.Rect == null) continue;

                float targetY = topY + (i * stepY);
                DOTween.Kill(entry.Rect);

                if (animateNew)
                {
                    entry.Rect.DOAnchorPosY(targetY, slideDuration).SetEase(slideEase);
                    entry.Rect.DOAnchorPosX(baseX, slideDuration);
                }
                else
                {
                    entry.Rect.anchoredPosition = new Vector2(baseX, targetY);
                }
            }
        }

        private static void CleanupNullEntries(List<StackEntry> stack)
        {
            stack.RemoveAll(e => e.Circle == null || e.Rect == null);
        }

        #endregion
    }
}

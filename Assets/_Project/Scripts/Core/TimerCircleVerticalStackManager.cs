using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Manages dynamic vertical stacking for all Timer Circle popups:
    /// - Fan Team (Left column): FanSprintTimerCircle, ShieldTimerCircle, FreeControlTimerCircle.
    /// - Anti Team (Right column): AntiUnlimitedTimerCircle, AntiVehiclePhaseTimerCircle.
    ///
    /// Stacking rules:
    /// 1. First active circle appears at the topmost position.
    /// 2. New circles stack beneath existing ones along the vertical axis.
    /// 3. When a circle expires, circles below smoothly slide up to fill the gap.
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
        /// Registers a Fan team buff timer circle in the left vertical stack.
        /// </summary>
        public static void RegisterFanCircle(MonoBehaviour circle, RectTransform rect)
        {
            if (circle == null || rect == null) return;
            Instance.RegisterCircleInternal(circle, rect, isFanSide: true);
        }

        /// <summary>
        /// Unregisters a Fan team buff timer circle when expired or cancelled.
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
        /// Registers an Anti team hazard timer circle in the right vertical stack.
        /// </summary>
        public static void RegisterAntiCircle(MonoBehaviour circle, RectTransform rect)
        {
            if (circle == null || rect == null) return;
            Instance.RegisterCircleInternal(circle, rect, isFanSide: false);
        }

        /// <summary>
        /// Unregisters an Anti team hazard timer circle when expired.
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
                // Already in stack -> preserve or update cached rect
                stack[existingIndex].Rect = rect;
                RepositionStack(isFanSide, animateNew: false);
                return;
            }

            // Append to end of stack
            var newEntry = new StackEntry(circle, rect);
            stack.Add(newEntry);

            float baseX = isFanSide ? fanBaseX : antiBaseX;
            float topY = isFanSide ? fanTopY : antiTopY;
            float stepY = isFanSide ? fanStepY : antiStepY;

            int targetIndex = stack.Count - 1;
            float targetY = topY + (targetIndex * stepY);

            // Ensure proper anchor and pivot settings
            Vector2 anchor = isFanSide ? new Vector2(0f, 0.80f) : new Vector2(1f, 0.80f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = isFanSide ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);

            DOTween.Kill(rect);

            if (targetIndex == 0)
            {
                // First circle: appears at topmost position
                rect.anchoredPosition = new Vector2(baseX, targetY);
                rect.localScale = new Vector3(0.85f, 0.85f, 1f);
                rect.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
            }
            else
            {
                // Subsequent circles: float down to stacked slot
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
                // Remaining circles slide up to close gaps
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

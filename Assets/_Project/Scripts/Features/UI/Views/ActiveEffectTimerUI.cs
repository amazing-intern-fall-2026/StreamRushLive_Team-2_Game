using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Hiển thị thanh đếm ngược thời gian tác dụng của các hiệu ứng (Buff/Debuff/Quà tặng)
    /// như Thả Xe Không Giới Hạn (60s), Khiên Bảo Vệ (20s), Bứt Tốc (30s), Nhảy Meme (60s).
    /// Hỗ trợ cả tự động tạo UI runtime hoặc gán sẵn qua Inspector.
    /// </summary>
    public class ActiveEffectTimerUI : MonoBehaviour
    {
        private static ActiveEffectTimerUI _instance;

        public static ActiveEffectTimerUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ActiveEffectTimerUI>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ActiveEffectTimerUI", typeof(RectTransform));
                        _instance = go.AddComponent<ActiveEffectTimerUI>();
                    }
                }
                return _instance;
            }
        }

        private class ActiveTimerEntry
        {
            public string Key;
            public string Title;
            public float TotalDuration;
            public float RemainingTime;
            public Color AccentColor;
            public Sprite Icon;

            public GameObject CardObj;
            public RectTransform CardRect;
            public CanvasGroup CanvasGroup;
            public TMP_Text TitleText;
            public TMP_Text TimerText;
            public Image FillImage;
            public Image IconImage;
            public Outline Outline;
        }

        [Header("Container Configuration")]
        [SerializeField] private RectTransform containerRect;
        [SerializeField] private Vector2 defaultAnchoredPos = new Vector2(0f, -195f);
        [SerializeField] private Vector2 cardSize = new Vector2(380f, 48f);

        private readonly Dictionary<string, ActiveTimerEntry> _activeTimers = new Dictionary<string, ActiveTimerEntry>();
        private readonly List<string> _keysToRemove = new List<string>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            EnsureContainerExists();
        }

        private void EnsureContainerExists()
        {
            if (containerRect == null)
            {
                Canvas mainCanvas = GetComponentInParent<Canvas>();
                if (mainCanvas == null)
                {
                    mainCanvas = FindFirstObjectByType<Canvas>();
                }

                if (mainCanvas != null)
                {
                    transform.SetParent(mainCanvas.transform, false);

                    containerRect = GetComponent<RectTransform>();
                    if (containerRect == null) containerRect = gameObject.AddComponent<RectTransform>();

                    containerRect.anchorMin = new Vector2(0.5f, 1f);
                    containerRect.anchorMax = new Vector2(0.5f, 1f);
                    containerRect.pivot = new Vector2(0.5f, 1f);
                    containerRect.anchoredPosition = defaultAnchoredPos;
                    containerRect.sizeDelta = new Vector2(500f, 200f);

                    var vlg = gameObject.GetComponent<VerticalLayoutGroup>();
                    if (vlg == null) vlg = gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.childAlignment = TextAnchor.UpperCenter;
                    vlg.childControlWidth = false;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = false;
                    vlg.childForceExpandHeight = false;
                    vlg.spacing = 8f;
                }
            }
        }

        public static void ShowTimer(string key, string title, float duration, Color accentColor, Sprite icon = null)
        {
            if (Instance != null)
            {
                Instance.InternalShowTimer(key, title, duration, accentColor, icon);
            }
        }

        public static void CancelTimer(string key)
        {
            if (_instance != null)
            {
                _instance.InternalCancelTimer(key);
            }
        }

        private void InternalShowTimer(string key, string title, float duration, Color accentColor, Sprite icon)
        {
            EnsureContainerExists();

            if (_activeTimers.TryGetValue(key, out var existingEntry))
            {
                existingEntry.TotalDuration = duration;
                existingEntry.RemainingTime = duration;
                existingEntry.Title = title;
                existingEntry.AccentColor = accentColor;
                if (icon != null) existingEntry.Icon = icon;
                UpdateEntryVisuals(existingEntry);
                return;
            }

            ActiveTimerEntry newEntry = CreateCardUI(key, title, duration, accentColor, icon);
            _activeTimers[key] = newEntry;

            // Hiệu ứng Pop-in
            newEntry.CanvasGroup.alpha = 0f;
            newEntry.CardRect.localScale = Vector3.one * 0.7f;
            newEntry.CanvasGroup.DOFade(1f, 0.25f);
            newEntry.CardRect.DOScale(1f, 0.25f).SetEase(Ease.OutBack);
        }

        private void InternalCancelTimer(string key)
        {
            if (_activeTimers.TryGetValue(key, out var entry))
            {
                AnimateAndDestroyCard(entry);
                _activeTimers.Remove(key);
            }
        }

        private void Update()
        {
            if (_activeTimers.Count == 0) return;

            float dt = Time.deltaTime;
            _keysToRemove.Clear();

            foreach (var kvp in _activeTimers)
            {
                var entry = kvp.Value;
                entry.RemainingTime -= dt;

                if (entry.RemainingTime <= 0f)
                {
                    _keysToRemove.Add(kvp.Key);
                }
                else
                {
                    UpdateEntryVisuals(entry);
                }
            }

            for (int i = 0; i < _keysToRemove.Count; i++)
            {
                string key = _keysToRemove[i];
                if (_activeTimers.TryGetValue(key, out var entry))
                {
                    AnimateAndDestroyCard(entry);
                    _activeTimers.Remove(key);
                }
            }
        }

        private void UpdateEntryVisuals(ActiveTimerEntry entry)
        {
            if (entry.CardObj == null) return;

            int seconds = Mathf.CeilToInt(entry.RemainingTime);
            if (entry.TimerText != null)
            {
                entry.TimerText.text = $"{seconds}s";
            }

            if (entry.FillImage != null && entry.TotalDuration > 0f)
            {
                entry.FillImage.fillAmount = Mathf.Clamp01(entry.RemainingTime / entry.TotalDuration);
            }
        }

        private void AnimateAndDestroyCard(ActiveTimerEntry entry)
        {
            if (entry.CardObj != null)
            {
                DOTween.Kill(entry.CardRect);
                DOTween.Kill(entry.CanvasGroup);

                Sequence seq = DOTween.Sequence();
                seq.Append(entry.CardRect.DOScale(0.8f, 0.2f).SetEase(Ease.InBack));
                seq.Join(entry.CanvasGroup.DOFade(0f, 0.2f));
                seq.OnComplete(() =>
                {
                    if (entry.CardObj != null)
                    {
                        Destroy(entry.CardObj);
                    }
                });
            }
        }

        private ActiveTimerEntry CreateCardUI(string key, string title, float duration, Color accentColor, Sprite icon)
        {
            GameObject card = new GameObject($"EffectTimer_{key}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(Outline));
            card.transform.SetParent(containerRect != null ? containerRect : transform, false);

            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.sizeDelta = cardSize;

            Image bgImage = card.GetComponent<Image>();
            bgImage.color = new Color(0.06f, 0.08f, 0.14f, 0.92f);

            CanvasGroup cg = card.GetComponent<CanvasGroup>();

            Outline outline = card.GetComponent<Outline>();
            outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            // 1. Fill Bar Background (Dưới đáy card)
            GameObject barBg = new GameObject("FillBar_Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barBg.transform.SetParent(card.transform, false);
            RectTransform barBgRect = barBg.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0f);
            barBgRect.anchorMax = new Vector2(1f, 0f);
            barBgRect.pivot = new Vector2(0.5f, 0f);
            barBgRect.sizeDelta = new Vector2(0f, 5f);
            barBgRect.anchoredPosition = Vector2.zero;
            Image barBgImg = barBg.GetComponent<Image>();
            barBgImg.color = new Color(1f, 1f, 1f, 0.12f);

            // 2. Fill Bar Foreground
            GameObject barFg = new GameObject("FillBar_Fg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barFg.transform.SetParent(barBg.transform, false);
            RectTransform barFgRect = barFg.GetComponent<RectTransform>();
            barFgRect.anchorMin = Vector2.zero;
            barFgRect.anchorMax = Vector2.one;
            barFgRect.sizeDelta = Vector2.zero;
            Image barFgImg = barFg.GetComponent<Image>();
            barFgImg.type = Image.Type.Filled;
            barFgImg.fillMethod = Image.FillMethod.Horizontal;
            barFgImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFgImg.fillAmount = 1f;
            barFgImg.color = accentColor;

            // 3. Icon (nếu có)
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObj.transform.SetParent(card.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(28f, 28f);
            iconRect.anchoredPosition = new Vector2(10f, 2f);
            Image iconImg = iconObj.GetComponent<Image>();
            if (icon != null)
            {
                iconImg.sprite = icon;
                iconImg.color = accentColor;
            }
            else
            {
                iconImg.enabled = false;
            }

            // 4. Title Text
            GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(card.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 0.5f);
            float leftPadding = icon != null ? 46f : 16f;
            titleRect.offsetMin = new Vector2(leftPadding, 6f);
            titleRect.offsetMax = new Vector2(-75f, 0f);

            TMP_Text titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
            titleTmp.text = title;
            titleTmp.fontSize = 15;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            titleTmp.color = Color.white;
            titleTmp.textWrappingMode = TextWrappingModes.NoWrap;
            titleTmp.overflowMode = TextOverflowModes.Ellipsis;

            // 5. Timer Text (Số giây đếm ngược)
            GameObject timerObj = new GameObject("TimerText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            timerObj.transform.SetParent(card.transform, false);
            RectTransform timerRect = timerObj.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(1f, 0f);
            timerRect.anchorMax = new Vector2(1f, 1f);
            timerRect.pivot = new Vector2(1f, 0.5f);
            timerRect.sizeDelta = new Vector2(70f, 0f);
            timerRect.anchoredPosition = new Vector2(-12f, 2f);

            TMP_Text timerTmp = timerObj.GetComponent<TextMeshProUGUI>();
            timerTmp.text = $"{Mathf.CeilToInt(duration)}s";
            timerTmp.fontSize = 18;
            timerTmp.fontStyle = FontStyles.Bold;
            timerTmp.alignment = TextAlignmentOptions.MidlineRight;
            timerTmp.color = accentColor;

            return new ActiveTimerEntry
            {
                Key = key,
                Title = title,
                TotalDuration = duration,
                RemainingTime = duration,
                AccentColor = accentColor,
                Icon = icon,
                CardObj = card,
                CardRect = cardRect,
                CanvasGroup = cg,
                TitleText = titleTmp,
                TimerText = timerTmp,
                FillImage = barFgImg,
                IconImage = iconImg,
                Outline = outline
            };
        }
    }
}

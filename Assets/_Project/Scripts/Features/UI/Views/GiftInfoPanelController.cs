using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SteamRush.Core;
using SteamRush.Features.StreamIntegration;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Sorting options for gift cards on UI.
    /// </summary>
    public enum GiftSortOption
    {
        [InspectorName("By Element Order (Left -> Right, Top -> Bottom)")]
        ByElementOrder = 0,

        [InspectorName("Team: Blue -> Red -> Special")]
        ByTeam_BlueRedSpecial = 1,

        [InspectorName("Team: Red -> Blue -> Special")]
        ByTeam_RedBlueSpecial = 2,

        [InspectorName("By Gift ID")]
        ByGiftId = 3,

        [InspectorName("By Value")]
        ByValue = 4,

        [InspectorName("None")]
        None = 5
    }

    /// <summary>
    /// Manages the gift banner UI (Canvas/GiftInfoPanel).
    /// Supports multi-row grid layout, auto-sorting by team, and gift action highlights.
    /// </summary>
    [DisallowMultipleComponent]
    public class GiftInfoPanelController : MonoBehaviour
    {
        [Header("Client Reference")]
        [SerializeField] private TikTokLiveClient _liveClient;

        [Header("Layout & Sort")]


        [Tooltip("Number of rows to display gift cards (Default: 2 rows for balanced layout).")]
        [Range(1, 4)]
        [SerializeField] private int _rowCount = 2;

        [Tooltip("Auto-sort mode for gift cards on UI (default: displays strictly in element order).")]
        [SerializeField] private GiftSortOption _sortOption = GiftSortOption.ByElementOrder;

#if UNITY_EDITOR
        [Tooltip("Auto-arrange and re-align gift cards when modified in Inspector or Runtime.")]
        [SerializeField] private bool _autoArrangeOnChanged = true;
#endif

        [Header("Card Configuration")]
        [SerializeField] private float _cardSpacing = 8f;
        [Tooltip("Show or hide gift icons on cards")]
        [SerializeField] private bool _showGiftIcons = true;

        public bool ShowGiftIcons
        {
            get => _showGiftIcons;
            set
            {
                if (_showGiftIcons != value)
                {
                    _showGiftIcons = value;
                    BuildGiftDisplay();
                }
            }
        }
        // Card background/border sprites and text colors across the 4 categories (Blue/Red/Special/Like)
        // are retrieved dynamically from shared HudTheme.Current.

        [Header("UI References")]
        [SerializeField] private RectTransform _contentContainer;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private Sprite _fallbackIcon;

        private readonly Dictionary<string, RectTransform> _cardMap = new Dictionary<string, RectTransform>();
        private readonly Dictionary<int, RectTransform> _cardIdMap = new Dictionary<int, RectTransform>();

        public int RowCount
        {
            get => _rowCount;
            set
            {
                _rowCount = Mathf.Max(1, value);
                BuildGiftDisplay();
            }
        }

        public GiftSortOption SortOption
        {
            get => _sortOption;
            set
            {
                _sortOption = value;
                BuildGiftDisplay();
            }
        }

        private void Awake()
        {
            if (_liveClient == null)
            {
                _liveClient = FindFirstObjectByType<TikTokLiveClient>();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && _autoArrangeOnChanged)
            {
                UnityEditor.EditorApplication.delayCall -= DelayedRebuildInEditor;
                UnityEditor.EditorApplication.delayCall += DelayedRebuildInEditor;
            }
        }

        private void DelayedRebuildInEditor()
        {
            if (this == null) return;
            BuildGiftDisplay();
        }
#endif

        private void Start()
        {
            BuildGiftDisplay();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled && _contentContainer != null && _contentContainer.childCount > 0)
            {
                BuildGiftDisplay();
            }
        }

        /// <summary>
        /// Rebuilds and arranges the gift cards UI.
        /// </summary>
        [ContextMenu("Rebuild Gift Display")]
        public void BuildGiftDisplay()
        {
            if (_liveClient == null)
            {
                _liveClient = FindFirstObjectByType<TikTokLiveClient>();
            }

            Transform placeholder = transform.Find("PlaceholderLabel");

            var rawMappings = _liveClient != null ? _liveClient.GiftMappings : null;
            if (rawMappings == null || rawMappings.Count == 0)
            {
                var router = FindFirstObjectByType<TikTokGiftRouter>();
                if (router != null)
                {
                    rawMappings = router.GiftMappings;
                }
            }

            if (rawMappings == null || rawMappings.Count == 0)
            {
                ClearAllCards();
                if (placeholder != null) placeholder.gameObject.SetActive(true);
                return;
            }

            if (placeholder != null)
            {
                placeholder.gameObject.SetActive(false);
            }

            // 1. Sort order
            List<TikTokGiftMapping> sortedMappings = GetSortedMappings(rawMappings);
            int count = sortedMappings.Count;

            // 2. Setup container
            SetupOriginalContainer(count);

            if (_contentContainer == null) return;

            // 3. Clear existing cards
            ClearAllCards();

            // 4. Calculate card dimensions to fit grid rows/columns
            int rows = Mathf.Max(1, _rowCount);
            int cols = Mathf.Max(1, Mathf.CeilToInt(count / (float)rows));

            float availableW = GetAvailableWidth();
            float availableH = GetAvailableHeight();
            float padX = 8f;
            float padY = 6f;
            float spX = _cardSpacing;
            float spY = 6f;

            float usableW = Mathf.Max(100f, availableW - (padX * 2f) - (spX * (cols - 1)));
            float usableH = Mathf.Max(60f, availableH - (padY * 2f) - (spY * (rows - 1)));
            float cardW = Mathf.Floor(usableW / cols);
            float cardH = Mathf.Floor(usableH / rows);

            GridLayoutGroup glg = _contentContainer.GetComponent<GridLayoutGroup>();
            if (glg != null)
            {
                // Display from left to right, top to bottom
                glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
                glg.startAxis = GridLayoutGroup.Axis.Horizontal;
                glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                glg.constraintCount = cols;
                glg.childAlignment = TextAnchor.MiddleCenter;
                glg.cellSize = new Vector2(cardW, cardH);
                glg.spacing = new Vector2(spX, spY);
                glg.padding = new RectOffset((int)padX, (int)padX, (int)padY, (int)padY);
            }

            // 5. Instantiate gift cards
            foreach (var item in sortedMappings)
            {
                CreateOriginalCard(item, cardW, cardH);
            }

            Canvas.ForceUpdateCanvases();
        }

        private void ClearAllCards()
        {
            if (_contentContainer == null) return;

            for (int i = _contentContainer.childCount - 1; i >= 0; i--)
            {
                GameObject child = _contentContainer.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child);
                else
                    Destroy(child);
#else
                Destroy(child);
#endif
            }

            _cardMap.Clear();
            _cardIdMap.Clear();
        }

        public List<TikTokGiftMapping> GetSortedMappings(List<TikTokGiftMapping> sourceList)
        {
            if (sourceList == null || sourceList.Count == 0) return new List<TikTokGiftMapping>();
            var list = new List<TikTokGiftMapping>(sourceList);

            switch (_sortOption)
            {
                case GiftSortOption.ByElementOrder:
                case GiftSortOption.None:
                    // Preserve 100% element order as declared in Gift Mappings (left to right, top to bottom)
                    break;

                case GiftSortOption.ByTeam_BlueRedSpecial:
                    list.Sort((a, b) =>
                    {
                        int pA = GetActionGroupPriority(a.action, true);
                        int pB = GetActionGroupPriority(b.action, true);
                        if (pA != pB) return pA.CompareTo(pB);
                        if (a.customValue != b.customValue) return a.customValue.CompareTo(b.customValue);
                        return a.giftId.CompareTo(b.giftId);
                    });
                    break;

                case GiftSortOption.ByTeam_RedBlueSpecial:
                    list.Sort((a, b) =>
                    {
                        int pA = GetActionGroupPriority(a.action, false);
                        int pB = GetActionGroupPriority(b.action, false);
                        if (pA != pB) return pA.CompareTo(pB);
                        if (a.customValue != b.customValue) return a.customValue.CompareTo(b.customValue);
                        return a.giftId.CompareTo(b.giftId);
                    });
                    break;

                case GiftSortOption.ByGiftId:
                    list.Sort((a, b) => a.giftId.CompareTo(b.giftId));
                    break;

                case GiftSortOption.ByValue:
                    list.Sort((a, b) => a.customValue.CompareTo(b.customValue));
                    break;

                default:
                    break;
            }

            return list;
        }

        private int GetActionGroupPriority(GiftActionType action, bool blueFirst)
        {
            int blueBase = blueFirst ? 10 : 20;
            int redBase = blueFirst ? 20 : 10;
            const int specialBase = 30;

            return action switch
            {
                GiftActionType.Blue_EnergyBottle => blueBase + 1,
                GiftActionType.Blue_SpeedBoost => blueBase + 2,
                GiftActionType.Blue_Shield => blueBase + 3,
                GiftActionType.Blue_FreeControl => blueBase + 4,

                GiftActionType.Red_SpawnPickup => redBase + 1,
                GiftActionType.Red_SpawnHeavyTruck => redBase + 2,
                GiftActionType.Red_UnlimitedCars => redBase + 3,
                GiftActionType.Red_EnergyBottle => redBase + 4,

                GiftActionType.Special_GiftDance => specialBase + 1,
                GiftActionType.Special_RainHazard => specialBase + 2,
                GiftActionType.Follow_Runner => specialBase + 3,
                GiftActionType.Special_VIPRelayTicket => specialBase + 3,
                GiftActionType.Like_Energy => specialBase + 4,

                _ => 99
            };
        }

        private float GetAvailableWidth()
        {
            if (_scrollRect != null)
            {
                float w = _scrollRect.GetComponent<RectTransform>().rect.width;
                if (w > 10f) return w;
            }

            RectTransform parentRt = GetComponent<RectTransform>();
            if (parentRt != null && parentRt.rect.width > 10f)
            {
                return parentRt.rect.width - 24f;
            }

            return 1016f;
        }

        private float GetAvailableHeight()
        {
            if (_scrollRect != null)
            {
                float h = _scrollRect.GetComponent<RectTransform>().rect.height;
                if (h > 10f) return h;
            }

            RectTransform parentRt = GetComponent<RectTransform>();
            if (parentRt != null && parentRt.rect.height > 10f)
            {
                return parentRt.rect.height - 16f;
            }

            return 240f;
        }


        /// <summary>
        /// Configures the panel, viewport, and grid container.
        /// </summary>
        private void SetupOriginalContainer(int itemCount)
        {

            Image panelImg = GetComponent<Image>();
            if (panelImg != null)
            {
                // Only assign default frame sprite if none is assigned in the Scene/Inspector
                if (panelImg.sprite == null)
                {
                    Sprite frame = HudTheme.Current.panelFrame;
                    if (frame != null) panelImg.sprite = frame;
                    panelImg.type = Image.Type.Sliced;
                    panelImg.pixelsPerUnitMultiplier = 0.6f;
                }
            }

            if (_scrollRect == null)
            {
                _scrollRect = GetComponentInChildren<ScrollRect>();
            }

            if (_scrollRect == null)
            {
                GameObject scrollObj = new GameObject("GiftScrollView", typeof(RectTransform), typeof(ScrollRect));
                scrollObj.transform.SetParent(transform, false);
                _scrollRect = scrollObj.GetComponent<ScrollRect>();
            }

            RectTransform scrollRt = _scrollRect.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            // Inset scroll rect to leave room for outer frame border
            scrollRt.offsetMin = new Vector2(20f, 18f);
            scrollRt.offsetMax = new Vector2(-20f, -18f);

            _scrollRect.horizontal = false;
            _scrollRect.vertical = false;
            _scrollRect.enabled = false;

            Transform vpTrans = _scrollRect.transform.Find("Viewport");
            GameObject viewport = vpTrans != null ? vpTrans.gameObject : new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(_scrollRect.transform, false);
            RectTransform vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.sizeDelta = Vector2.zero;

            Image vpImg = viewport.GetComponent<Image>();
            if (vpImg != null)
            {
                vpImg.color = Color.clear;
                vpImg.enabled = false;
            }

            Mask vpMask = viewport.GetComponent<Mask>();
            if (vpMask != null)
            {
                vpMask.showMaskGraphic = false;
                vpMask.enabled = false;
            }

            _scrollRect.viewport = vpRt;

            Transform contentTrans = viewport.transform.Find("Content");
            GameObject content = contentTrans != null ? contentTrans.gameObject : new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            _contentContainer = content.GetComponent<RectTransform>();
            _contentContainer.anchorMin = Vector2.zero;
            _contentContainer.anchorMax = Vector2.one;
            _contentContainer.offsetMin = Vector2.zero;
            _contentContainer.offsetMax = Vector2.zero;
            _contentContainer.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup hlg = _contentContainer.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(hlg); else Destroy(hlg);
#else
                Destroy(hlg);
#endif
            }

            VerticalLayoutGroup vlg = _contentContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(vlg); else Destroy(vlg);
#else
                Destroy(vlg);
#endif
            }

            ContentSizeFitter csf = _contentContainer.GetComponent<ContentSizeFitter>();
            if (csf != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(csf); else Destroy(csf);
#else
                Destroy(csf);
#endif
            }

            int rows = Mathf.Max(1, _rowCount);
            int cols = Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)rows));

            GridLayoutGroup glg = _contentContainer.GetComponent<GridLayoutGroup>();
            if (glg == null) glg = _contentContainer.gameObject.AddComponent<GridLayoutGroup>();

            glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
            glg.startAxis = GridLayoutGroup.Axis.Horizontal;
            glg.childAlignment = TextAnchor.MiddleCenter;
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = cols;
            glg.padding = new RectOffset(8, 8, 6, 6);
            glg.spacing = new Vector2(_cardSpacing, 6f);

            _scrollRect.content = _contentContainer;
        }

        /// <summary>
        /// Creates an individual gift card item with interactive Button support.
        /// </summary>
        private void CreateOriginalCard(TikTokGiftMapping mapping, float cardWidth, float cardHeight)
        {
            GameObject cardObj = new GameObject($"Card_{mapping.giftName}", typeof(RectTransform), typeof(Image), typeof(Button));
            cardObj.transform.SetParent(_contentContainer, false);
            RectTransform cardRt = cardObj.GetComponent<RectTransform>();

            Image bg = cardObj.GetComponent<Image>();
            // Card cell styled with dark panel from HudTheme tinted by gift category (Blue/Red/Special/Like).
            HudTheme theme = HudTheme.Current;
            HudCategory category = GetCategory(mapping.action);
            Sprite flat = theme.panelFlat != null ? theme.panelFlat : theme.panelDark;
            if (flat != null)
            {
                bg.sprite = flat;
                bg.type = Image.Type.Sliced;
            }
            // Outline border attached to the Body layer; base card Image kept clear for raycasts.
            bg.color = Color.clear;

            GameObject bodyObj = new GameObject("Body", typeof(RectTransform), typeof(Image));
            bodyObj.transform.SetParent(cardObj.transform, false);
            RectTransform bodyRt = bodyObj.GetComponent<RectTransform>();
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(4f, 4f);
            bodyRt.offsetMax = new Vector2(-4f, -4f);
            Image bodyImg = bodyObj.GetComponent<Image>();
            if (flat != null)
            {
                bodyImg.sprite = flat;
                bodyImg.type = Image.Type.Sliced;
            }
            Color fill = theme.CategoryFill(category);
            fill.a = 1f; // Fully opaque so rear outline color does not bleed through
            bodyImg.color = fill;
            bodyImg.raycastTarget = false;
            Outline border = bodyObj.AddComponent<Outline>();
            border.effectColor = theme.CategoryBorder(category);
            border.effectDistance = new Vector2(4f, -4f);
            border.useGraphicAlpha = false;
            bodyObj.transform.SetAsFirstSibling();

            // 1. TikTok Gift Icon (top half of card if enabled)
            if (_showGiftIcons)
            {
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(cardObj.transform, false);
                RectTransform iconRt = iconObj.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 0.65f);
                iconRt.anchorMax = new Vector2(0.5f, 0.65f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;

                float iconSize = Mathf.Clamp(cardHeight * 0.46f, 38f, 52f);
                iconRt.sizeDelta = new Vector2(iconSize, iconSize);

                Image iconImg = iconObj.GetComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false; // Do not block button click events
                Sprite resolvedIcon = mapping.giftIcon != null ? mapping.giftIcon : GetOfficialTikTokGiftIcon(mapping.giftId, mapping.giftName);
                iconImg.sprite = resolvedIcon != null ? resolvedIcon : _fallbackIcon;
            }

            // 2. Description Label (fills entire card if icons hidden, or bottom half if icons shown)
            GameObject descObj = new GameObject("DescLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            descObj.transform.SetParent(cardObj.transform, false);
            RectTransform descRt = descObj.GetComponent<RectTransform>();
            if (_showGiftIcons)
            {
                descRt.anchorMin = new Vector2(0.04f, 0.05f);
                descRt.anchorMax = new Vector2(0.96f, 0.38f);
            }
            else
            {
                descRt.anchorMin = new Vector2(0.06f, 0.06f);
                descRt.anchorMax = new Vector2(0.94f, 0.94f);
            }
            descRt.offsetMin = Vector2.zero;
            descRt.offsetMax = Vector2.zero;
            descRt.anchoredPosition = Vector2.zero;

            TextMeshProUGUI descTxt = descObj.GetComponent<TextMeshProUGUI>();
            descTxt.text = GetConciseDesc(mapping);
            descTxt.fontStyle = FontStyles.Bold;
            descTxt.alignment = TextAlignmentOptions.Center;
            descTxt.color = GetTeamColor(mapping.action);
            descTxt.textWrappingMode = TextWrappingModes.Normal;
            descTxt.lineSpacing = -8f;
            descTxt.enableAutoSizing = true;
            descTxt.fontSizeMin = 8.5f;
            descTxt.fontSizeMax = _showGiftIcons ? 13.5f : 16f;
            descTxt.raycastTarget = false; // Do not block button click events

            // 3. Configure interactive Button for gift card
            Button cardBtn = cardObj.GetComponent<Button>();
            cardBtn.targetGraphic = bg;
            ColorBlock colors = cardBtn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            cardBtn.colors = colors;

            TikTokGiftMapping capturedMapping = mapping;
            cardBtn.onClick.RemoveAllListeners();
            cardBtn.onClick.AddListener(() =>
            {
                TriggerGiftEvent(capturedMapping);
            });

            RegisterCardMaps(mapping, cardRt);
        }

        /// <summary>
        /// Directly executes gift action when user clicks the gift card button on UI.
        /// </summary>
        public void TriggerGiftEvent(TikTokGiftMapping mapping)
        {
            if (mapping == null) return;

            // 1. Gift card bounce animation
            HighlightGift(mapping.giftId, mapping.giftName);

            // 2. Directly trigger gift action via TikTokGiftRouter
            var router = FindFirstObjectByType<TikTokGiftRouter>();
            if (router != null)
            {
                router.TriggerGiftMappingDirect(mapping, "Streamer");
            }
            else
            {
                // Fallback to EventBus if router is unassigned
                EventBus.Publish(new TikTokGiftEvent(
                    userId: "streamer_tester",
                    displayName: "Streamer",
                    giftId: mapping.giftId,
                    giftName: mapping.giftName,
                    giftIconUrl: null,
                    coins: 10,
                    repeatCount: 1,
                    totalCoins: 10
                ));
            }
        }

        private void RegisterCardMaps(TikTokGiftMapping mapping, RectTransform cardRt)
        {
            string key = (mapping.giftName ?? "").ToLowerInvariant();
            if (!string.IsNullOrEmpty(key) && !_cardMap.ContainsKey(key))
            {
                _cardMap.Add(key, cardRt);
            }
            if (mapping.giftId > 0 && !_cardIdMap.ContainsKey(mapping.giftId))
            {
                _cardIdMap.Add(mapping.giftId, cardRt);
            }
        }

        [ContextMenu("Display in Element Order (Left -> Right, Top -> Bottom)")]
        public void SetToElementOrder()
        {
            _sortOption = GiftSortOption.ByElementOrder;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            BuildGiftDisplay();
            Debug.Log("<color=#00FF88>[GiftInfoPanel] Switched to element order display (left to right, top to bottom).</color>");
        }

        [ContextMenu("Sync Gift Order to TikTokLiveClient")]
        public void SyncSortedOrderToClient()
        {
            if (_liveClient == null || _liveClient.GiftMappings == null) return;
            var sorted = GetSortedMappings(_liveClient.GiftMappings);
            _liveClient.GiftMappings.Clear();
            _liveClient.GiftMappings.AddRange(sorted);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(_liveClient);
#endif
            BuildGiftDisplay();
            Debug.Log("<color=#00FF88>[GiftInfoPanel] Synchronized faction gift order to TikTokLiveClient.</color>");
        }

        public Color GetTeamColor(GiftActionType action)
        {
            return HudTheme.Current.CategoryText(GetCategory(action));
        }

        // Categorize gift to determine background/border/text colors: Blue / Red / Like (neutral) / Special (gold).
        private static HudCategory GetCategory(GiftActionType action)
        {
            switch (action)
            {
                case GiftActionType.Blue_Shield:
                case GiftActionType.Blue_SpeedBoost:
                case GiftActionType.Blue_FreeControl:
                case GiftActionType.Blue_EnergyBottle:
                    return HudCategory.Blue;

                case GiftActionType.Red_SpawnPickup:
                case GiftActionType.Red_SpawnHeavyTruck:
                case GiftActionType.Red_UnlimitedCars:
                case GiftActionType.Red_EnergyBottle:
                    return HudCategory.Red;

                case GiftActionType.Like_Energy:
                    return HudCategory.Neutral;

                default:
                    return HudCategory.Special;
            }
        }

        public string GetConciseDesc(TikTokGiftMapping mapping)
        {
            if (mapping == null) return string.Empty;

            if (!string.IsNullOrWhiteSpace(mapping.description))
            {
                return mapping.description.Trim();
            }

            return mapping.action switch
            {
                GiftActionType.Blue_Shield => "Shield 15s",
                GiftActionType.Blue_SpeedBoost => "Turbo Speed",
                GiftActionType.Blue_FreeControl => "Free Controls",
                GiftActionType.Blue_EnergyBottle => "+300 Energy",
                GiftActionType.Red_SpawnPickup => "Animals",
                GiftActionType.Red_SpawnHeavyTruck => "Train",
                GiftActionType.Red_UnlimitedCars => "Unlimited Cars",
                GiftActionType.Red_EnergyBottle => "+500 Energy",
                GiftActionType.Special_GiftDance => "Dance",
                GiftActionType.Special_RainHazard => "Rain Hazard",
                GiftActionType.Follow_Runner => "Runner",
                GiftActionType.Special_VIPRelayTicket => "Runner",
                GiftActionType.Like_Energy => "Like: +Energy",
                _ => "Support"
            };
        }

        private Sprite GetOfficialTikTokGiftIcon(int giftId, string giftName)
        {
            Sprite resolved = PreGameConfig.PreGameGiftIconResolver.ResolveGiftIcon(giftId, giftName);
            return resolved != null ? resolved : _fallbackIcon;
        }

        /// <summary>
        /// Automatically downloads and updates live stream gift icon by Gift ID and iconUrl.
        /// </summary>
        public void UpdateGiftIconFromLive(int giftId, string giftName, string iconUrl)
        {
            if (string.IsNullOrEmpty(iconUrl) || giftId <= 0) return;

            StartCoroutine(DownloadAndApplyGiftIconRoutine(giftId, iconUrl));
        }

        private IEnumerator DownloadAndApplyGiftIconRoutine(int giftId, string iconUrl)
        {
            using (var uwr = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(iconUrl))
            {
                yield return uwr.SendWebRequest();

                if (uwr.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Texture2D tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(uwr);
                    if (tex != null)
                    {
                        Sprite newSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

                        if (_cardIdMap.TryGetValue(giftId, out var cardRt) && cardRt != null)
                        {
                            var img = cardRt.Find("Icon")?.GetComponent<Image>();
                            if (img != null)
                            {
                                img.sprite = newSprite;
                                img.color = Color.white;
                            }
                        }

#if UNITY_EDITOR
                        try
                        {
                            string dir = "Assets/_Project/Textures/TikTokGifts";
                            string savePath = $"{dir}/{giftId}.png";
                            if (!System.IO.File.Exists(savePath))
                            {
                                System.IO.File.WriteAllBytes(savePath, tex.EncodeToPNG());
                                UnityEditor.AssetDatabase.ImportAsset(savePath);
                                var importer = UnityEditor.AssetImporter.GetAtPath(savePath) as UnityEditor.TextureImporter;
                                if (importer != null)
                                {
                                    importer.textureType = UnityEditor.TextureImporterType.Sprite;
                                    importer.alphaIsTransparency = true;
                                    importer.SaveAndReimport();
                                }
                                Debug.Log($"<color=#00FF88>[GiftInfoPanel] Cached live stream gift icon for ID {giftId} -> {savePath}</color>");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[GiftInfoPanel] Error caching gift icon {giftId}: {ex.Message}");
                        }
#endif
                    }
                }
            }
        }

        public void HighlightGift(int giftId, string giftName)
        {
            RectTransform targetCard = null;

            if (giftId > 0 && _cardIdMap.TryGetValue(giftId, out var byId))
            {
                targetCard = byId;
            }
            else if (!string.IsNullOrEmpty(giftName) && _cardMap.TryGetValue(giftName.ToLowerInvariant(), out var byName))
            {
                targetCard = byName;
            }

            if (targetCard != null)
            {
                StopCoroutine("AnimateCardBounceRoutine");
                StartCoroutine(AnimateCardBounceRoutine(targetCard));
            }
        }

        private IEnumerator AnimateCardBounceRoutine(RectTransform card)
        {
            if (card == null) yield break;

            Vector3 origScale = Vector3.one;
            float elapsed = 0f;
            float duration = 0.35f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.22f;
                card.localScale = origScale * scale;
                yield return null;
            }

            card.localScale = origScale;
        }
    }
}

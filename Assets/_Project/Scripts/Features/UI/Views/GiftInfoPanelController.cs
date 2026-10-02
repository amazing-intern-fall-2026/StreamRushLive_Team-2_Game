using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SteamRush.Features.StreamIntegration;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Sorting options for gift cards on UI.
    /// </summary>
    public enum GiftSortOption
    {
        [InspectorName("Team: Blue -> Red -> Special")]
        ByTeam_BlueRedSpecial = 0,

        [InspectorName("Team: Red -> Blue -> Special")]
        ByTeam_RedBlueSpecial = 1,

        [InspectorName("By Gift ID")]
        ByGiftId = 2,

        [InspectorName("By Value")]
        ByValue = 3,

        [InspectorName("None")]
        None = 4
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

        [Tooltip("Auto-sort mode for gift cards on UI.")]
        [SerializeField] private GiftSortOption _sortOption = GiftSortOption.ByTeam_BlueRedSpecial;

        [Tooltip("Auto-arrange and re-align gift cards when modified in Inspector or Runtime.")]
        [SerializeField] private bool _autoArrangeOnChanged = true;

        [Header("Card Configuration")]
        [SerializeField] private float _cardSpacing = 8f;
        [SerializeField] private Color _cardBgColor = new Color(0.10f, 0.12f, 0.18f, 0.85f);

        [Header("Team Colors")]
        [SerializeField] private Color _blueTeamColor = new Color(0.0f, 0.90f, 1.0f, 1f);   // Neon Cyan Blue
        [SerializeField] private Color _redTeamColor = new Color(1.0f, 0.22f, 0.28f, 1f);   // Neon Crimson Red
        [SerializeField] private Color _specialColor = new Color(1.0f, 0.85f, 0.15f, 1f);   // Neon Gold Yellow

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
                ClearAllCards();
                if (placeholder != null) placeholder.gameObject.SetActive(true);
                return;
            }

            if (placeholder != null)
            {
                placeholder.gameObject.SetActive(false);
            }

            // 1. TỰ ĐỘNG SẮP XẾP THỨ TỰ (SORT ORDER)
            List<TikTokGiftMapping> sortedMappings = GetSortedMappings(rawMappings);
            int count = sortedMappings.Count;

            // 2. CẤU HÌNH KHUNG NỀN NGUYÊN BẢN (KHÔNG CHỨA BẤT KỲ MẢNG TRẮNG NÀO)
            SetupOriginalContainer(count);

            if (_contentContainer == null) return;

            // 3. XÓA SẠCH CÁC THẺ CŨ
            ClearAllCards();

            // 4. TÍNH TOÁN KÍCH THƯỚC ĐỂ VỪA KHÍT 100% KHUNG THEO SỐ HÀNG
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
                glg.cellSize = new Vector2(cardW, cardH);
                glg.constraintCount = cols;
                glg.spacing = new Vector2(spX, spY);
                glg.padding = new RectOffset((int)padX, (int)padX, (int)padY, (int)padY);
            }

            // 5. SINH TỪNG THẺ QUÀ
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

                case GiftSortOption.None:
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
            RectTransform panelRt = GetComponent<RectTransform>();
            if (panelRt != null)
            {
                if (_rowCount >= 2)
                {
                    // Multi-row: expanded height positioned comfortably above the debug bar
                    panelRt.anchorMin = new Vector2(0f, 0.102f);
                    panelRt.anchorMax = new Vector2(1f, 0.238f);
                    panelRt.offsetMin = new Vector2(20f, 4f);
                    panelRt.offsetMax = new Vector2(-20f, -4f);
                }
                else
                {
                    panelRt.anchorMin = new Vector2(0f, 0.10f);
                    panelRt.anchorMax = new Vector2(1f, 0.20f);
                    panelRt.offsetMin = new Vector2(20f, 8f);
                    panelRt.offsetMax = new Vector2(-20f, -8f);
                }
                panelRt.pivot = new Vector2(0.5f, 0.5f);
            }

            Image panelImg = GetComponent<Image>();
            if (panelImg != null)
            {
                Sprite pill = FindSpriteInAssets("UI_BannerPill");
                if (pill != null) panelImg.sprite = pill;
                panelImg.type = Image.Type.Sliced;
                panelImg.color = new Color(0.07f, 0.07f, 0.10f, 0.55f);
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
            scrollRt.offsetMin = new Vector2(8f, 6f);
            scrollRt.offsetMax = new Vector2(-8f, -6f);

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
        /// Creates an individual gift card item.
        /// </summary>
        private void CreateOriginalCard(TikTokGiftMapping mapping, float cardWidth, float cardHeight)
        {
            GameObject cardObj = new GameObject($"Card_{mapping.giftName}", typeof(RectTransform), typeof(Image));
            cardObj.transform.SetParent(_contentContainer, false);
            RectTransform cardRt = cardObj.GetComponent<RectTransform>();

            Image bg = cardObj.GetComponent<Image>();
            bg.color = _cardBgColor;

            Sprite pillSprite = FindSpriteInAssets("UI_BannerPill") ?? FindSpriteInAssets("UI_Pill");
            if (pillSprite != null)
            {
                bg.sprite = pillSprite;
                bg.type = Image.Type.Sliced;
            }

            var outline = cardObj.GetComponent<Outline>();
            if (outline == null) outline = cardObj.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.08f);
            outline.effectDistance = new Vector2(1f, -1f);

            // 1. Ảnh Gift Chính Thức Của TikTok (Nằm nửa trên thẻ)
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
            Sprite resolvedIcon = mapping.giftIcon != null ? mapping.giftIcon : GetOfficialTikTokGiftIcon(mapping.giftId, mapping.giftName);
            iconImg.sprite = resolvedIcon != null ? resolvedIcon : _fallbackIcon;

            // 2. Dòng Mô Tả Ngắn Gọn Dễ Hiểu (Nằm nửa dưới thẻ, Chữ To, Đậm, Đúng Màu Phe)
            GameObject descObj = new GameObject("DescLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            descObj.transform.SetParent(cardObj.transform, false);
            RectTransform descRt = descObj.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0.04f, 0.05f);
            descRt.anchorMax = new Vector2(0.96f, 0.38f);
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
            descTxt.fontSizeMax = 13.5f;

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

        [ContextMenu("Đồng Bộ Thứ Tự Quà Vào TikTokLiveClient")]
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
            Debug.Log("<color=#00FF88>[GiftInfoPanel] Đã đồng bộ thứ tự quà tặng theo phe vào TikTokLiveClient.</color>");
        }

        public Color GetTeamColor(GiftActionType action)
        {
            switch (action)
            {
                case GiftActionType.Blue_Shield:
                case GiftActionType.Blue_SpeedBoost:
                case GiftActionType.Blue_FreeControl:
                case GiftActionType.Blue_EnergyBottle:
                    return _blueTeamColor;

                case GiftActionType.Red_SpawnPickup:
                case GiftActionType.Red_SpawnHeavyTruck:
                case GiftActionType.Red_UnlimitedCars:
                case GiftActionType.Red_EnergyBottle:
                    return _redTeamColor;

                case GiftActionType.Like_Energy:
                    return Color.white;

                default:
                    return _specialColor;
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
                GiftActionType.Red_SpawnPickup => "Pickup Truck",
                GiftActionType.Red_SpawnHeavyTruck => "Heavy Truck",
                GiftActionType.Red_UnlimitedCars => "Unlimited Cars",
                GiftActionType.Red_EnergyBottle => "+500 Energy",
                GiftActionType.Special_GiftDance => "Meme Dance",
                GiftActionType.Special_RainHazard => "Rain Hazard",
                GiftActionType.Special_VIPRelayTicket => "VIP Runner",
                GiftActionType.Like_Energy => "Like: +Energy",
                _ => "Support"
            };
        }

        private Sprite GetOfficialTikTokGiftIcon(int giftId, string giftName)
        {
            string dir = "Assets/_Project/Textures/TikTokGifts";

#if UNITY_EDITOR
            if (giftId > 0)
            {
                string idPath = $"{dir}/{giftId}.png";
                Sprite idSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(idPath);
                if (idSprite != null) return idSprite;
            }

            string lower = (giftName ?? "").ToLowerInvariant();
            if (lower.Contains("like") || lower.Contains("tap") || lower.Contains("heart") || lower.Contains("tim")) return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/heart.png");
            if (lower.Contains("rose") || lower.Contains("hoa")) return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/rose.png");
            if (lower.Contains("tiktok")) return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/tiktok.png");
            if (lower.Contains("heart") || lower.Contains("tim")) return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/heart.png");
            if (lower.Contains("dumbbell") || lower.Contains("tạ") || lower.Contains("weight")) return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/dumbbell.png");
            if (lower.Contains("donut") || lower.Contains("bánh")) return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/donut.png");
            if (lower.Contains("cap") || lower.Contains("mũ")) return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/cap.png");
            if (lower.Contains("lion") || lower.Contains("sư tử")) return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/lion.png");
            if (lower.Contains("dance") || lower.Contains("nhảy")) return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/dance.png");
            if (lower.Contains("vip") || lower.Contains("vé") || lower.Contains("star")) return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/vip.png");
            if (lower.Contains("rain") || lower.Contains("mưa")) return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/rain.png");
#endif
            return _fallbackIcon;
        }

        /// <summary>
        /// Tự động cập nhật ảnh món quà trực tiếp từ phòng Live theo Gift ID và iconUrl
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
                                Debug.Log($"<color=#00FF88>[GiftInfoPanel] Đã tự động lưu ảnh món quà mới theo Gift ID {giftId} -> {savePath}</color>");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[GiftInfoPanel] Lỗi lưu cache icon quà {giftId}: {ex.Message}");
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

        private Sprite FindSpriteInAssets(string spriteName)
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"{spriteName} t:Sprite");
            foreach (var g in guids)
            {
                string p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                if (p.EndsWith($"{spriteName}.png") || p.Contains($"/{spriteName}.png"))
                {
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
                }
            }
#endif
            return null;
        }
    }
}

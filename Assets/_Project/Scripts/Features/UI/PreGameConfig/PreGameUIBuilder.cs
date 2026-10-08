using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SteamRush.Features.UI;
using SteamRush.Features.Backend;

namespace SteamRush.Features.UI.PreGameConfig
{
    public static class PreGameUIBuilder
    {
        private static TMP_FontAsset GetFont()
        {
            var theme = HudTheme.Current;
            if (theme != null && theme.fontBold != null) return theme.fontBold;
#if UNITY_EDITOR
            var font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Baloo2-Bold SDF.asset");
            if (font != null) return font;
#endif
            return Resources.Load<TMP_FontAsset>("Fonts/Baloo2-Bold SDF");
        }

        private static Sprite LoadSprite(string path)
        {
#if UNITY_EDITOR
            var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) return sp;
#endif
            string filename = System.IO.Path.GetFileNameWithoutExtension(path);
            return Resources.Load<Sprite>(filename);
        }

        private static Sprite GetFolderSprite()
        {
            return LoadSprite("Assets/_Project/Textures/Icons/icon_folder.png");
        }

        private static Sprite GetGlobeSprite()
        {
            return LoadSprite("Assets/_Project/Textures/Icons/icon_globe.png");
        }

        private static Sprite GetRestartSprite()
        {
            return LoadSprite("Assets/Hyper_Casual_UI/Sprites/Icons/codicon_debug-restart.png");
        }

        private static Sprite GetToggleOnSprite()
        {
            return LoadSprite("Assets/Hyper_Casual_UI/Sprites/Toggle/Toggle_ON.png");
        }

        private static Sprite GetToggleOffSprite()
        {
            return LoadSprite("Assets/Hyper_Casual_UI/Sprites/Toggle/Toggle_Off.png");
        }

        private static Sprite GetCloseSprite()
        {
            return LoadSprite("Assets/Hyper_Casual_UI/Sprites/Icons/Close.png");
        }

        /// <summary>
        /// Universally resolves TikTok gift icon sprite by giftId and/or giftName with fallback chains.
        /// </summary>
        public static Sprite ResolveGiftIcon(int giftId, string giftName)
        {
            var router = UnityEngine.Object.FindFirstObjectByType<StreamIntegration.TikTokGiftRouter>();
            if (router != null && router.GiftMappings != null)
            {
                var found = router.GiftMappings.Find(m =>
                    (giftId > 0 && m.giftId == giftId) ||
                    (!string.IsNullOrEmpty(giftName) && string.Equals(m.giftName, giftName, StringComparison.OrdinalIgnoreCase)));
                if (found != null && found.giftIcon != null) return found.giftIcon;
            }

#if UNITY_EDITOR
            string dir = "Assets/_Project/Textures/TikTokGifts";
            if (giftId > 0)
            {
                var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{giftId}.png");
                if (sp != null) return sp;
            }

            if (!string.IsNullOrEmpty(giftName))
            {
                string lower = giftName.ToLowerInvariant().Trim();
                var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{lower}.png");
                if (sp != null) return sp;

                // Keyword fallback matching
                if (lower.Contains("heart") || lower.Contains("like")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5487.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/heart.png");
                else if (lower.Contains("rose")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5655.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/rose.png");
                else if (lower.Contains("tiktok") || lower.Contains("speed")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5269.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/tiktok.png");
                else if (lower.Contains("cap") || lower.Contains("hat") || lower.Contains("freedom")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5879.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/cap.png");
                else if (lower.Contains("donut")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5338.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/donut.png");
                else if (lower.Contains("dumbbell") || lower.Contains("beast") || lower.Contains("pickup")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5585.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/dumbbell.png");
                else if (lower.Contains("lion") || lower.Contains("train")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/6001.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/lion.png");
                else if (lower.Contains("sunglasses") || lower.Contains("car storm") || lower.Contains("unlimited")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5661.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/sunglasses.png");
                else if (lower.Contains("chili")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5586.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/chili.png");
                else if (lower.Contains("dance") || lower.Contains("meme")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/6037.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/dance.png");
                else if (lower.Contains("rain") || lower.Contains("weather")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/5978.png") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/rain.png");
                else if (lower.Contains("follow") || lower.Contains("runner") || lower.Contains("next")) sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/follow.png");

                if (sp != null) return sp;
            }
#endif

            if (giftId > 0)
            {
                var sp = Resources.Load<Sprite>($"TikTokGifts/{giftId}");
                if (sp != null) return sp;
            }
            if (!string.IsNullOrEmpty(giftName))
            {
                var sp = Resources.Load<Sprite>($"TikTokGifts/{giftName.ToLowerInvariant().Trim()}");
                if (sp != null) return sp;
            }

            return null;
        }

        private static Sprite GetInputFieldBgSprite()
        {
            return LoadSprite("Assets/Violet Theme Ui/Buttons/Misc/Input Field Background.png");
        }

        private static Sprite GetGreenPillSprite()
        {
            return LoadSprite("Assets/Hyper_Casual_UI/Sprites/Buttons/empty_buttons/green.png");
        }

        private static Sprite GetIconSprite(string name)
        {
            return LoadSprite($"Assets/Hyper_Casual_UI/Sprites/Icons/{name}.png");
        }

        public static GameObject BuildOrUpdatePreGameConfigUI(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            {
                var allCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (var c in allCanvases)
                {
                    if (c.renderMode != RenderMode.WorldSpace)
                    {
                        canvas = c;
                        break;
                    }
                }
            }

            if (canvas == null)
            {
                Debug.LogError("[PreGameUIBuilder] No ScreenSpace Canvas found in scene!");
                return null;
            }

            var theme = HudTheme.Current;
            var font = GetFont();

            // Clean up existing modal instance if present
            Transform existing = canvas.transform.Find("PreGameConfigModal");
            if (existing != null)
            {
#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
#else
                UnityEngine.Object.Destroy(existing.gameObject);
#endif
            }

            // 1. Root Modal Object
            GameObject modalRoot = new GameObject("PreGameConfigModal", typeof(RectTransform));
            modalRoot.transform.SetParent(canvas.transform, false);
            RectTransform modalRt = modalRoot.GetComponent<RectTransform>();
            modalRt.anchorMin = Vector2.zero;
            modalRt.anchorMax = Vector2.one;
            modalRt.sizeDelta = Vector2.zero;

            // 1b. Dim Backdrop (Click outside closes modal)
            GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdrop.transform.SetParent(modalRoot.transform, false);
            RectTransform bdRt = backdrop.GetComponent<RectTransform>();
            bdRt.anchorMin = Vector2.zero;
            bdRt.anchorMax = Vector2.one;
            bdRt.sizeDelta = Vector2.zero;
            Image bdImg = backdrop.GetComponent<Image>();
            bdImg.color = new Color(0.02f, 0.05f, 0.08f, 0.88f);
            Button bdBtn = backdrop.GetComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(() =>
            {
                var mgr = PreGameConfigManager.Instance;
                if (mgr != null) mgr.CloseConfigUI();
            });

            // 2. Main Window Container (Styled with Hyper-Casual Level screen frame)
            GameObject window = new GameObject("Window", typeof(RectTransform), typeof(Image));
            window.transform.SetParent(modalRoot.transform, false);
            RectTransform winRt = window.GetComponent<RectTransform>();
            winRt.anchorMin = new Vector2(0.5f, 0.5f);
            winRt.anchorMax = new Vector2(0.5f, 0.5f);
            winRt.pivot = new Vector2(0.5f, 0.5f);
            winRt.sizeDelta = new Vector2(850f, 900f);

            Image winImg = window.GetComponent<Image>();
            if (theme != null && theme.panelFrame != null)
            {
                winImg.sprite = theme.panelFrame;
                winImg.type = Image.Type.Sliced;
                winImg.pixelsPerUnitMultiplier = 0.72f;
            }
            winImg.color = Color.white;

            // 3. Header Bar
            GameObject header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(window.transform, false);
            RectTransform headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0f, 80f);
            headerRt.anchoredPosition = new Vector2(0f, -14f);

            // Header Icon (Crown/Setting)
            Sprite headerIconSp = GetIconSprite("setting");
            if (headerIconSp != null)
            {
                GameObject hIconObj = new GameObject("HeaderIcon", typeof(RectTransform), typeof(Image));
                hIconObj.transform.SetParent(header.transform, false);
                RectTransform hIconRt = hIconObj.GetComponent<RectTransform>();
                hIconRt.anchorMin = new Vector2(0.08f, 0.55f);
                hIconRt.anchorMax = new Vector2(0.08f, 0.55f);
                hIconRt.pivot = new Vector2(0.5f, 0.5f);
                hIconRt.sizeDelta = new Vector2(34f, 34f);
                Image hIconImg = hIconObj.GetComponent<Image>();
                hIconImg.sprite = headerIconSp;
                hIconImg.color = new Color32(0xFC, 0xDA, 0x21, 0xFF);
                hIconImg.preserveAspect = true;
                hIconImg.raycastTarget = false;
            }

            var titleTxt = CreateText(header.transform, "TitleText", "GAME CONFIGURATION", 25f, FontStyles.Bold, new Color32(0xFC, 0xDA, 0x21, 0xFF), TextAlignmentOptions.Center, font);
            RectTransform titleRt = titleTxt.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.45f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.sizeDelta = Vector2.zero;

            var subtitleTxt = CreateText(header.transform, "SubtitleText", "Stream Rush Live Setup & Gameplay Controls", 13.5f, FontStyles.Normal, new Color32(0xBF, 0xE3, 0xE8, 0xFF), TextAlignmentOptions.Center, font);
            RectTransform subRt = subtitleTxt.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0f, 0f);
            subRt.anchorMax = new Vector2(1f, 0.45f);
            subRt.sizeDelta = Vector2.zero;

            // Close button (Using circular Close.png sprite)
            GameObject closeBtnObj = CreateCloseButton(header.transform, "BtnClose", new Vector2(38f, 38f), font);
            RectTransform closeRt = closeBtnObj.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-22f, -8f);

            // 4. Tab Bar (Basic vs Advanced)
            GameObject tabBar = new GameObject("TabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabBar.transform.SetParent(window.transform, false);
            RectTransform tabRt = tabBar.GetComponent<RectTransform>();
            tabRt.anchorMin = new Vector2(0.05f, 1f);
            tabRt.anchorMax = new Vector2(0.95f, 1f);
            tabRt.pivot = new Vector2(0.5f, 1f);
            tabRt.anchoredPosition = new Vector2(0f, -96f);
            tabRt.sizeDelta = new Vector2(0f, 44f);

            HorizontalLayoutGroup tabHlg = tabBar.GetComponent<HorizontalLayoutGroup>();
            tabHlg.spacing = 14f;
            tabHlg.childControlWidth = true;
            tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = true;
            tabHlg.childForceExpandHeight = true;

            GameObject btnTabBasic = CreateTabButton(tabBar.transform, "BtnTabBasic", "BASIC SETUP", true, font, GetIconSprite("character"));
            GameObject btnTabAdv = CreateTabButton(tabBar.transform, "BtnTabAdvanced", "ADVANCED SYSTEM", false, font, GetIconSprite("setting"));

            // 5. Inset Content Area (Framed container)
            GameObject contentArea = new GameObject("ContentArea", typeof(RectTransform), typeof(Image));
            contentArea.transform.SetParent(window.transform, false);
            RectTransform caRt = contentArea.GetComponent<RectTransform>();
            caRt.anchorMin = new Vector2(0.04f, 0.11f);
            caRt.anchorMax = new Vector2(0.96f, 0.81f);
            caRt.sizeDelta = Vector2.zero;

            Image caImg = contentArea.GetComponent<Image>();
            if (theme != null && theme.panelInset != null)
            {
                caImg.sprite = theme.panelInset;
                caImg.type = Image.Type.Sliced;
                caImg.pixelsPerUnitMultiplier = 1.0f;
            }
            caImg.color = new Color(0.04f, 0.11f, 0.16f, 0.95f);

            // 5A. Basic Panel Content
            GameObject panelBasic = new GameObject("PanelBasicContent", typeof(RectTransform));
            panelBasic.transform.SetParent(contentArea.transform, false);
            RectTransform pbRt = panelBasic.GetComponent<RectTransform>();
            pbRt.anchorMin = Vector2.zero;
            pbRt.anchorMax = Vector2.one;
            pbRt.offsetMin = new Vector2(12f, 12f);
            pbRt.offsetMax = new Vector2(-12f, -12f);

            // Basic - Section 1: TikTok Username + Check Live Row
            GameObject bannerUser = CreateHeaderBanner(panelBasic.transform, "TIKTOK LIVE HOST CHANNEL", new Color32(0xFC, 0xDA, 0x21, 0xFF), font, GetIconSprite("character"));
            RectTransform buRt = bannerUser.GetComponent<RectTransform>();
            buRt.anchorMin = new Vector2(0f, 1f);
            buRt.anchorMax = new Vector2(1f, 1f);
            buRt.pivot = new Vector2(0f, 1f);
            buRt.anchoredPosition = new Vector2(0f, 0f);

            GameObject userRow = new GameObject("UserRow", typeof(RectTransform));
            userRow.transform.SetParent(panelBasic.transform, false);
            RectTransform urRt = userRow.GetComponent<RectTransform>();
            urRt.anchorMin = new Vector2(0f, 1f);
            urRt.anchorMax = new Vector2(1f, 1f);
            urRt.pivot = new Vector2(0f, 1f);
            urRt.anchoredPosition = new Vector2(0f, -42f);
            urRt.sizeDelta = new Vector2(0f, 40f);

            GameObject inputUserObj = CreateInputField(userRow.transform, "InputUsername", "Enter TikTok live @username...", 14f, font);
            RectTransform iuRt = inputUserObj.GetComponent<RectTransform>();
            iuRt.anchorMin = new Vector2(0f, 0f);
            iuRt.anchorMax = new Vector2(0.56f, 1f);
            iuRt.offsetMin = Vector2.zero;
            iuRt.offsetMax = new Vector2(-6f, 0f);

            GameObject btnCheckLiveObj = CreatePillButton(userRow.transform, "BtnCheckLive", "CHECK LIVE", theme?.pillBlue, Color.white, 12f, font, GetIconSprite("setting"));
            RectTransform clRt = btnCheckLiveObj.GetComponent<RectTransform>();
            clRt.anchorMin = new Vector2(0.56f, 0f);
            clRt.anchorMax = new Vector2(0.77f, 1f);
            clRt.offsetMin = new Vector2(4f, 0f);
            clRt.offsetMax = new Vector2(-4f, 0f);

            GameObject statusBadge = new GameObject("LiveStatusBadge", typeof(RectTransform), typeof(Image));
            statusBadge.transform.SetParent(userRow.transform, false);
            RectTransform sbRt = statusBadge.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(0.77f, 0f);
            sbRt.anchorMax = new Vector2(1f, 1f);
            sbRt.offsetMin = new Vector2(4f, 0f);
            sbRt.offsetMax = Vector2.zero;

            Image sbImg = statusBadge.GetComponent<Image>();
            if (theme != null && theme.panelDark != null)
            {
                sbImg.sprite = theme.panelDark;
                sbImg.type = Image.Type.Sliced;
            }
            sbImg.color = new Color(0.06f, 0.16f, 0.24f, 0.95f);

            var txtLiveStatus = CreateText(statusBadge.transform, "TxtLiveStatus", "<color=#88A0B0>NOT VERIFIED</color>", 11.5f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, font);
            RectTransform lsRt = txtLiveStatus.GetComponent<RectTransform>();
            lsRt.anchorMin = Vector2.zero;
            lsRt.anchorMax = Vector2.one;
            lsRt.offsetMin = Vector2.zero;
            lsRt.offsetMax = Vector2.zero;

            // Basic - Section 2: Target Distance
            GameObject bannerDist = CreateHeaderBanner(panelBasic.transform, "TARGET DISTANCE & BATON RELAY", new Color32(0x40, 0xAD, 0xFF, 0xFF), font, GetIconSprite("flag"));
            RectTransform bdDistRt = bannerDist.GetComponent<RectTransform>();
            bdDistRt.anchorMin = new Vector2(0f, 1f);
            bdDistRt.anchorMax = new Vector2(1f, 1f);
            bdDistRt.pivot = new Vector2(0f, 1f);
            bdDistRt.anchoredPosition = new Vector2(0f, -92f);

            GameObject distRow = new GameObject("DistanceRow", typeof(RectTransform));
            distRow.transform.SetParent(panelBasic.transform, false);
            RectTransform drRt = distRow.GetComponent<RectTransform>();
            drRt.anchorMin = new Vector2(0f, 1f);
            drRt.anchorMax = new Vector2(1f, 1f);
            drRt.pivot = new Vector2(0f, 1f);
            drRt.anchoredPosition = new Vector2(0f, -134f);
            drRt.sizeDelta = new Vector2(0f, 40f);

            GameObject inputDistObj = CreateInputField(distRow.transform, "InputTargetDistance", "Enter target distance (km)... e.g., 1 or 2.5", 14f, font);
            RectTransform idRt = inputDistObj.GetComponent<RectTransform>();
            idRt.anchorMin = new Vector2(0f, 0f);
            idRt.anchorMax = new Vector2(0.66f, 1f);
            idRt.offsetMin = Vector2.zero;
            idRt.offsetMax = Vector2.zero;

            GameObject togInfRow = CreateToggleField(distRow.transform, "Infinite Run:", false, font);
            RectTransform tiRt = togInfRow.GetComponent<RectTransform>();
            tiRt.anchorMin = new Vector2(0.68f, 0f);
            tiRt.anchorMax = new Vector2(1f, 1f);
            tiRt.offsetMin = Vector2.zero;
            tiRt.offsetMax = Vector2.zero;

            // Basic - Section 3: Gifts List
            GameObject bannerGifts = CreateHeaderBanner(panelBasic.transform, "STREAM GIFTS & INTERACTION TRIGGERS", new Color32(0x00, 0xE5, 0xFF, 0xFF), font, GetIconSprite("gift"));
            RectTransform bgRt = bannerGifts.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 1f);
            bgRt.anchorMax = new Vector2(1f, 1f);
            bgRt.pivot = new Vector2(0f, 1f);
            bgRt.anchoredPosition = new Vector2(0f, -184f);

            // Table Column Header (Clean card header)
            GameObject tableHeader = new GameObject("TableHeader", typeof(RectTransform), typeof(Image));
            tableHeader.transform.SetParent(panelBasic.transform, false);
            RectTransform thRt = tableHeader.GetComponent<RectTransform>();
            thRt.anchorMin = new Vector2(0f, 1f);
            thRt.anchorMax = new Vector2(1f, 1f);
            thRt.pivot = new Vector2(0f, 1f);
            thRt.anchoredPosition = new Vector2(0f, -226f);
            thRt.sizeDelta = new Vector2(0f, 28f);

            Image thBg = tableHeader.GetComponent<Image>();
            if (theme != null && theme.panelDark != null)
            {
                thBg.sprite = theme.panelDark;
                thBg.type = Image.Type.Sliced;
            }
            thBg.color = new Color(0.08f, 0.20f, 0.28f, 0.95f);

            var thActive = CreateText(tableHeader.transform, "ThActive", "ACTIVE", 11f, FontStyles.Bold, new Color32(0x00, 0xE5, 0xFF, 0xFF), TextAlignmentOptions.Center, font);
            SetRect(thActive.GetComponent<RectTransform>(), 8f, 0f, 60f, 28f);

            var thFeat = CreateText(tableHeader.transform, "ThFeature", "FEATURE ACTION", 11f, FontStyles.Bold, new Color32(0x00, 0xE5, 0xFF, 0xFF), TextAlignmentOptions.MidlineLeft, font);
            SetRect(thFeat.GetComponent<RectTransform>(), 74f, 0f, 142f, 28f);

            var thGift = CreateText(tableHeader.transform, "ThGift", "TIKTOK GIFT", 11f, FontStyles.Bold, new Color32(0x00, 0xE5, 0xFF, 0xFF), TextAlignmentOptions.MidlineLeft, font);
            SetRect(thGift.GetComponent<RectTransform>(), 222f, 0f, 145f, 28f);

            var thStat = CreateText(tableHeader.transform, "ThStat", "STAT", 11f, FontStyles.Bold, new Color32(0x00, 0xE5, 0xFF, 0xFF), TextAlignmentOptions.Center, font);
            SetRect(thStat.GetComponent<RectTransform>(), 372f, 0f, 72f, 28f);

            var thDesc = CreateText(tableHeader.transform, "ThDesc", "CARD DESCRIPTION", 11f, FontStyles.Bold, new Color32(0x00, 0xE5, 0xFF, 0xFF), TextAlignmentOptions.MidlineLeft, font);
            SetRect(thDesc.GetComponent<RectTransform>(), 450f, 0f, 194f, 28f);

            var thOrder = CreateText(tableHeader.transform, "ThOrder", "ORDER", 11f, FontStyles.Bold, new Color32(0x00, 0xE5, 0xFF, 0xFF), TextAlignmentOptions.Center, font);
            SetRect(thOrder.GetComponent<RectTransform>(), 648f, 0f, 58f, 28f);

            // ScrollView for gifts
            GameObject scrollObj = new GameObject("GiftsScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollObj.transform.SetParent(panelBasic.transform, false);
            RectTransform svRt = scrollObj.GetComponent<RectTransform>();
            svRt.anchorMin = new Vector2(0f, 0f);
            svRt.anchorMax = new Vector2(1f, 1f);
            svRt.offsetMin = new Vector2(0f, 0f);
            svRt.offsetMax = new Vector2(0f, -258f);

            Image svBg = scrollObj.GetComponent<Image>();
            svBg.color = new Color(0.03f, 0.09f, 0.14f, 0.85f);

            ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.scrollSensitivity = 40f;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.sizeDelta = Vector2.zero;
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            sr.viewport = vpRt;

            GameObject giftsContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            giftsContent.transform.SetParent(viewport.transform, false);
            RectTransform gcRt = giftsContent.GetComponent<RectTransform>();
            gcRt.anchorMin = new Vector2(0f, 1f);
            gcRt.anchorMax = new Vector2(1f, 1f);
            gcRt.pivot = new Vector2(0.5f, 1f);
            gcRt.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup gcVlg = giftsContent.GetComponent<VerticalLayoutGroup>();
            gcVlg.spacing = 8f;
            gcVlg.padding = new RectOffset(8, 8, 8, 8);
            gcVlg.childControlWidth = true;
            gcVlg.childControlHeight = false;
            gcVlg.childForceExpandWidth = true;

            ContentSizeFitter gcCsf = giftsContent.GetComponent<ContentSizeFitter>();
            gcCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = gcRt;

            // Template Gift Row
            GameObject giftRowTemplate = CreateGiftRowTemplate(giftsContent.transform, font);
            giftRowTemplate.SetActive(false);

            // 5B. Advanced Panel Content (Full Scroll View)
            GameObject panelAdv = new GameObject("PanelAdvancedContent", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            panelAdv.transform.SetParent(contentArea.transform, false);
            RectTransform paRt = panelAdv.GetComponent<RectTransform>();
            paRt.anchorMin = Vector2.zero;
            paRt.anchorMax = Vector2.one;
            paRt.offsetMin = new Vector2(10f, 10f);
            paRt.offsetMax = new Vector2(-10f, -10f);

            panelAdv.GetComponent<Image>().color = new Color(0.03f, 0.09f, 0.14f, 0.85f);
            ScrollRect srAdv = panelAdv.GetComponent<ScrollRect>();
            srAdv.horizontal = false;
            srAdv.vertical = true;
            srAdv.scrollSensitivity = 45f;

            GameObject vpAdv = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            vpAdv.transform.SetParent(panelAdv.transform, false);
            RectTransform vpaRt = vpAdv.GetComponent<RectTransform>();
            vpaRt.anchorMin = Vector2.zero;
            vpaRt.anchorMax = Vector2.one;
            vpaRt.sizeDelta = Vector2.zero;
            vpAdv.GetComponent<Image>().color = Color.white;
            vpAdv.GetComponent<Mask>().showMaskGraphic = false;
            srAdv.viewport = vpaRt;

            GameObject advContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            advContent.transform.SetParent(vpAdv.transform, false);
            RectTransform acRt = advContent.GetComponent<RectTransform>();
            acRt.anchorMin = new Vector2(0f, 1f);
            acRt.anchorMax = new Vector2(1f, 1f);
            acRt.pivot = new Vector2(0.5f, 1f);
            acRt.sizeDelta = Vector2.zero;

            VerticalLayoutGroup acVlg = advContent.GetComponent<VerticalLayoutGroup>();
            acVlg.spacing = 10f;
            acVlg.padding = new RectOffset(14, 14, 14, 14);
            acVlg.childControlWidth = true;
            acVlg.childControlHeight = false;
            acVlg.childForceExpandWidth = true;

            ContentSizeFitter acCsf = advContent.GetComponent<ContentSizeFitter>();
            acCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            srAdv.content = acRt;

            // SECTION 1: FAN FACTION & RUNNER ENERGY
            CreateHeaderBanner(advContent.transform, "FAN FACTION & RUNNER ENERGY", new Color32(0x40, 0xAD, 0xFF, 0xFF), font, GetIconSprite("heartt"));
            GameObject rowLaneCost = CreateSettingField(advContent.transform, "Lane change energy cost (default: 10):", "10", font);
            GameObject rowJumpCost = CreateSettingField(advContent.transform, "Jump energy cost (default: 20):", "20", font);
            GameObject rowFreeDur = CreateSettingField(advContent.transform, "Freedom Charm duration (sec, default: 30s):", "30", font);
            GameObject rowSprintDur = CreateSettingField(advContent.transform, "Sprint Boost duration (sec, default: 30s):", "30", font);

            // SECTION 2: ANTI FACTION & TRAFFIC HAZARDS
            CreateHeaderBanner(advContent.transform, "ANTI FACTION & TRAFFIC HAZARDS", new Color32(0xFF, 0x4D, 0x57, 0xFF), font, GetIconSprite("danger"));
            GameObject rowAutoSpawn = CreateToggleField(advContent.transform, "Auto-spawn car at 100% energy:", true, font);
            GameObject rowMaxCars = CreateSettingField(advContent.transform, "Max concurrent cars on 3 lanes (1 - 3):", "2", font);

            // SECTION 3: COLLISION PENALTIES
            CreateHeaderBanner(advContent.transform, "COLLISION PENALTIES", new Color32(0xFC, 0xDA, 0x21, 0xFF), font, GetIconSprite("timer"));
            GameObject rowDistPen = CreateSettingField(advContent.transform, "Distance penalty on crash (meters, default: 100m):", "100", font);
            GameObject rowSedanPen = CreateSettingField(advContent.transform, "Energy lost on Sedan hit (%):", "20", font);
            GameObject rowPickupPen = CreateSettingField(advContent.transform, "Energy lost on Hunting Beast hit (%):", "40", font);
            GameObject rowHeavyPen = CreateSettingField(advContent.transform, "Energy lost on Train hit (%):", "60", font);

            // SECTION 4: OFFLINE LIVE STREAM SIMULATION
            CreateHeaderBanner(advContent.transform, "OFFLINE LIVE STREAM SIMULATION", new Color32(0x00, 0xE5, 0xFF, 0xFF), font, GetIconSprite("setting"));
            GameObject rowLiveMaster = CreateToggleField(advContent.transform, "Auto live simulation in test mode:", true, font);
            GameObject rowSimChats = CreateToggleField(advContent.transform, "Simulate chat comments:", true, font);
            GameObject rowSimGifts = CreateToggleField(advContent.transform, "Simulate viewer gifts:", true, font);
            GameObject rowSimLikes = CreateToggleField(advContent.transform, "Simulate continuous hearts (Likes):", true, font);
            GameObject rowSimFollowers = CreateToggleField(advContent.transform, "Simulate baton handover (Follow):", false, font);
            GameObject rowSimDelay = CreateToggleField(advContent.transform, "Simulate stream broadcast delay (1.5s - 3.0s):", false, font);

            // SECTION 5: HUD & OVERLAY DISPLAY
            CreateHeaderBanner(advContent.transform, "HUD & OVERLAY DISPLAY", new Color32(0x9B, 0x51, 0xE0, 0xFF), font, GetIconSprite("setting"));
            GameObject rowShowGuide = CreateToggleField(advContent.transform, "Show How-To-Play Guide banner:", true, font);
            GameObject rowShowGiftPanel = CreateToggleField(advContent.transform, "Show Gift Info Panel banner:", true, font);
            GameObject rowShowStopwatch = CreateToggleField(advContent.transform, "Show Match Timer (Stopwatch):", true, font);
            GameObject rowShowTimerCircles = CreateToggleField(advContent.transform, "Show Skill & Buff Timer Circles:", true, font);
            GameObject rowDebugUI = CreateToggleField(advContent.transform, "Show Debug UI in game (Toggle '~', default: OFF):", false, font);

            // SECTION 6: TIKTOK LIVE BACKEND & CONNECTION
            CreateHeaderBanner(advContent.transform, "TIKTOK LIVE BACKEND & CONNECTION", new Color32(0x00, 0xE5, 0xFF, 0xFF), font, GetIconSprite("setting"));
            GameObject rowHttpPort = CreateSettingField(advContent.transform, "Backend HTTP Port (Default: 9091):", "9091", font, 0.62f);
            GameObject rowSocketPort = CreateSettingField(advContent.transform, "Socket.IO Port for Game Client (Default: 3001):", "3001", font, 0.62f);
            Button btnGetEulerKey = null;
            GameObject rowEulerKey = CreateWebUrlSettingField(advContent.transform, "Euler API Key (EULER_API_KEY):", "euler_YTJkMTExNjY3ZjFiODZjZDczOWJhZGZjNzRiYTFhMDAzMzM5OGY1ZjQ3MGFkOTdiNzA0Mzgx", "https://www.eulerstream.com/", "GET KEY", font, out btnGetEulerKey);
            Button btnBrowseDir = null;
            Button btnResetDir = null;
            GameObject rowBackendDir = CreateFolderSettingField(advContent.transform, "Backend Folder Path:", TikTokBackendManager.DefaultLocalPath, font, out btnBrowseDir, out btnResetDir);

            // Status Display Card
            GameObject rowStatus = new GameObject("Row_BackendStatus", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Image));
            rowStatus.transform.SetParent(advContent.transform, false);
            Image statusImg = rowStatus.GetComponent<Image>();
            if (theme != null && theme.panelDark != null)
            {
                statusImg.sprite = theme.panelDark;
                statusImg.type = Image.Type.Sliced;
            }
            statusImg.color = new Color(0.06f, 0.16f, 0.24f, 0.95f);
            var statusHlg = rowStatus.GetComponent<HorizontalLayoutGroup>();
            statusHlg.padding = new RectOffset(14, 14, 10, 10);
            statusHlg.childControlWidth = true;
            statusHlg.childControlHeight = true;
            statusHlg.childForceExpandWidth = true;
            statusHlg.childForceExpandHeight = true;
            var txtStatus = CreateText(rowStatus.transform, "TxtStatus", "Port Status: Not checked", 13.5f, FontStyles.Bold, new Color32(0xBF, 0xE3, 0xE8, 0xFF), TextAlignmentOptions.Left, font);

            // Action Buttons Row (3D Pill buttons)
            GameObject rowBackendBtns = new GameObject("Row_BackendButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowBackendBtns.transform.SetParent(advContent.transform, false);
            var btnsHlg = rowBackendBtns.GetComponent<HorizontalLayoutGroup>();
            btnsHlg.spacing = 10f;
            btnsHlg.childControlWidth = true;
            btnsHlg.childControlHeight = true;
            btnsHlg.childForceExpandWidth = true;
            btnsHlg.childForceExpandHeight = true;
            rowBackendBtns.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 44f);

            GameObject btnCheckPort = CreatePillButton(rowBackendBtns.transform, "BtnCheckPort", "CHECK PORTS", theme?.pillBlue, Color.white, 12.5f, font);
            GameObject btnStartBackend = CreatePillButton(rowBackendBtns.transform, "BtnStartBackend", "START BACKEND", GetGreenPillSprite(), Color.white, 12.5f, font);
            GameObject btnSetupBackend = CreatePillButton(rowBackendBtns.transform, "BtnSetupBackend", "AUTO SETUP (GIT)", theme?.pillGold, Color.black, 12.5f, font);
            GameObject btnStopBackend = CreatePillButton(rowBackendBtns.transform, "BtnStopBackend", "STOP BACKEND", theme?.pillRed, Color.white, 12.5f, font);

            panelAdv.SetActive(false);

            // 6. Bottom Action Bar (3D Pill Buttons)
            GameObject bottomBar = new GameObject("BottomBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bottomBar.transform.SetParent(window.transform, false);
            RectTransform bbRt = bottomBar.GetComponent<RectTransform>();
            bbRt.anchorMin = new Vector2(0.04f, 0f);
            bbRt.anchorMax = new Vector2(0.96f, 0f);
            bbRt.pivot = new Vector2(0.5f, 0f);
            bbRt.anchoredPosition = new Vector2(0f, 18f);
            bbRt.sizeDelta = new Vector2(0f, 50f);

            HorizontalLayoutGroup bbHlg = bottomBar.GetComponent<HorizontalLayoutGroup>();
            bbHlg.spacing = 12f;
            bbHlg.childControlWidth = true;
            bbHlg.childControlHeight = true;
            bbHlg.childForceExpandWidth = true;
            bbHlg.childForceExpandHeight = true;

            GameObject btnReset = CreatePillButton(bottomBar.transform, "BtnResetDefaults", "RESET DEFAULTS", theme?.pillDark, Color.white, 14f, font, GetRestartSprite());
            GameObject btnSave = CreatePillButton(bottomBar.transform, "BtnSaveConfig", "SAVE SETTINGS", theme?.pillBlue, Color.white, 15f, font);
            GameObject btnTest = CreatePillButton(bottomBar.transform, "BtnTestMode", "TEST RUN (OFFLINE)", GetGreenPillSprite(), Color.white, 15f, font);
            GameObject btnLive = CreatePillButton(bottomBar.transform, "BtnGoLive", "START LIVE STREAM", theme?.pillGold, Color.black, 16f, font);

            // 7. Wire UI script component
            PreGameConfigUI configUI = modalRoot.AddComponent<PreGameConfigUI>();
            WireConfigUIFields(configUI,
                btnTabBasic, btnTabAdv, panelBasic, panelAdv,
                inputUserObj.GetComponent<TMP_InputField>(),
                btnCheckLiveObj.GetComponent<Button>(),
                txtLiveStatus,
                inputDistObj.GetComponent<TMP_InputField>(),
                togInfRow.GetComponentInChildren<Toggle>(),
                giftsContent.transform, giftRowTemplate,
                rowLaneCost.GetComponentInChildren<TMP_InputField>(),
                rowJumpCost.GetComponentInChildren<TMP_InputField>(),
                rowFreeDur.GetComponentInChildren<TMP_InputField>(),
                rowSprintDur.GetComponentInChildren<TMP_InputField>(),
                rowAutoSpawn.GetComponentInChildren<Toggle>(),
                rowMaxCars.GetComponentInChildren<TMP_InputField>(),
                rowDistPen.GetComponentInChildren<TMP_InputField>(),
                rowSedanPen.GetComponentInChildren<TMP_InputField>(),
                rowPickupPen.GetComponentInChildren<TMP_InputField>(),
                rowHeavyPen.GetComponentInChildren<TMP_InputField>(),
                rowLiveMaster.GetComponentInChildren<Toggle>(),
                rowSimChats.GetComponentInChildren<Toggle>(),
                rowSimGifts.GetComponentInChildren<Toggle>(),
                rowSimLikes.GetComponentInChildren<Toggle>(),
                rowSimFollowers.GetComponentInChildren<Toggle>(),
                rowSimDelay.GetComponentInChildren<Toggle>(),
                rowShowGuide.GetComponentInChildren<Toggle>(),
                rowShowGiftPanel.GetComponentInChildren<Toggle>(),
                rowShowStopwatch.GetComponentInChildren<Toggle>(),
                rowShowTimerCircles.GetComponentInChildren<Toggle>(),
                rowDebugUI.GetComponentInChildren<Toggle>(),
                rowHttpPort.GetComponentInChildren<TMP_InputField>(),
                rowSocketPort.GetComponentInChildren<TMP_InputField>(),
                rowEulerKey.GetComponentInChildren<TMP_InputField>(),
                rowBackendDir.GetComponentInChildren<TMP_InputField>(),
                txtStatus,
                btnBrowseDir,
                btnCheckPort.GetComponent<Button>(),
                btnStartBackend.GetComponent<Button>(),
                btnSetupBackend.GetComponent<Button>(),
                btnStopBackend.GetComponent<Button>(),
                btnGetEulerKey,
                btnReset.GetComponent<Button>(), btnSave.GetComponent<Button>(), btnTest.GetComponent<Button>(), btnLive.GetComponent<Button>(), closeBtnObj.GetComponent<Button>()
            );

            // 8. Ensure Manager component on Canvas
            PreGameConfigManager manager = canvas.GetComponent<PreGameConfigManager>();
            if (manager == null) manager = canvas.gameObject.AddComponent<PreGameConfigManager>();
            SetPrivateField(manager, "_configUI", configUI);
            SetPrivateField(manager, "_openOnStart", true);

            // 9. Wire HUD Settings button, Audio button and Demo Run badge
            CreateHudGearButton(canvas, manager, font);
            CreateHudAudioButton(canvas);
            CreateDemoRunBadge(canvas, manager, font);

            modalRoot.SetActive(false);
            return modalRoot;
        }

        private static void CreateHudGearButton(Canvas canvas, PreGameConfigManager manager, TMP_FontAsset font)
        {
            Transform existingStyled = canvas.transform.Find("Btn_OpenPreGameConfig");
            Transform existingDuplicate = canvas.transform.Find("BtnOpenPreGameConfig");

            if (existingDuplicate != null)
            {
#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(existingDuplicate.gameObject);
#else
                UnityEngine.Object.Destroy(existingDuplicate.gameObject);
#endif
            }

            Button targetBtn = null;
            if (existingStyled != null)
            {
                targetBtn = existingStyled.GetComponent<Button>();
                var targetImg = existingStyled.GetComponent<Image>();
                if (targetImg != null)
                {
                    targetImg.raycastTarget = true;
                    if (targetBtn != null) targetBtn.targetGraphic = targetImg;
                }

                // Ensure child icon and text don't block clicks
                for (int i = 0; i < existingStyled.childCount; i++)
                {
                    var childGraphic = existingStyled.GetChild(i).GetComponent<Graphic>();
                    if (childGraphic != null) childGraphic.raycastTarget = false;
                }

                if (targetBtn != null)
                {
                    var colors = targetBtn.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(0.7f, 1f, 1f, 1f);
                    colors.pressedColor = new Color(0.4f, 0.7f, 0.9f, 1f);
                    targetBtn.colors = colors;

                    if (manager != null)
                    {
                        targetBtn.onClick.RemoveListener(manager.ToggleConfigUI);
                        targetBtn.onClick.AddListener(manager.ToggleConfigUI);
                    }
                }
            }

            if (targetBtn != null)
            {
                SetPrivateField(manager, "_btnOpenConfig", targetBtn);
                return;
            }

            // Fallback if none exists in scene
            GameObject btnObj = new GameObject("Btn_OpenPreGameConfig", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(canvas.transform, false);
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(24f, 24f);
            rt.sizeDelta = new Vector2(116f, 38f);

            var theme = HudTheme.Current;
            Image img = btnObj.GetComponent<Image>();
            if (theme != null && theme.pillDark != null)
            {
                img.sprite = theme.pillDark;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.1f, 0.2f, 0.3f, 0.95f);
            }

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (manager != null) manager.ToggleConfigUI();
            });

            SetPrivateField(manager, "_btnOpenConfig", btn);
        }

        private static void CreateHudAudioButton(Canvas canvas)
        {
            Transform existingStyled = canvas.transform.Find("Btn_ToggleAudio");
            Transform existingDuplicate = canvas.transform.Find("BtnAudioToggle");

            if (existingDuplicate != null)
            {
#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(existingDuplicate.gameObject);
#else
                UnityEngine.Object.Destroy(existingDuplicate.gameObject);
#endif
            }

            if (existingStyled != null)
            {
                if (existingStyled.GetComponent<Views.AudioToggleController>() == null)
                {
                    existingStyled.gameObject.AddComponent<Views.AudioToggleController>();
                }
            }
        }

        private static void CreateDemoRunBadge(Canvas canvas, PreGameConfigManager manager, TMP_FontAsset font)
        {
            Transform existing = canvas.transform.Find("Badge_DemoRun");
            if (existing != null)
            {
#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
#else
                UnityEngine.Object.Destroy(existing.gameObject);
#endif
            }

            GameObject badgeObj = new GameObject("Badge_DemoRun", typeof(RectTransform), typeof(Image), typeof(Views.DemoRunBadgeController));
            badgeObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = badgeObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(24f, 68f);
            rt.sizeDelta = new Vector2(116f, 26f);

            var theme = HudTheme.Current;
            Image bgImg = badgeObj.GetComponent<Image>();
            if (theme != null && theme.pillDark != null)
            {
                bgImg.sprite = theme.pillDark;
                bgImg.type = Image.Type.Sliced;
            }
            bgImg.color = new Color(0.08f, 0.14f, 0.22f, 0.92f);
            bgImg.raycastTarget = false;

            // Indicator dot
            GameObject dotObj = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dotObj.transform.SetParent(badgeObj.transform, false);
            RectTransform drt = dotObj.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0f, 0.5f);
            drt.anchorMax = new Vector2(0f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = new Vector2(14f, 0f);
            drt.sizeDelta = new Vector2(8f, 8f);

            Image dotImg = dotObj.GetComponent<Image>();
            if (theme != null && theme.circleGold != null)
            {
                dotImg.sprite = theme.circleGold;
            }
            dotImg.color = new Color(1f, 0.85f, 0.2f, 1f);
            dotImg.raycastTarget = false;

            // Text
            var txt = CreateText(badgeObj.transform, "Text", "RUN DEMO", 11.5f, FontStyles.Bold, new Color32(0xFC, 0xDA, 0x21, 0xFF), TextAlignmentOptions.MidlineLeft, font);
            RectTransform trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(24f, 0f);
            trt.offsetMax = Vector2.zero;
            txt.raycastTarget = false;

            var controller = badgeObj.GetComponent<Views.DemoRunBadgeController>();
            controller.Setup(dotImg, txt);

            if (manager != null)
            {
                SetPrivateField(manager, "_badgeDemoRun", badgeObj);
                manager.SetDemoRunBadge(badgeObj);
            }

            badgeObj.SetActive(manager != null && manager.IsTestModeActive);
        }

        public static void SetRect(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static GameObject CreateDropdownControl(Transform parent, string name, TMP_FontAsset font)
        {
            GameObject dropdownObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            dropdownObj.transform.SetParent(parent, false);

            Image bg = dropdownObj.GetComponent<Image>();
            Sprite inputBg = GetInputFieldBgSprite();
            if (inputBg != null)
            {
                bg.sprite = inputBg;
                bg.type = Image.Type.Sliced;
            }
            bg.color = new Color(0.08f, 0.20f, 0.28f, 0.95f);

            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(dropdownObj.transform, false);
            RectTransform lrt = labelObj.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(10f, 2f);
            lrt.offsetMax = new Vector2(-26f, -2f);

            TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.fontSize = 12f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject arrowObj = new GameObject("Arrow", typeof(RectTransform), typeof(TextMeshProUGUI));
            arrowObj.transform.SetParent(dropdownObj.transform, false);
            RectTransform art = arrowObj.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(1f, 0.5f);
            art.anchorMax = new Vector2(1f, 0.5f);
            art.pivot = new Vector2(1f, 0.5f);
            art.anchoredPosition = new Vector2(-6f, 0f);
            art.sizeDelta = new Vector2(16f, 16f);

            TextMeshProUGUI arrow = arrowObj.GetComponent<TextMeshProUGUI>();
            if (font != null) arrow.font = font;
            arrow.fontSize = 11f;
            arrow.text = "▼";
            arrow.color = new Color32(0xBF, 0xE3, 0xE8, 0xFF);
            arrow.alignment = TextAlignmentOptions.Center;

            TMP_Dropdown dd = dropdownObj.GetComponent<TMP_Dropdown>();
            dd.captionText = label;

            return dropdownObj;
        }

        private static GameObject CreateGiftRowTemplate(Transform parent, TMP_FontAsset font)
        {
            GameObject row = new GameObject("GiftRowTemplate", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(parent, false);
            RectTransform rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 48f);

            Image bg = row.GetComponent<Image>();
            var theme = HudTheme.Current;
            if (theme != null && theme.panelDark != null)
            {
                bg.sprite = theme.panelDark;
                bg.type = Image.Type.Sliced;
            }
            bg.color = new Color(0.08f, 0.20f, 0.28f, 0.95f);

            // 1. Enable Switch Toggle
            GameObject toggleObj = CreateSimpleToggle(row.transform, "ToggleEnable", font);
            RectTransform togRt = toggleObj.GetComponent<RectTransform>();
            SetRect(togRt, 10f, 0f, 56f, 22f);

            // 2. Feature Name
            var featTxt = CreateText(row.transform, "FeatureNameText", "Feature Name", 13f, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft, font);
            RectTransform fnRt = featTxt.GetComponent<RectTransform>();
            SetRect(fnRt, 74f, 0f, 142f, 36f);

            // 3. Gift Selection (Icon + Dropdown)
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(row.transform, false);
            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            SetRect(iconRt, 222f, 0f, 30f, 30f);
            Image iconImg = iconObj.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.color = Color.clear;
            iconImg.enabled = false;

            GameObject dropdownObj = CreateDropdownControl(row.transform, "GiftDropdown", font);
            RectTransform ddRt = dropdownObj.GetComponent<RectTransform>();
            SetRect(ddRt, 256f, 0f, 110f, 34f);

            // 4. Stat Input
            GameObject statInput = CreateStatInputField(row.transform, "StatInput", font);
            RectTransform siRt = statInput.GetComponent<RectTransform>();
            SetRect(siRt, 372f, 0f, 72f, 34f);

            // 5. Description Input
            GameObject descInput = CreateInputField(row.transform, "DescInput", "Card description...", 12f, font);
            RectTransform diRt = descInput.GetComponent<RectTransform>();
            SetRect(diRt, 450f, 0f, 194f, 34f);

            // 6. Reorder Buttons (▲ ▼)
            GameObject btnUp = CreatePillButton(row.transform, "BtnUp", "▲", theme?.pillDark, Color.white, 10f, font);
            RectTransform upRt = btnUp.GetComponent<RectTransform>();
            SetRect(upRt, 648f, 0f, 26f, 28f);

            GameObject btnDown = CreatePillButton(row.transform, "BtnDown", "▼", theme?.pillDark, Color.white, 10f, font);
            RectTransform downRt = btnDown.GetComponent<RectTransform>();
            SetRect(downRt, 678f, 0f, 26f, 28f);

            return row;
        }

        public static GameObject CreateStatInputField(Transform parent, string name, TMP_FontAsset font)
        {
            GameObject inputObj = CreateInputField(parent, name, "0", 13f, font);
            var input = inputObj.GetComponent<TMP_InputField>();
            if (input != null)
            {
                input.contentType = TMP_InputField.ContentType.DecimalNumber;
                if (input.textComponent != null)
                {
                    input.textComponent.alignment = TextAlignmentOptions.Center;
                    input.textComponent.fontStyle = FontStyles.Bold;
                    input.textComponent.color = Color.white;
                }
                if (input.placeholder != null && input.placeholder is TextMeshProUGUI ph)
                {
                    ph.alignment = TextAlignmentOptions.Center;
                    ph.color = new Color(0.55f, 0.72f, 0.82f, 0.6f);
                }
            }
            var bg = inputObj.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = new Color(0.08f, 0.20f, 0.28f, 0.95f);
            }
            return inputObj;
        }

        public static GameObject CreateHeaderColumn(Transform parent, string name, string text, TMP_FontAsset font)
        {
            var txt = CreateText(parent, name, text, 11f, FontStyles.Bold, new Color32(0x00, 0xE5, 0xFF, 0xFF), TextAlignmentOptions.Center, font);
            return txt.gameObject;
        }

        private static GameObject CreateSimpleToggle(Transform parent, string name, TMP_FontAsset font)
        {
            GameObject toggleObj = new GameObject(name, typeof(RectTransform), typeof(Toggle), typeof(Image), typeof(Views.ToggleSpriteSwapper));
            toggleObj.transform.SetParent(parent, false);
            RectTransform trt = toggleObj.GetComponent<RectTransform>();
            trt.sizeDelta = new Vector2(62f, 22f);

            Image img = toggleObj.GetComponent<Image>();
            Sprite onSp = GetToggleOnSprite();
            Sprite offSp = GetToggleOffSprite();

            img.sprite = onSp;
            img.color = Color.white;
            img.preserveAspect = true;

            Toggle t = toggleObj.GetComponent<Toggle>();
            t.targetGraphic = img;
            t.transition = Selectable.Transition.None;
            t.isOn = true;

            var swapper = toggleObj.GetComponent<Views.ToggleSpriteSwapper>();
            swapper.Setup(onSp, offSp, img);

            return toggleObj;
        }

        private static GameObject CreateHeaderBanner(Transform parent, string title, Color color, TMP_FontAsset font, Sprite categoryIcon = null)
        {
            GameObject obj = new GameObject("Banner_" + title, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 36f);

            Image img = obj.GetComponent<Image>();
            var theme = HudTheme.Current;
            if (theme != null && theme.panelDark != null)
            {
                img.sprite = theme.panelDark;
                img.type = Image.Type.Sliced;
            }
            img.color = new Color(0.06f, 0.16f, 0.22f, 0.98f);

            // Accent Left Pill
            GameObject accentObj = new GameObject("AccentStripe", typeof(RectTransform), typeof(Image));
            accentObj.transform.SetParent(obj.transform, false);
            RectTransform art = accentObj.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0f, 0.12f);
            art.anchorMax = new Vector2(0f, 0.88f);
            art.pivot = new Vector2(0f, 0.5f);
            art.anchoredPosition = new Vector2(4f, 0f);
            art.sizeDelta = new Vector2(6f, 0f);
            Image accentImg = accentObj.GetComponent<Image>();
            accentImg.color = color;

            float textLeft = 16f;
            if (categoryIcon != null)
            {
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(obj.transform, false);
                RectTransform irt = iconObj.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0f, 0.5f);
                irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(16f, 0f);
                irt.sizeDelta = new Vector2(20f, 20f);
                Image iImg = iconObj.GetComponent<Image>();
                iImg.sprite = categoryIcon;
                iImg.color = color;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
                textLeft = 42f;
            }

            var txt = CreateText(obj.transform, "Text", title, 13.5f, FontStyles.Bold, color, TextAlignmentOptions.MidlineLeft, font);
            RectTransform trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(textLeft, 0f);
            trt.offsetMax = new Vector2(-12f, 0f);

            return obj;
        }

        private static GameObject CreateSettingField(Transform parent, string label, string defaultValue, TMP_FontAsset font, float labelRatio = 0.62f, bool showResetBtn = true)
        {
            GameObject row = new GameObject("Row_" + label, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 40f);

            var lblTxt = CreateText(row.transform, "Label", label, 13.5f, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft, font);
            RectTransform lrt = lblTxt.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(labelRatio - 0.02f, 1f);
            lrt.offsetMin = new Vector2(8f, 0f);
            lrt.offsetMax = Vector2.zero;

            GameObject inputObj = CreateInputField(row.transform, "Input", defaultValue, 13.5f, font);
            RectTransform irt = inputObj.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(labelRatio, 0f);
            irt.anchorMax = new Vector2(showResetBtn ? 0.88f : 1f, 1f);
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = new Vector2(-6f, 0f);

            if (showResetBtn)
            {
                var inputField = inputObj.GetComponent<TMP_InputField>();
                var theme = HudTheme.Current;
                GameObject btnReset = CreatePillButton(row.transform, "BtnReset", "RESET", theme?.pillDark, new Color32(0xBF, 0xE3, 0xE8, 0xFF), 11f, font, GetRestartSprite());
                RectTransform brt = btnReset.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.89f, 0f);
                brt.anchorMax = new Vector2(1f, 1f);
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;

                btnReset.GetComponent<Button>().onClick.AddListener(() =>
                {
                    if (inputField != null) inputField.text = defaultValue;
                });
            }

            return row;
        }

        private static GameObject CreateFolderSettingField(Transform parent, string label, string defaultValue, TMP_FontAsset font, out Button browseBtn, out Button resetBtn)
        {
            GameObject row = new GameObject("Row_" + label, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 40f);

            var lblTxt = CreateText(row.transform, "Label", label, 13.5f, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft, font);
            RectTransform lrt = lblTxt.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(0.32f, 1f);
            lrt.offsetMin = new Vector2(8f, 0f);
            lrt.offsetMax = Vector2.zero;

            // Reset button (Right aligned, standard 11% width ~84px)
            var theme = HudTheme.Current;
            GameObject btnReset = CreatePillButton(row.transform, "BtnResetDir", "RESET", theme?.pillDark, new Color32(0xBF, 0xE3, 0xE8, 0xFF), 11f, font, GetRestartSprite());
            RectTransform brt = btnReset.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.89f, 0f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            resetBtn = btnReset.GetComponent<Button>();

            // Browse button with folder icon (Fixed 38x38px circular button, centered right before Reset)
            GameObject btnBrowse = CreateIconButton(row.transform, "BtnBrowseDir", GetFolderSprite(), theme?.pillBlue, new Vector2(24f, 24f));
            RectTransform bbrt = btnBrowse.GetComponent<RectTransform>();
            bbrt.anchorMin = new Vector2(0.89f, 0.5f);
            bbrt.anchorMax = new Vector2(0.89f, 0.5f);
            bbrt.pivot = new Vector2(1f, 0.5f);
            bbrt.anchoredPosition = new Vector2(-6f, 0f);
            bbrt.sizeDelta = new Vector2(38f, 38f);
            browseBtn = btnBrowse.GetComponent<Button>();

            // Input field (stretches cleanly between Label and Browse button)
            GameObject inputObj = CreateInputField(row.transform, "Input", defaultValue, 13f, font);
            RectTransform irt = inputObj.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.33f, 0f);
            irt.anchorMax = new Vector2(0.89f, 1f);
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = new Vector2(-50f, 0f);

            var inputField = inputObj.GetComponent<TMP_InputField>();
            var resetComp = btnReset.AddComponent<Views.InputFieldResetButton>();
            resetComp.Setup(inputField, defaultValue);

            return row;
        }

        private static GameObject CreateWebUrlSettingField(Transform parent, string label, string defaultValue, string targetUrl, string buttonText, TMP_FontAsset font, out Button webBtnOut)
        {
            GameObject row = new GameObject("Row_" + label, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 40f);

            var lblTxt = CreateText(row.transform, "Label", label, 13.5f, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft, font);
            RectTransform lrt = lblTxt.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(0.33f, 1f);
            lrt.offsetMin = new Vector2(8f, 0f);
            lrt.offsetMax = Vector2.zero;

            // Reset button (Right aligned, standard 11% width ~84px)
            var theme = HudTheme.Current;
            GameObject btnReset = CreatePillButton(row.transform, "BtnReset", "RESET", theme?.pillDark, new Color32(0xBF, 0xE3, 0xE8, 0xFF), 11f, font, GetRestartSprite());
            RectTransform brt = btnReset.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.89f, 0f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;

            // Web Action Button ("GET KEY" with Globe icon)
            GameObject btnWeb = CreatePillButton(row.transform, "BtnOpenWeb", buttonText, theme?.pillBlue, Color.white, 11f, font, GetGlobeSprite());
            RectTransform wrt = btnWeb.GetComponent<RectTransform>();
            wrt.anchorMin = new Vector2(0.76f, 0f);
            wrt.anchorMax = new Vector2(0.88f, 1f);
            wrt.offsetMin = Vector2.zero;
            wrt.offsetMax = new Vector2(-4f, 0f);

            webBtnOut = btnWeb.GetComponent<Button>();
            var urlOpener = btnWeb.AddComponent<Views.OpenUrlButton>();
            urlOpener.Setup(targetUrl);

            // Input field (stretches cleanly between Label and Web button)
            GameObject inputObj = CreateInputField(row.transform, "Input", defaultValue, 13f, font);
            RectTransform irt = inputObj.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.34f, 0f);
            irt.anchorMax = new Vector2(0.76f, 1f);
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = new Vector2(-4f, 0f);

            var inputField = inputObj.GetComponent<TMP_InputField>();
            var resetComp = btnReset.AddComponent<Views.InputFieldResetButton>();
            resetComp.Setup(inputField, defaultValue);

            return row;
        }

        private static GameObject CreateToggleField(Transform parent, string label, bool defaultValue, TMP_FontAsset font)
        {
            GameObject row = new GameObject("Row_" + label, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 38f);

            var lblTxt = CreateText(row.transform, "Label", label, 13.5f, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft, font);
            RectTransform lrt = lblTxt.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(0.82f, 1f);
            lrt.offsetMin = new Vector2(8f, 0f);
            lrt.offsetMax = Vector2.zero;

            GameObject togObj = CreateSimpleToggle(row.transform, "Toggle", font);
            RectTransform trt = togObj.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.89f, 0.5f);
            trt.anchorMax = new Vector2(0.89f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = Vector2.zero;

            togObj.GetComponent<Toggle>().isOn = defaultValue;
            return row;
        }

        private static GameObject CreateTabButton(Transform parent, string name, string label, bool isActive, TMP_FontAsset font, Sprite tabIcon = null)
        {
            var theme = HudTheme.Current;
            Sprite pillSp = isActive ? theme?.pillGold : theme?.pillDark;

            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            Image img = btnObj.GetComponent<Image>();
            if (pillSp != null)
            {
                img.sprite = pillSp;
                img.type = Image.Type.Sliced;
            }
            img.color = Color.white;

            float textLeft = 0f;
            if (tabIcon != null)
            {
                GameObject iconObj = new GameObject("TabIcon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(btnObj.transform, false);
                RectTransform irt = iconObj.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.12f, 0.5f);
                irt.anchorMax = new Vector2(0.12f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(22f, 22f);
                Image iImg = iconObj.GetComponent<Image>();
                iImg.sprite = tabIcon;
                iImg.color = isActive ? new Color32(0x1A, 0x1A, 0x1A, 0xFF) : new Color32(0xBF, 0xE3, 0xE8, 0xFF);
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
                textLeft = 14f;
            }

            Color textColor = isActive ? new Color32(0x1A, 0x1A, 0x1A, 0xFF) : new Color32(0xBF, 0xE3, 0xE8, 0xFF);
            var txt = CreateText(btnObj.transform, "Text", label, 14.5f, FontStyles.Bold, textColor, TextAlignmentOptions.Center, font);
            RectTransform trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(textLeft, 0f);
            trt.offsetMax = Vector2.zero;

            return btnObj;
        }

        private static GameObject CreatePillButton(Transform parent, string name, string label, Sprite pillSprite, Color textColor, float fontSize, TMP_FontAsset font, Sprite leadingIcon = null)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            Image img = btnObj.GetComponent<Image>();
            if (pillSprite != null)
            {
                img.sprite = pillSprite;
                img.type = Image.Type.Sliced;
            }
            img.color = Color.white;

            float textLeft = 0f;
            if (leadingIcon != null)
            {
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(btnObj.transform, false);
                RectTransform irt = iconObj.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.16f, 0.5f);
                irt.anchorMax = new Vector2(0.16f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(18f, 18f);
                Image iImg = iconObj.GetComponent<Image>();
                iImg.sprite = leadingIcon;
                iImg.color = textColor;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
                textLeft = 16f;
            }

            var txt = CreateText(btnObj.transform, "Text", label, fontSize, FontStyles.Bold, textColor, TextAlignmentOptions.Center, font);
            RectTransform trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(textLeft, 0f);
            trt.offsetMax = Vector2.zero;

            return btnObj;
        }

        private static GameObject CreateIconButton(Transform parent, string name, Sprite iconSprite, Sprite pillSprite = null, Vector2? iconSize = null)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            Image img = btnObj.GetComponent<Image>();
            if (pillSprite != null)
            {
                img.sprite = pillSprite;
                img.type = Image.Type.Sliced;
            }
            else
            {
                img.color = new Color32(0x00, 0x91, 0xEA, 0xFF);
            }

            if (iconSprite != null)
            {
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(btnObj.transform, false);
                RectTransform iconRt = iconObj.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;
                iconRt.sizeDelta = iconSize ?? new Vector2(24f, 24f);

                Image iconImg = iconObj.GetComponent<Image>();
                iconImg.sprite = iconSprite;
                iconImg.color = Color.white;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }
            return btnObj;
        }

        private static GameObject CreateCloseButton(Transform parent, string name, Vector2 sizeDelta, TMP_FontAsset font)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            if (sizeDelta != Vector2.zero) rt.sizeDelta = sizeDelta;

            Image img = btnObj.GetComponent<Image>();
            Sprite closeSp = GetCloseSprite();
            if (closeSp != null)
            {
                img.sprite = closeSp;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.color = new Color(0.85f, 0.2f, 0.25f, 0.9f);
                var txt = CreateText(btnObj.transform, "Text", "X", 17f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, font);
                RectTransform trt = txt.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.sizeDelta = Vector2.zero;
            }

            return btnObj;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions align, TMP_FontAsset font)
        {
            GameObject txtObj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static GameObject CreateInputField(Transform parent, string name, string placeholderText, float fontSize, TMP_FontAsset font)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            root.transform.SetParent(parent, false);
            Image bg = root.GetComponent<Image>();

            Sprite inputBg = GetInputFieldBgSprite();
            if (inputBg != null)
            {
                bg.sprite = inputBg;
                bg.type = Image.Type.Sliced;
            }
            bg.color = new Color(0.08f, 0.20f, 0.28f, 0.95f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(root.transform, false);
            RectTransform taRt = textArea.GetComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(12f, 4f);
            taRt.offsetMax = new Vector2(-12f, -4f);

            GameObject ph = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            ph.transform.SetParent(textArea.transform, false);
            RectTransform phRt = ph.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.sizeDelta = Vector2.zero;
            TextMeshProUGUI phTxt = ph.GetComponent<TextMeshProUGUI>();
            if (font != null) phTxt.font = font;
            phTxt.text = placeholderText;
            phTxt.fontSize = fontSize;
            phTxt.fontStyle = FontStyles.Italic;
            phTxt.color = new Color(0.55f, 0.72f, 0.82f, 0.6f);
            phTxt.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(textArea.transform, false);
            RectTransform tRt = text.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.sizeDelta = Vector2.zero;
            TextMeshProUGUI tTxt = text.GetComponent<TextMeshProUGUI>();
            if (font != null) tTxt.font = font;
            tTxt.fontSize = fontSize;
            tTxt.color = Color.white;
            tTxt.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_InputField input = root.GetComponent<TMP_InputField>();
            input.textViewport = taRt;
            input.textComponent = tTxt;
            input.placeholder = phTxt;
            input.fontAsset = font;

            return root;
        }

        private static void WireConfigUIFields(PreGameConfigUI ui,
            GameObject btnTabBasic, GameObject btnTabAdv, GameObject panelBasic, GameObject panelAdv,
            TMP_InputField inputUsername,
            Button btnCheckLive,
            TextMeshProUGUI txtLiveStatus,
            TMP_InputField inTargetDist, Toggle togInfiniteDist,
            Transform giftsContainer, GameObject giftRowTemplate,
            TMP_InputField inLaneCost, TMP_InputField inJumpCost, TMP_InputField inFreeDur, TMP_InputField inSprintDur,
            Toggle togAutoSpawn, TMP_InputField inMaxCars, TMP_InputField inDistPen, TMP_InputField inSedanPen, TMP_InputField inPickupPen, TMP_InputField inHeavyPen,
            Toggle togLiveMaster, Toggle togSimChats, Toggle togSimGifts, Toggle togSimLikes, Toggle togSimFollowers, Toggle togSimDelay,
            Toggle togShowGuide, Toggle togShowGiftPanel, Toggle togShowStopwatch, Toggle togShowTimerCircles,
            Toggle togDebugUI,
            TMP_InputField inBackendPort, TMP_InputField inBackendSocketPort, TMP_InputField inEulerKey, TMP_InputField inBackendDir,
            TextMeshProUGUI txtBackendStatus,
            Button btnBrowseDir,
            Button btnCheckPort, Button btnStartBackend, Button btnSetupBackend, Button btnStopBackend, Button btnGetEulerKey,
            Button btnReset, Button btnSave, Button btnTest, Button btnLive, Button btnClose)
        {
            SetPrivateField(ui, "_btnTabBasic", btnTabBasic.GetComponent<Button>());
            SetPrivateField(ui, "_btnTabAdvanced", btnTabAdv.GetComponent<Button>());
            SetPrivateField(ui, "_panelBasicContent", panelBasic);
            SetPrivateField(ui, "_panelAdvancedContent", panelAdv);
            SetPrivateField(ui, "_tabBasicHighlight", btnTabBasic.GetComponent<Image>());
            SetPrivateField(ui, "_tabAdvancedHighlight", btnTabAdv.GetComponent<Image>());

            SetPrivateField(ui, "_inputUsername", inputUsername);
            SetPrivateField(ui, "_btnCheckLive", btnCheckLive);
            SetPrivateField(ui, "_txtLiveStatus", txtLiveStatus);
            SetPrivateField(ui, "_inputTargetDistance", inTargetDist);
            SetPrivateField(ui, "_toggleInfiniteDistance", togInfiniteDist);
            SetPrivateField(ui, "_giftsContainer", giftsContainer);
            SetPrivateField(ui, "_giftRowTemplate", giftRowTemplate);

            SetPrivateField(ui, "_inputLaneCost", inLaneCost);
            SetPrivateField(ui, "_inputJumpCost", inJumpCost);
            SetPrivateField(ui, "_inputFreeControlDuration", inFreeDur);
            SetPrivateField(ui, "_inputSprintDuration", inSprintDur);
            SetPrivateField(ui, "_toggleAutoSpawnCar", togAutoSpawn);
            SetPrivateField(ui, "_inputMaxCars", inMaxCars);
            SetPrivateField(ui, "_inputDistancePenalty", inDistPen);
            SetPrivateField(ui, "_inputSedanEnergyPenalty", inSedanPen);
            SetPrivateField(ui, "_inputPickupEnergyPenalty", inPickupPen);
            SetPrivateField(ui, "_inputHeavyEnergyPenalty", inHeavyPen);

            SetPrivateField(ui, "_toggleLiveDemoMaster", togLiveMaster);
            SetPrivateField(ui, "_toggleSimulatedChats", togSimChats);
            SetPrivateField(ui, "_toggleSimulatedGifts", togSimGifts);
            SetPrivateField(ui, "_toggleSimulatedLikes", togSimLikes);
            SetPrivateField(ui, "_toggleSimulatedFollowers", togSimFollowers);
            SetPrivateField(ui, "_toggleSimulatedDelay", togSimDelay);
            SetPrivateField(ui, "_toggleShowHowToPlayGuide", togShowGuide);
            SetPrivateField(ui, "_toggleShowGiftInfoPanel", togShowGiftPanel);
            SetPrivateField(ui, "_toggleShowStopwatch", togShowStopwatch);
            SetPrivateField(ui, "_toggleShowTimerCircles", togShowTimerCircles);
            SetPrivateField(ui, "_toggleDebugUI", togDebugUI);

            SetPrivateField(ui, "_inputBackendPort", inBackendPort);
            SetPrivateField(ui, "_inputBackendSocketPort", inBackendSocketPort);
            SetPrivateField(ui, "_inputEulerApiKey", inEulerKey);
            SetPrivateField(ui, "_inputBackendDir", inBackendDir);
            SetPrivateField(ui, "_txtBackendStatus", txtBackendStatus);
            SetPrivateField(ui, "_btnBrowseBackendDir", btnBrowseDir);
            SetPrivateField(ui, "_btnCheckPort", btnCheckPort);
            SetPrivateField(ui, "_btnStartBackend", btnStartBackend);
            SetPrivateField(ui, "_btnSetupBackend", btnSetupBackend);
            SetPrivateField(ui, "_btnStopBackend", btnStopBackend);
            SetPrivateField(ui, "_btnGetEulerKey", btnGetEulerKey);

            SetPrivateField(ui, "_btnResetDefaults", btnReset);
            SetPrivateField(ui, "_btnSaveConfig", btnSave);
            SetPrivateField(ui, "_btnTestMode", btnTest);
            SetPrivateField(ui, "_btnGoLive", btnLive);
            SetPrivateField(ui, "_btnClose", btnClose);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }
}

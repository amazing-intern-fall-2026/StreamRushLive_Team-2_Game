using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SteamRush.Features.UI;

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

        public static GameObject BuildOrUpdatePreGameConfigUI(Canvas canvas)
        {
            if (canvas == null) return null;

            TMP_FontAsset font = GetFont();
            HudTheme theme = HudTheme.Current;

            // Remove existing if any
            Transform existing = canvas.transform.Find("PreGameConfigModal");
            if (existing != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(existing.gameObject);
                else UnityEngine.Object.Destroy(existing.gameObject);
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

            // 1b. Dim Backdrop
            GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(modalRoot.transform, false);
            RectTransform bdRt = backdrop.GetComponent<RectTransform>();
            bdRt.anchorMin = Vector2.zero;
            bdRt.anchorMax = Vector2.one;
            bdRt.sizeDelta = Vector2.zero;
            Image bdImg = backdrop.GetComponent<Image>();
            bdImg.color = new Color(0.02f, 0.05f, 0.08f, 0.85f);

            // 2. Main Window Container
            GameObject window = new GameObject("Window", typeof(RectTransform), typeof(Image));
            window.transform.SetParent(modalRoot.transform, false);
            RectTransform winRt = window.GetComponent<RectTransform>();
            winRt.anchorMin = new Vector2(0.5f, 0.5f);
            winRt.anchorMax = new Vector2(0.5f, 0.5f);
            winRt.pivot = new Vector2(0.5f, 0.5f);
            winRt.sizeDelta = new Vector2(760f, 850f);

            Image winImg = window.GetComponent<Image>();
            if (theme != null && theme.panelFrame != null)
            {
                winImg.sprite = theme.panelFrame;
                winImg.type = Image.Type.Sliced;
                winImg.pixelsPerUnitMultiplier = 0.65f;
            }
            winImg.color = Color.white;

            // 3. Header
            GameObject header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(window.transform, false);
            RectTransform headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0f, 75f);
            headerRt.anchoredPosition = new Vector2(0f, -15f);

            var titleTxt = CreateText(header.transform, "TitleText", "GAME CONFIGURATION", 26f, FontStyles.Bold, new Color32(0xFC, 0xDA, 0x21, 0xFF), TextAlignmentOptions.Center, font);
            RectTransform titleRt = titleTxt.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.45f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.sizeDelta = Vector2.zero;

            var subtitleTxt = CreateText(header.transform, "SubtitleText", "Customize stream settings & gameplay before going live", 13.5f, FontStyles.Normal, new Color32(0xBF, 0xE3, 0xE8, 0xFF), TextAlignmentOptions.Center, font);
            RectTransform subRt = subtitleTxt.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0f, 0f);
            subRt.anchorMax = new Vector2(1f, 0.45f);
            subRt.sizeDelta = Vector2.zero;

            // Close (X) button
            GameObject closeBtnObj = CreateCloseButton(header.transform, "BtnClose", new Vector2(36f, 36f), new Color(0.85f, 0.2f, 0.25f, 0.9f), font);
            RectTransform closeRt = closeBtnObj.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-28f, -10f);

            // 4. Tab Bar (Basic vs Advanced)
            GameObject tabBar = new GameObject("TabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabBar.transform.SetParent(window.transform, false);
            RectTransform tabRt = tabBar.GetComponent<RectTransform>();
            tabRt.anchorMin = new Vector2(0.05f, 1f);
            tabRt.anchorMax = new Vector2(0.95f, 1f);
            tabRt.pivot = new Vector2(0.5f, 1f);
            tabRt.anchoredPosition = new Vector2(0f, -95f);
            tabRt.sizeDelta = new Vector2(0f, 44f);

            HorizontalLayoutGroup tabHlg = tabBar.GetComponent<HorizontalLayoutGroup>();
            tabHlg.spacing = 16f;
            tabHlg.childControlWidth = true;
            tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = true;
            tabHlg.childForceExpandHeight = true;

            GameObject btnTabBasic = CreateTabButton(tabBar.transform, "BtnTabBasic", "BASIC SETTINGS", true, font);
            GameObject btnTabAdv = CreateTabButton(tabBar.transform, "BtnTabAdvanced", "ADVANCED SETTINGS", false, font);

            // 5. Content Area
            GameObject contentArea = new GameObject("ContentArea", typeof(RectTransform));
            contentArea.transform.SetParent(window.transform, false);
            RectTransform caRt = contentArea.GetComponent<RectTransform>();
            caRt.anchorMin = new Vector2(0.04f, 0.12f);
            caRt.anchorMax = new Vector2(0.96f, 0.81f);
            caRt.sizeDelta = Vector2.zero;

            // 5A. Basic Panel Content
            GameObject panelBasic = new GameObject("PanelBasicContent", typeof(RectTransform));
            panelBasic.transform.SetParent(contentArea.transform, false);
            RectTransform pbRt = panelBasic.GetComponent<RectTransform>();
            pbRt.anchorMin = Vector2.zero;
            pbRt.anchorMax = Vector2.one;
            pbRt.sizeDelta = Vector2.zero;

            // Basic - Section 1: TikTok Username
            var userLabel = CreateText(panelBasic.transform, "LblUsername", "TIKTOK LIVE CHANNEL (@USERNAME):", 14f, FontStyles.Bold, new Color32(0xFC, 0xDA, 0x21, 0xFF), TextAlignmentOptions.Left, font);
            RectTransform ulRt = userLabel.GetComponent<RectTransform>();
            ulRt.anchorMin = new Vector2(0f, 1f);
            ulRt.anchorMax = new Vector2(1f, 1f);
            ulRt.pivot = new Vector2(0f, 1f);
            ulRt.anchoredPosition = new Vector2(10f, 0f);
            ulRt.sizeDelta = new Vector2(-20f, 22f);

            GameObject inputUserObj = CreateInputField(panelBasic.transform, "InputUsername", "Enter TikTok live @username...", 16f, font);
            RectTransform iuRt = inputUserObj.GetComponent<RectTransform>();
            iuRt.anchorMin = new Vector2(0f, 1f);
            iuRt.anchorMax = new Vector2(1f, 1f);
            iuRt.pivot = new Vector2(0f, 1f);
            iuRt.anchoredPosition = new Vector2(10f, -26f);
            iuRt.sizeDelta = new Vector2(-20f, 40f);

            // Basic - Section 2: Target Distance
            var distLabel = CreateText(panelBasic.transform, "LblDistance", "TARGET DISTANCE (KM):", 14f, FontStyles.Bold, new Color32(0xFC, 0xDA, 0x21, 0xFF), TextAlignmentOptions.Left, font);
            RectTransform dlRt = distLabel.GetComponent<RectTransform>();
            dlRt.anchorMin = new Vector2(0f, 1f);
            dlRt.anchorMax = new Vector2(1f, 1f);
            dlRt.pivot = new Vector2(0f, 1f);
            dlRt.anchoredPosition = new Vector2(10f, -76f);
            dlRt.sizeDelta = new Vector2(-20f, 22f);

            GameObject distRow = new GameObject("DistanceRow", typeof(RectTransform));
            distRow.transform.SetParent(panelBasic.transform, false);
            RectTransform drRt = distRow.GetComponent<RectTransform>();
            drRt.anchorMin = new Vector2(0f, 1f);
            drRt.anchorMax = new Vector2(1f, 1f);
            drRt.pivot = new Vector2(0f, 1f);
            drRt.anchoredPosition = new Vector2(10f, -102f);
            drRt.sizeDelta = new Vector2(-20f, 38f);

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
            var giftsLabel = CreateText(panelBasic.transform, "LblGifts", "GIFTS & ACTIONS (FEATURE - SELECT GIFT - CARD DESCRIPTION):", 13.5f, FontStyles.Bold, new Color32(0xFC, 0xDA, 0x21, 0xFF), TextAlignmentOptions.Left, font);
            RectTransform glRt = giftsLabel.GetComponent<RectTransform>();
            glRt.anchorMin = new Vector2(0f, 1f);
            glRt.anchorMax = new Vector2(1f, 1f);
            glRt.pivot = new Vector2(0f, 1f);
            glRt.anchoredPosition = new Vector2(10f, -150f);
            glRt.sizeDelta = new Vector2(-20f, 20f);

            // Table Column Header
            GameObject tableHeader = new GameObject("TableHeader", typeof(RectTransform), typeof(Image));
            tableHeader.transform.SetParent(panelBasic.transform, false);
            RectTransform thRt = tableHeader.GetComponent<RectTransform>();
            thRt.anchorMin = new Vector2(0f, 1f);
            thRt.anchorMax = new Vector2(1f, 1f);
            thRt.pivot = new Vector2(0f, 1f);
            thRt.anchoredPosition = new Vector2(10f, -172f);
            thRt.sizeDelta = new Vector2(-20f, 26f);

            Image thBg = tableHeader.GetComponent<Image>();
            thBg.color = new Color(0.06f, 0.16f, 0.24f, 0.95f);

            var thActive = CreateText(tableHeader.transform, "ThActive", "[V]", 10.5f, FontStyles.Bold, new Color32(0x00, 0xE5, 0xFF, 0xFF), TextAlignmentOptions.Center, font);
            SetRect(thActive.GetComponent<RectTransform>(), 8f, 0f, 30f, 24f);

            var thFeat = CreateText(tableHeader.transform, "ThFeature", "FEATURE", 11f, FontStyles.Bold, new Color32(0x00, 0xE5, 0xFF, 0xFF), TextAlignmentOptions.MidlineLeft, font);
            SetRect(thFeat.GetComponent<RectTransform>(), 44f, 0f, 170f, 24f);

            var thGift = CreateText(tableHeader.transform, "ThGift", "TIKTOK GIFT", 11f, FontStyles.Bold, new Color32(0x00, 0xE5, 0xFF, 0xFF), TextAlignmentOptions.MidlineLeft, font);
            SetRect(thGift.GetComponent<RectTransform>(), 218f, 0f, 175f, 24f);

            var thDesc = CreateText(tableHeader.transform, "ThDesc", "DESCRIPTION", 11f, FontStyles.Bold, new Color32(0x00, 0xE5, 0xFF, 0xFF), TextAlignmentOptions.MidlineLeft, font);
            SetRect(thDesc.GetComponent<RectTransform>(), 398f, 0f, 200f, 24f);

            var thOrder = CreateText(tableHeader.transform, "ThOrder", "ORDER", 10.5f, FontStyles.Bold, new Color32(0x00, 0xE5, 0xFF, 0xFF), TextAlignmentOptions.Center, font);
            SetRect(thOrder.GetComponent<RectTransform>(), 604f, 0f, 52f, 24f);

            // ScrollView for gifts
            GameObject scrollObj = new GameObject("GiftsScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollObj.transform.SetParent(panelBasic.transform, false);
            RectTransform svRt = scrollObj.GetComponent<RectTransform>();
            svRt.anchorMin = new Vector2(0f, 0f);
            svRt.anchorMax = new Vector2(1f, 1f);
            svRt.offsetMin = new Vector2(10f, 10f);
            svRt.offsetMax = new Vector2(-10f, -202f);

            Image svBg = scrollObj.GetComponent<Image>();
            svBg.color = new Color(0.04f, 0.12f, 0.18f, 0.9f);

            ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;

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
            gcVlg.spacing = 6f;
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

            // 5B. Advanced Panel Content
            GameObject panelAdv = new GameObject("PanelAdvancedContent", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            panelAdv.transform.SetParent(contentArea.transform, false);
            RectTransform paRt = panelAdv.GetComponent<RectTransform>();
            paRt.anchorMin = Vector2.zero;
            paRt.anchorMax = Vector2.one;
            paRt.sizeDelta = Vector2.zero;

            panelAdv.GetComponent<Image>().color = new Color(0.04f, 0.12f, 0.18f, 0.9f);
            ScrollRect srAdv = panelAdv.GetComponent<ScrollRect>();
            srAdv.horizontal = false;
            srAdv.vertical = true;

            GameObject vpAdv = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            vpAdv.transform.SetParent(panelAdv.transform, false);
            RectTransform vpAdvRt = vpAdv.GetComponent<RectTransform>();
            vpAdvRt.anchorMin = Vector2.zero;
            vpAdvRt.anchorMax = Vector2.one;
            vpAdvRt.sizeDelta = Vector2.zero;
            vpAdv.GetComponent<Image>().color = Color.white;
            vpAdv.GetComponent<Mask>().showMaskGraphic = false;
            srAdv.viewport = vpAdvRt;

            GameObject advContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            advContent.transform.SetParent(vpAdv.transform, false);
            RectTransform acRt = advContent.GetComponent<RectTransform>();
            acRt.anchorMin = new Vector2(0f, 1f);
            acRt.anchorMax = new Vector2(1f, 1f);
            acRt.pivot = new Vector2(0.5f, 1f);
            acRt.sizeDelta = Vector2.zero;

            VerticalLayoutGroup acVlg = advContent.GetComponent<VerticalLayoutGroup>();
            acVlg.spacing = 10f;
            acVlg.padding = new RectOffset(16, 16, 16, 16);
            acVlg.childControlWidth = true;
            acVlg.childControlHeight = false;
            acVlg.childForceExpandWidth = true;

            ContentSizeFitter acCsf = advContent.GetComponent<ContentSizeFitter>();
            acCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            srAdv.content = acRt;

            // SECTION 1: FAN & RUNNER ENERGY
            CreateHeaderBanner(advContent.transform, "FAN FACTION & RUNNER ENERGY", new Color32(0x40, 0xAD, 0xFF, 0xFF), font);
            GameObject rowLaneCost = CreateSettingField(advContent.transform, "Lane change energy cost (default: 10):", "10", font);
            GameObject rowJumpCost = CreateSettingField(advContent.transform, "Jump energy cost (default: 20):", "20", font);
            GameObject rowFreeDur = CreateSettingField(advContent.transform, "Freedom Charm duration (sec, default: 30s):", "30", font);
            GameObject rowSprintDur = CreateSettingField(advContent.transform, "Sprint Boost duration (sec, default: 30s):", "30", font);

            // SECTION 2: ANTI & TRAFFIC HAZARDS
            CreateHeaderBanner(advContent.transform, "ANTI FACTION & TRAFFIC HAZARDS", new Color32(0xFF, 0x4D, 0x57, 0xFF), font);
            GameObject rowAutoSpawn = CreateToggleField(advContent.transform, "Auto-spawn car at 100% energy:", true, font);
            GameObject rowMaxCars = CreateSettingField(advContent.transform, "Max concurrent cars on 3 lanes (1 - 3):", "2", font);

            // SECTION 3: COLLISION PENALTIES
            CreateHeaderBanner(advContent.transform, "COLLISION PENALTIES", new Color32(0xFC, 0xDA, 0x21, 0xFF), font);
            GameObject rowDistPen = CreateSettingField(advContent.transform, "Distance penalty on crash (meters, default: 100m):", "100", font);
            GameObject rowSedanPen = CreateSettingField(advContent.transform, "Energy lost on Sedan hit (%):", "20", font);
            GameObject rowPickupPen = CreateSettingField(advContent.transform, "Energy lost on Thú săn hit (%):", "40", font);
            GameObject rowHeavyPen = CreateSettingField(advContent.transform, "Energy lost on Tàu hỏa hit (%):", "60", font);

            // SECTION 4: OFFLINE LIVE STREAM SIMULATION
            CreateHeaderBanner(advContent.transform, "OFFLINE LIVE STREAM SIMULATION", new Color32(0x00, 0xE5, 0xFF, 0xFF), font);
            GameObject rowLiveMaster = CreateToggleField(advContent.transform, "Auto live simulation in test mode:", true, font);
            GameObject rowSimChats = CreateToggleField(advContent.transform, "Simulate chat comments:", true, font);
            GameObject rowSimGifts = CreateToggleField(advContent.transform, "Simulate viewer gifts:", true, font);
            GameObject rowSimLikes = CreateToggleField(advContent.transform, "Simulate continuous hearts (Likes):", true, font);
            GameObject rowSimFollowers = CreateToggleField(advContent.transform, "Simulate baton handover (Follow):", false, font);
            GameObject rowSimDelay = CreateToggleField(advContent.transform, "Simulate stream broadcast delay (1.5s - 3.0s):", false, font);

            // SECTION 5: DEBUG & TESTING TOOLS
            CreateHeaderBanner(advContent.transform, "DEBUG & TESTING TOOLS", new Color32(0x9B, 0x51, 0xE0, 0xFF), font);
            GameObject rowDebugUI = CreateToggleField(advContent.transform, "Show Debug UI in game (Toggle '~', default: OFF):", false, font);

            panelAdv.SetActive(false);

            // 6. Bottom Action Bar
            GameObject bottomBar = new GameObject("BottomBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bottomBar.transform.SetParent(window.transform, false);
            RectTransform bbRt = bottomBar.GetComponent<RectTransform>();
            bbRt.anchorMin = new Vector2(0.04f, 0f);
            bbRt.anchorMax = new Vector2(0.96f, 0f);
            bbRt.pivot = new Vector2(0.5f, 0f);
            bbRt.anchoredPosition = new Vector2(0f, 20f);
            bbRt.sizeDelta = new Vector2(0f, 52f);

            HorizontalLayoutGroup bbHlg = bottomBar.GetComponent<HorizontalLayoutGroup>();
            bbHlg.spacing = 14f;
            bbHlg.childControlWidth = true;
            bbHlg.childControlHeight = true;
            bbHlg.childForceExpandWidth = true;
            bbHlg.childForceExpandHeight = true;

            GameObject btnReset = CreateButton(bottomBar.transform, "BtnResetDefaults", "RESET DEFAULTS", Vector2.zero, new Color(0.25f, 0.32f, 0.38f, 0.95f), Color.white, 15f, font);
            GameObject btnTest = CreateButton(bottomBar.transform, "BtnTestMode", "TEST RUN (OFFLINE)", Vector2.zero, new Color32(0x00, 0xC8, 0x53, 0xFF), Color.white, 16f, font);
            GameObject btnLive = CreateButton(bottomBar.transform, "BtnGoLive", "START LIVE STREAM", Vector2.zero, new Color32(0xFE, 0x2C, 0x55, 0xFF), Color.white, 17f, font);

            // 7. Wire UI script component
            PreGameConfigUI configUI = modalRoot.AddComponent<PreGameConfigUI>();
            WireConfigUIFields(configUI,
                btnTabBasic, btnTabAdv, panelBasic, panelAdv,
                inputUserObj.GetComponent<TMP_InputField>(),
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
                rowDebugUI.GetComponentInChildren<Toggle>(),
                btnReset.GetComponent<Button>(), btnTest.GetComponent<Button>(), btnLive.GetComponent<Button>(), closeBtnObj.GetComponent<Button>()
            );

            // 8. Ensure Manager component on Canvas
            PreGameConfigManager manager = canvas.GetComponent<PreGameConfigManager>();
            if (manager == null) manager = canvas.gameObject.AddComponent<PreGameConfigManager>();
            SetPrivateField(manager, "_configUI", configUI);

            // 9. Create HUD Gear Button to re-open setup anytime
            CreateHudGearButton(canvas, manager, font);
            CreateHudAudioButton(canvas);

            return modalRoot;
        }

        public static void CreateHudAudioButton(Canvas canvas)
        {
            if (canvas == null) return;

            Transform existingBtn = canvas.transform.Find("Btn_ToggleAudio");
            if (existingBtn != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(existingBtn.gameObject);
                else UnityEngine.Object.Destroy(existingBtn.gameObject);
#else
                UnityEngine.Object.Destroy(existingBtn.gameObject);
#endif
            }

            GameObject btnObj = new GameObject("Btn_ToggleAudio", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(Views.AudioToggleController));
            btnObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(144f, 24f);
            rt.sizeDelta = new Vector2(42f, 38f);

            var bgImg = btnObj.GetComponent<Image>();
            bgImg.color = new Color(0.08f, 0.22f, 0.32f, 0.95f);

            var outline = btnObj.GetComponent<Outline>();
            outline.effectColor = new Color(0.0f, 0.85f, 1.0f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var btn = btnObj.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.7f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.5f, 0.8f, 0.9f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            btn.targetGraphic = bgImg;

            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(btnObj.transform, false);

            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = new Vector2(26f, 26f);

            var iconImg = iconObj.GetComponent<Image>();
#if UNITY_EDITOR
            var soundOnSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hyper_Casual_UI/Sprites/Icons/soundon.png");
            var soundOffSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hyper_Casual_UI/Sprites/Icons/soundoff.png");
#else
            var soundOnSprite = Resources.Load<Sprite>("soundon");
            var soundOffSprite = Resources.Load<Sprite>("soundoff");
#endif
            iconImg.sprite = soundOnSprite;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.color = Color.white;

            var controller = btnObj.GetComponent<Views.AudioToggleController>();
#if UNITY_EDITOR
            controller.ConfigureReferences(iconImg, bgImg, outline, null, soundOnSprite, soundOffSprite);
#endif
        }

        private static Sprite GetGearSprite()
        {
            var sprite = Resources.Load<Sprite>("Icons/White Gear 1");
#if UNITY_EDITOR
            if (sprite == null)
                sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/White Icons/White Gear 1.png");
#endif
            return sprite;
        }

        private static Sprite GetCloseSprite()
        {
            var sprite = Resources.Load<Sprite>("Icons/White Close");
#if UNITY_EDITOR
            if (sprite == null)
                sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/White Icons/White Close.png");
#endif
            return sprite;
        }

        private static void CreateHudGearButton(Canvas canvas, PreGameConfigManager manager, TMP_FontAsset font)
        {
            Transform existingBtn = canvas.transform.Find("Btn_OpenPreGameConfig");
            if (existingBtn != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(existingBtn.gameObject);
                else UnityEngine.Object.Destroy(existingBtn.gameObject);
#else
                UnityEngine.Object.Destroy(existingBtn.gameObject);
#endif
            }

            GameObject gearObj = new GameObject("Btn_OpenPreGameConfig", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            gearObj.transform.SetParent(canvas.transform, false);

            RectTransform grt = gearObj.GetComponent<RectTransform>();
            grt.anchorMin = new Vector2(0f, 0f);
            grt.anchorMax = new Vector2(0f, 0f);
            grt.pivot = new Vector2(0f, 0f);
            grt.anchoredPosition = new Vector2(24f, 24f);
            grt.sizeDelta = new Vector2(116f, 38f);

            var bgImg = gearObj.GetComponent<Image>();
            bgImg.color = new Color(0.08f, 0.22f, 0.32f, 0.95f);

            var outline = gearObj.GetComponent<Outline>();
            outline.effectColor = new Color(0.0f, 0.85f, 1.0f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            Button btn = gearObj.GetComponent<Button>();
            btn.targetGraphic = bgImg;
            btn.onClick.AddListener(() =>
            {
                manager?.OpenConfigUI();
            });

            Sprite gearSprite = GetGearSprite();
            if (gearSprite != null)
            {
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(gearObj.transform, false);
                RectTransform iconRt = iconObj.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(12f, 0f);
                iconRt.sizeDelta = new Vector2(18f, 18f);

                Image iconImg = iconObj.GetComponent<Image>();
                iconImg.sprite = gearSprite;
                iconImg.color = Color.white;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;

                var txt = CreateText(gearObj.transform, "Text", "Settings", 14f, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft, font);
                RectTransform trt = txt.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(36f, 0f);
                trt.offsetMax = new Vector2(-6f, 0f);
            }
            else
            {
                var txt = CreateText(gearObj.transform, "Text", "Settings", 14f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, font);
                RectTransform trt = txt.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.sizeDelta = Vector2.zero;
            }
        }

        private static void SetRect(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        private static GameObject CreateDropdownControl(Transform parent, string name, TMP_FontAsset font)
        {
            var res = new TMPro.TMP_DefaultControls.Resources();
            GameObject dropdownObj = TMPro.TMP_DefaultControls.CreateDropdown(res);
            dropdownObj.name = name;
            dropdownObj.transform.SetParent(parent, false);

            var dropdown = dropdownObj.GetComponent<TMP_Dropdown>();
            if (dropdown != null)
            {
                if (dropdown.captionText != null)
                {
                    if (font != null) dropdown.captionText.font = font;
                    dropdown.captionText.fontSize = 11.5f;
                    dropdown.captionText.alignment = TextAlignmentOptions.MidlineLeft;
                    dropdown.captionText.color = Color.white;
                }

                if (dropdown.itemText != null)
                {
                    if (font != null) dropdown.itemText.font = font;
                    dropdown.itemText.fontSize = 11.5f;
                    dropdown.itemText.alignment = TextAlignmentOptions.MidlineLeft;
                }

                if (dropdown.template != null)
                {
                    var c = dropdown.template.gameObject.AddComponent<Canvas>();
                    c.overrideSorting = true;
                    c.sortingOrder = 3000;
                    dropdown.template.gameObject.AddComponent<GraphicRaycaster>();
                    dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 150f);
                }
            }

            Image bg = dropdownObj.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.12f, 0.24f, 0.35f, 0.95f);

            return dropdownObj;
        }

        private static GameObject CreateGiftRowTemplate(Transform parent, TMP_FontAsset font)
        {
            GameObject row = new GameObject("GiftRowTemplate", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(parent, false);
            RectTransform rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 46f);

            Image bg = row.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.2f, 0.28f, 0.85f);

            // 1. Enable Toggle [V]
            GameObject toggleObj = CreateSimpleToggle(row.transform, "ToggleEnable", font);
            RectTransform togRt = toggleObj.GetComponent<RectTransform>();
            SetRect(togRt, 8f, 0f, 30f, 30f);

            // 2. Feature Name
            var featTxt = CreateText(row.transform, "FeatureNameText", "Feature Name", 12.5f, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft, font);
            RectTransform fnRt = featTxt.GetComponent<RectTransform>();
            SetRect(fnRt, 44f, 0f, 170f, 34f);

            // 3. Gift Selection (Icon + Dropdown)
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(row.transform, false);
            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            SetRect(iconRt, 218f, 0f, 30f, 30f);
            Image iconImg = iconObj.GetComponent<Image>();
            iconImg.preserveAspect = true;

            GameObject dropdownObj = CreateDropdownControl(row.transform, "GiftDropdown", font);
            RectTransform ddRt = dropdownObj.GetComponent<RectTransform>();
            SetRect(ddRt, 252f, 0f, 140f, 32f);

            // 4. Description Input
            GameObject descInput = CreateInputField(row.transform, "DescInput", "Card description...", 12f, font);
            RectTransform diRt = descInput.GetComponent<RectTransform>();
            SetRect(diRt, 398f, 0f, 200f, 32f);

            // 5. Reorder Buttons (▲ ▼)
            GameObject btnUp = CreateButton(row.transform, "BtnUp", "▲", new Vector2(24f, 26f), new Color(0.18f, 0.35f, 0.48f, 0.9f), Color.white, 11f, font);
            RectTransform upRt = btnUp.GetComponent<RectTransform>();
            SetRect(upRt, 604f, 0f, 24f, 26f);

            GameObject btnDown = CreateButton(row.transform, "BtnDown", "▼", new Vector2(24f, 26f), new Color(0.18f, 0.35f, 0.48f, 0.9f), Color.white, 11f, font);
            RectTransform downRt = btnDown.GetComponent<RectTransform>();
            SetRect(downRt, 632f, 0f, 24f, 26f);

            return row;
        }

        private static GameObject CreateSimpleToggle(Transform parent, string name, TMP_FontAsset font)
        {
            GameObject toggleObj = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            toggleObj.transform.SetParent(parent, false);
            RectTransform trt = toggleObj.GetComponent<RectTransform>();
            trt.sizeDelta = new Vector2(40f, 28f);

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(toggleObj.transform, false);
            RectTransform bgRt = bg.GetComponent<RectTransform>();
            bgRt.sizeDelta = new Vector2(28f, 28f);
            Image bgImg = bg.GetComponent<Image>();
            bgImg.color = new Color(0.15f, 0.28f, 0.38f, 1f);

            GameObject check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(bg.transform, false);
            RectTransform chkRt = check.GetComponent<RectTransform>();
            chkRt.sizeDelta = new Vector2(20f, 20f);
            Image chkImg = check.GetComponent<Image>();
            chkImg.color = new Color32(0x00, 0xE5, 0xFF, 0xFF);

            Toggle t = toggleObj.GetComponent<Toggle>();
            t.targetGraphic = bgImg;
            t.graphic = chkImg;
            t.isOn = true;

            return toggleObj;
        }

        private static GameObject CreateHeaderBanner(Transform parent, string title, Color color, TMP_FontAsset font)
        {
            GameObject obj = new GameObject("Banner_" + title, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 32f);
            Image img = obj.GetComponent<Image>();
            img.color = new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, 0.95f);

            var txt = CreateText(obj.transform, "Text", title, 14f, FontStyles.Bold, color, TextAlignmentOptions.Left, font);
            RectTransform trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(12f, 0f);
            trt.offsetMax = new Vector2(-12f, 0f);

            return obj;
        }

        private static GameObject CreateSettingField(Transform parent, string label, string defaultValue, TMP_FontAsset font)
        {
            GameObject row = new GameObject("Row_" + label, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 36f);

            var lblTxt = CreateText(row.transform, "Label", label, 13.5f, FontStyles.Normal, Color.white, TextAlignmentOptions.Left, font);
            RectTransform lrt = lblTxt.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(0.7f, 1f);
            lrt.offsetMin = new Vector2(6f, 0f);
            lrt.offsetMax = Vector2.zero;

            GameObject inputObj = CreateInputField(row.transform, "Input", defaultValue, 14f, font);
            RectTransform irt = inputObj.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.72f, 0f);
            irt.anchorMax = new Vector2(1f, 1f);
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = new Vector2(-6f, 0f);

            return row;
        }

        private static GameObject CreateToggleField(Transform parent, string label, bool defaultValue, TMP_FontAsset font)
        {
            GameObject row = new GameObject("Row_" + label, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 36f);

            var lblTxt = CreateText(row.transform, "Label", label, 13.5f, FontStyles.Normal, Color.white, TextAlignmentOptions.Left, font);
            RectTransform lrt = lblTxt.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(0.8f, 1f);
            lrt.offsetMin = new Vector2(6f, 0f);
            lrt.offsetMax = Vector2.zero;

            GameObject togObj = CreateSimpleToggle(row.transform, "Toggle", font);
            RectTransform trt = togObj.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.85f, 0.5f);
            trt.anchorMax = new Vector2(0.85f, 0.5f);
            trt.anchoredPosition = Vector2.zero;

            togObj.GetComponent<Toggle>().isOn = defaultValue;
            return row;
        }

        private static GameObject CreateTabButton(Transform parent, string name, string label, bool isActive, TMP_FontAsset font)
        {
            Color activeColor = new Color32(0xFC, 0xDA, 0x21, 0xFF);
            Color inactiveColor = new Color32(0x20, 0x61, 0x72, 0xFF);

            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            Image img = btnObj.GetComponent<Image>();
            img.color = isActive ? activeColor : inactiveColor;

            var txt = CreateText(btnObj.transform, "Text", label, 15f, FontStyles.Bold, isActive ? Color.black : Color.white, TextAlignmentOptions.Center, font);
            RectTransform trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;

            return btnObj;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, Vector2 sizeDelta, Color bgColor, Color textColor, float fontSize, TMP_FontAsset font)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            if (sizeDelta != Vector2.zero) rt.sizeDelta = sizeDelta;

            Image img = btnObj.GetComponent<Image>();
            img.color = bgColor;

            var txt = CreateText(btnObj.transform, "Text", label, fontSize, FontStyles.Bold, textColor, TextAlignmentOptions.Center, font);
            RectTransform trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;

            return btnObj;
        }

        private static GameObject CreateCloseButton(Transform parent, string name, Vector2 sizeDelta, Color bgColor, TMP_FontAsset font)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            if (sizeDelta != Vector2.zero) rt.sizeDelta = sizeDelta;

            Image img = btnObj.GetComponent<Image>();
            img.color = bgColor;

            Sprite closeSprite = GetCloseSprite();
            if (closeSprite != null)
            {
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(btnObj.transform, false);
                RectTransform iconRt = iconObj.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;
                iconRt.sizeDelta = new Vector2(18f, 18f);

                Image iconImg = iconObj.GetComponent<Image>();
                iconImg.sprite = closeSprite;
                iconImg.color = Color.white;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }
            else
            {
                var txt = CreateText(btnObj.transform, "Text", "X", 18f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, font);
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
            bg.color = new Color(0.08f, 0.18f, 0.25f, 0.95f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(root.transform, false);
            RectTransform taRt = textArea.GetComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(10f, 4f);
            taRt.offsetMax = new Vector2(-10f, -4f);

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
            phTxt.color = new Color(0.6f, 0.75f, 0.85f, 0.5f);
            phTxt.alignment = TextAlignmentOptions.Left;

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
            tTxt.alignment = TextAlignmentOptions.Left;

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
            TMP_InputField inTargetDist, Toggle togInfiniteDist,
            Transform giftsContainer, GameObject giftRowTemplate,
            TMP_InputField inLaneCost, TMP_InputField inJumpCost, TMP_InputField inFreeDur, TMP_InputField inSprintDur,
            Toggle togAutoSpawn, TMP_InputField inMaxCars, TMP_InputField inDistPen, TMP_InputField inSedanPen, TMP_InputField inPickupPen, TMP_InputField inHeavyPen,
            Toggle togLiveMaster, Toggle togSimChats, Toggle togSimGifts, Toggle togSimLikes, Toggle togSimFollowers, Toggle togSimDelay,
            Toggle togDebugUI,
            Button btnReset, Button btnTest, Button btnLive, Button btnClose)
        {
            SetPrivateField(ui, "_btnTabBasic", btnTabBasic.GetComponent<Button>());
            SetPrivateField(ui, "_btnTabAdvanced", btnTabAdv.GetComponent<Button>());
            SetPrivateField(ui, "_panelBasicContent", panelBasic);
            SetPrivateField(ui, "_panelAdvancedContent", panelAdv);
            SetPrivateField(ui, "_tabBasicHighlight", btnTabBasic.GetComponent<Image>());
            SetPrivateField(ui, "_tabAdvancedHighlight", btnTabAdv.GetComponent<Image>());

            SetPrivateField(ui, "_inputUsername", inputUsername);
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
            SetPrivateField(ui, "_toggleDebugUI", togDebugUI);

            SetPrivateField(ui, "_btnResetDefaults", btnReset);
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

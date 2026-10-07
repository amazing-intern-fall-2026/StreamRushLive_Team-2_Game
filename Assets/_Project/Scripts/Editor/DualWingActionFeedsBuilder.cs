#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SteamRush.Features.UI;
using SteamRush.Features.UI.Views;

namespace SteamRush.EditorTools
{
    public static class DualWingActionFeedsBuilder
    {
        [MenuItem("Tools/StreamRush/Build Dual Wing Action Feeds")]
        public static void Build()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            Canvas canvas = null;
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.gameObject.name == "Canvas")
                {
                    canvas = c;
                    break;
                }
            }

            if (canvas == null)
            {
                Debug.LogError("[DualWingActionFeedsBuilder] Screen Space Canvas not found in scene!");
                return;
            }

            // Clean up any improperly parented containers
            var wrongFans = Object.FindObjectsByType<GiftToastQueue>(FindObjectsSortMode.None);
            foreach (var w in wrongFans)
            {
                if (w.gameObject.name == "FanFeedContainer" || w.gameObject.name == "AntiFeedContainer")
                {
                    Object.DestroyImmediate(w.gameObject);
                }
            }

            Sprite pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Textures/Generated/UI_BannerPill.png");
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Baloo2-ExtraBold SDF.asset");

            // ==========================================
            // 1. FAN ACTION FEED (LEFT WING)
            // ==========================================
            Transform existingFan = canvas.transform.Find("FanFeedContainer");
            if (existingFan != null) Object.DestroyImmediate(existingFan.gameObject);

            GameObject fanContainer = new GameObject("FanFeedContainer", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(fanContainer, "Create FanFeedContainer");
            fanContainer.transform.SetParent(canvas.transform, false);

            RectTransform fanRt = fanContainer.GetComponent<RectTransform>();
            fanRt.anchorMin = new Vector2(0f, 0.72f);
            fanRt.anchorMax = new Vector2(0f, 0.72f);
            fanRt.pivot = new Vector2(0f, 1f);
            fanRt.anchoredPosition = new Vector2(36f, 0f);
            fanRt.sizeDelta = new Vector2(340f, 0f);

            VerticalLayoutGroup fanVlg = fanContainer.AddComponent<VerticalLayoutGroup>();
            fanVlg.spacing = 6f;
            fanVlg.childAlignment = TextAnchor.UpperLeft;
            fanVlg.childControlWidth = true;
            fanVlg.childControlHeight = true;
            fanVlg.childForceExpandWidth = true;
            fanVlg.childForceExpandHeight = false;

            ContentSizeFitter fanCsf = fanContainer.AddComponent<ContentSizeFitter>();
            fanCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fanCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            GiftToastQueue fanQueue = fanContainer.AddComponent<GiftToastQueue>();

            // Fan Template
            GameObject fanTemplate = new GameObject("FanFeed_Template", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(HorizontalLayoutGroup), typeof(Outline), typeof(LayoutElement), typeof(GiftToastController));
            fanTemplate.transform.SetParent(fanContainer.transform, false);

            RectTransform fanTempRt = fanTemplate.GetComponent<RectTransform>();
            fanTempRt.sizeDelta = new Vector2(340f, 38f);

            LayoutElement fanItemLe = fanTemplate.GetComponent<LayoutElement>();
            fanItemLe.minHeight = 38f;
            fanItemLe.preferredHeight = 38f;
            fanItemLe.flexibleHeight = 0f;

            Image fanBg = fanTemplate.GetComponent<Image>();
            fanBg.sprite = pillSprite;
            fanBg.type = Image.Type.Sliced;
            fanBg.color = new Color(0.04f, 0.10f, 0.16f, 0.94f);

            Outline fanOutline = fanTemplate.GetComponent<Outline>();
            fanOutline.effectColor = new Color(0.0f, 0.9f, 1.0f, 0.85f);
            fanOutline.effectDistance = new Vector2(1.5f, -1.5f);

            HorizontalLayoutGroup fanHlg = fanTemplate.GetComponent<HorizontalLayoutGroup>();
            fanHlg.padding = new RectOffset(12, 14, 4, 4);
            fanHlg.spacing = 6f;
            fanHlg.childAlignment = TextAnchor.MiddleLeft;
            fanHlg.childControlWidth = true;
            fanHlg.childControlHeight = true;
            fanHlg.childForceExpandWidth = false;
            fanHlg.childForceExpandHeight = false;

            // Fan Icon
            GameObject fanIconGo = new GameObject("Feed_Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            fanIconGo.transform.SetParent(fanTemplate.transform, false);
            Image fanIconImg = fanIconGo.GetComponent<Image>();
            fanIconImg.preserveAspect = true;
            fanIconImg.color = new Color(0.2f, 0.85f, 1f, 1f);
            LayoutElement fanIconLe = fanIconGo.GetComponent<LayoutElement>();
            fanIconLe.preferredWidth = 22f;
            fanIconLe.preferredHeight = 22f;
            fanIconLe.minWidth = 22f;
            fanIconLe.minHeight = 22f;

            // Fan SenderName
            GameObject fanNameGo = new GameObject("Feed_SenderName", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            fanNameGo.transform.SetParent(fanTemplate.transform, false);
            TextMeshProUGUI fanNameTxt = fanNameGo.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) fanNameTxt.font = fontAsset;
            fanNameTxt.fontSize = 15f;
            fanNameTxt.color = Color.white;
            fanNameTxt.fontStyle = FontStyles.Bold;
            fanNameTxt.text = "[FanUser]";
            fanNameTxt.alignment = TextAlignmentOptions.MidlineLeft;
            fanNameTxt.overflowMode = TextOverflowModes.Overflow;
            LayoutElement fanNameLe = fanNameGo.GetComponent<LayoutElement>();
            fanNameLe.flexibleWidth = 0f;

            // Fan Action
            GameObject fanActionGo = new GameObject("Feed_Action", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            fanActionGo.transform.SetParent(fanTemplate.transform, false);
            TextMeshProUGUI fanActionTxt = fanActionGo.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) fanActionTxt.font = fontAsset;
            fanActionTxt.fontSize = 15f;
            fanActionTxt.color = new Color(0.22f, 0.88f, 1.0f, 1f);
            fanActionTxt.fontStyle = FontStyles.Bold;
            fanActionTxt.text = "Lane 2 (Middle)";
            fanActionTxt.alignment = TextAlignmentOptions.MidlineLeft;
            fanActionTxt.overflowMode = TextOverflowModes.Overflow;
            LayoutElement fanActionLe = fanActionGo.GetComponent<LayoutElement>();
            fanActionLe.flexibleWidth = 1f;

            // Fan GiftToastController
            GiftToastController fanToastCtrl = fanTemplate.GetComponent<GiftToastController>();
            SerializedObject fanSo = new SerializedObject(fanToastCtrl);
            fanSo.FindProperty("iconImage").objectReferenceValue = fanIconImg;
            fanSo.FindProperty("viewerNameText").objectReferenceValue = fanNameTxt;
            fanSo.FindProperty("itemNameText").objectReferenceValue = fanActionTxt;
            fanSo.FindProperty("canvasGroup").objectReferenceValue = fanTemplate.GetComponent<CanvasGroup>();
            fanSo.FindProperty("cardOutline").objectReferenceValue = fanOutline;
            fanSo.FindProperty("showDuration").floatValue = 1.8f;
            fanSo.FindProperty("animDuration").floatValue = 0.2f;
            fanSo.FindProperty("hideIconIfNull").boolValue = true;
            fanSo.ApplyModifiedProperties();

            fanTemplate.SetActive(false);

            SerializedObject fanQueueSo = new SerializedObject(fanQueue);
            fanQueueSo.FindProperty("toastTemplate").objectReferenceValue = fanToastCtrl;
            fanQueueSo.FindProperty("_maxConcurrentToasts").intValue = 3;
            fanQueueSo.ApplyModifiedProperties();

            // ==========================================
            // 2. ANTI ACTION FEED (RIGHT WING)
            // ==========================================
            Transform existingAnti = canvas.transform.Find("AntiFeedContainer");
            if (existingAnti != null) Object.DestroyImmediate(existingAnti.gameObject);

            GameObject antiContainer = new GameObject("AntiFeedContainer", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(antiContainer, "Create AntiFeedContainer");
            antiContainer.transform.SetParent(canvas.transform, false);

            RectTransform antiRt = antiContainer.GetComponent<RectTransform>();
            antiRt.anchorMin = new Vector2(1f, 0.72f);
            antiRt.anchorMax = new Vector2(1f, 0.72f);
            antiRt.pivot = new Vector2(1f, 1f);
            antiRt.anchoredPosition = new Vector2(-36f, 0f);
            antiRt.sizeDelta = new Vector2(340f, 0f);

            VerticalLayoutGroup antiVlg = antiContainer.AddComponent<VerticalLayoutGroup>();
            antiVlg.spacing = 6f;
            antiVlg.childAlignment = TextAnchor.UpperRight;
            antiVlg.childControlWidth = true;
            antiVlg.childControlHeight = true;
            antiVlg.childForceExpandWidth = true;
            antiVlg.childForceExpandHeight = false;

            ContentSizeFitter antiCsf = antiContainer.AddComponent<ContentSizeFitter>();
            antiCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            antiCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            GiftToastQueue antiQueue = antiContainer.AddComponent<GiftToastQueue>();

            // Anti Template
            GameObject antiTemplate = new GameObject("AntiFeed_Template", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(HorizontalLayoutGroup), typeof(Outline), typeof(LayoutElement), typeof(GiftToastController));
            antiTemplate.transform.SetParent(antiContainer.transform, false);

            RectTransform antiTempRt = antiTemplate.GetComponent<RectTransform>();
            antiTempRt.sizeDelta = new Vector2(340f, 38f);

            LayoutElement antiItemLe = antiTemplate.GetComponent<LayoutElement>();
            antiItemLe.minHeight = 38f;
            antiItemLe.preferredHeight = 38f;
            antiItemLe.flexibleHeight = 0f;

            Image antiBg = antiTemplate.GetComponent<Image>();
            antiBg.sprite = pillSprite;
            antiBg.type = Image.Type.Sliced;
            antiBg.color = new Color(0.18f, 0.04f, 0.05f, 0.94f);

            Outline antiOutline = antiTemplate.GetComponent<Outline>();
            antiOutline.effectColor = new Color(1.0f, 0.28f, 0.34f, 0.85f);
            antiOutline.effectDistance = new Vector2(1.5f, -1.5f);

            HorizontalLayoutGroup antiHlg = antiTemplate.GetComponent<HorizontalLayoutGroup>();
            antiHlg.padding = new RectOffset(12, 14, 4, 4);
            antiHlg.spacing = 6f;
            antiHlg.childAlignment = TextAnchor.MiddleLeft;
            antiHlg.childControlWidth = true;
            antiHlg.childControlHeight = true;
            antiHlg.childForceExpandWidth = false;
            antiHlg.childForceExpandHeight = false;

            // Anti Icon
            GameObject antiIconGo = new GameObject("Feed_Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            antiIconGo.transform.SetParent(antiTemplate.transform, false);
            Image antiIconImg = antiIconGo.GetComponent<Image>();
            antiIconImg.preserveAspect = true;
            antiIconImg.color = new Color(1.0f, 0.4f, 0.35f, 1f);
            LayoutElement antiIconLe = antiIconGo.GetComponent<LayoutElement>();
            antiIconLe.preferredWidth = 22f;
            antiIconLe.preferredHeight = 22f;
            antiIconLe.minWidth = 22f;
            antiIconLe.minHeight = 22f;

            // Anti SenderName
            GameObject antiNameGo = new GameObject("Feed_SenderName", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            antiNameGo.transform.SetParent(antiTemplate.transform, false);
            TextMeshProUGUI antiNameTxt = antiNameGo.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) antiNameTxt.font = fontAsset;
            antiNameTxt.fontSize = 15f;
            antiNameTxt.color = Color.white;
            antiNameTxt.fontStyle = FontStyles.Bold;
            antiNameTxt.text = "[AntiUser]";
            antiNameTxt.alignment = TextAlignmentOptions.MidlineLeft;
            antiNameTxt.overflowMode = TextOverflowModes.Overflow;
            LayoutElement antiNameLe = antiNameGo.GetComponent<LayoutElement>();
            antiNameLe.flexibleWidth = 0f;

            // Anti Action
            GameObject antiActionGo = new GameObject("Feed_Action", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            antiActionGo.transform.SetParent(antiTemplate.transform, false);
            TextMeshProUGUI antiActionTxt = antiActionGo.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) antiActionTxt.font = fontAsset;
            antiActionTxt.fontSize = 15f;
            antiActionTxt.color = new Color(1.0f, 0.38f, 0.44f, 1f);
            antiActionTxt.fontStyle = FontStyles.Bold;
            antiActionTxt.text = "Spawn Car Lane 1";
            antiActionTxt.alignment = TextAlignmentOptions.MidlineLeft;
            antiActionTxt.overflowMode = TextOverflowModes.Overflow;
            LayoutElement antiActionLe = antiActionGo.GetComponent<LayoutElement>();
            antiActionLe.flexibleWidth = 1f;


            // Anti GiftToastController
            GiftToastController antiToastCtrl = antiTemplate.GetComponent<GiftToastController>();
            SerializedObject antiSo = new SerializedObject(antiToastCtrl);
            antiSo.FindProperty("iconImage").objectReferenceValue = antiIconImg;
            antiSo.FindProperty("viewerNameText").objectReferenceValue = antiNameTxt;
            antiSo.FindProperty("itemNameText").objectReferenceValue = antiActionTxt;
            antiSo.FindProperty("canvasGroup").objectReferenceValue = antiTemplate.GetComponent<CanvasGroup>();
            antiSo.FindProperty("cardOutline").objectReferenceValue = antiOutline;
            antiSo.FindProperty("showDuration").floatValue = 1.8f;
            antiSo.FindProperty("animDuration").floatValue = 0.2f;
            antiSo.FindProperty("hideIconIfNull").boolValue = true;
            antiSo.ApplyModifiedProperties();

            antiTemplate.SetActive(false);

            SerializedObject antiQueueSo = new SerializedObject(antiQueue);
            antiQueueSo.FindProperty("toastTemplate").objectReferenceValue = antiToastCtrl;
            antiQueueSo.FindProperty("_maxConcurrentToasts").intValue = 3;
            antiQueueSo.ApplyModifiedProperties();

            // ==========================================
            // 3. BIND TO HUDMANAGER
            // ==========================================
            HUDManager hud = Object.FindFirstObjectByType<HUDManager>();
            if (hud != null)
            {
                SerializedObject hudSo = new SerializedObject(hud);
                hudSo.FindProperty("fanFeedQueue").objectReferenceValue = fanQueue;
                hudSo.FindProperty("antiFeedQueue").objectReferenceValue = antiQueue;
                hudSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(hud);
                Debug.Log("[DualWingActionFeedsBuilder] Assigned fanFeedQueue and antiFeedQueue to HUDManager successfully!");
            }
            else
            {
                Debug.LogWarning("[DualWingActionFeedsBuilder] HUDManager not found in scene!");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("<color=#00FF88>[DualWingActionFeedsBuilder] INITIALIZED DUAL-WING ACTION FEEDS SUCCESSFULLY!</color>");
        }

        [MenuItem("Tools/StreamRush/Test Show Action Feeds")]
        public static void TestShowFeeds()
        {
            HUDManager hud = Object.FindFirstObjectByType<HUDManager>();
            if (hud == null)
            {
                Debug.LogWarning("[DualWingActionFeedsBuilder] HUDManager not found in scene!");
                return;
            }

            hud.ShowFanAction("Minh", "Lane 2 (Mid)");
            hud.ShowFanAction("Alex", "Jump");
            hud.ShowFanAction("Huy", "Shield");

            hud.ShowAntiAction("Dung", "Spawned Car (Lane 1)");
            hud.ShowAntiAction("Binh", "Spawned Pickup");
            hud.ShowAntiAction("Tuan", "Car Storm");

            Debug.Log("[DualWingActionFeedsBuilder] Sent test notifications to both Fan and Anti feeds.");
        }

        [MenuItem("Tools/StreamRush/Test Show Action Feeds Long")]
        public static void TestShowFeedsLong()
        {
            HUDManager hud = Object.FindFirstObjectByType<HUDManager>();
            if (hud == null) return;

            GiftToastQueue fanQueue = GameObject.Find("FanFeedContainer")?.GetComponent<GiftToastQueue>();
            GiftToastQueue antiQueue = GameObject.Find("AntiFeedContainer")?.GetComponent<GiftToastQueue>();

            var fanTemplate = fanQueue?.transform.Find("FanFeed_Template")?.GetComponent<GiftToastController>();
            if (fanTemplate != null) fanTemplate.SetDuration(8f, 0.2f);

            var antiTemplate = antiQueue?.transform.Find("AntiFeed_Template")?.GetComponent<GiftToastController>();
            if (antiTemplate != null) antiTemplate.SetDuration(8f, 0.2f);

            hud.ShowFanAction("Minh", "Lane 2 (Mid)");
            hud.ShowFanAction("Alex", "Jump");
            hud.ShowFanAction("Huy", "Shield");

            hud.ShowAntiAction("Dung", "Spawned Car (Lane 1)");
            hud.ShowAntiAction("Binh", "Spawned Pickup");
            hud.ShowAntiAction("Tuan", "Car Storm");

            Debug.Log("[DualWingActionFeedsBuilder] Sent test notifications (8s) to both Fan and Anti feeds.");
        }
    }
}
#endif

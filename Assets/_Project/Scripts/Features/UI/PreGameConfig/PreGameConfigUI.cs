using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SteamRush.Features.UI;
using SteamRush.Core.Localization;

namespace SteamRush.Features.UI.PreGameConfig
{
    /// <summary>
    /// Manager & Coordinator for the PreGameConfig modal UI.
    /// Orchestrates sub-views according to Single Responsibility Principle (SRP):
    /// - PreGameGeneralSettingsView: Manages general game parameters & demo simulation toggles.
    /// - PreGameGiftsTableView: Manages the interactive gift configurations table.
    /// - PreGameBackendSettingsView: Manages backend connectivity, ports, and lifecycle commands.
    /// - PreGameLiveVerifier: Manages live room connectivity testing and GoLive authorization.
    /// </summary>
    public class PreGameConfigUI : MonoBehaviour
    {
        [Header("Modular Sub-Views (SRP)")]
        [SerializeField] private PreGameGeneralSettingsView _generalSettingsView;
        [SerializeField] private PreGameGiftsTableView _giftsTableView;
        [SerializeField] private PreGameBackendSettingsView _backendSettingsView;
        [SerializeField] private PreGameLiveVerifier _liveVerifier;

        [Header("Tab Navigation")]
        [SerializeField] private Button _btnTabBasic;
        [SerializeField] private Button _btnTabAdvanced;
        [SerializeField] private GameObject _panelBasicContent;
        [SerializeField] private GameObject _panelAdvancedContent;
        [SerializeField] private Image _tabBasicHighlight;
        [SerializeField] private Image _tabAdvancedHighlight;

        [Header("Action Buttons")]
        [SerializeField] private Button _btnResetDefaults;
        [SerializeField] private Button _btnSaveConfig;
        [SerializeField] private Button _btnTestMode;
        [SerializeField] private Button _btnClose;

        [Header("Localization & Guide")]
        [SerializeField] private Button _btnLanguageToggle;
        [SerializeField] private TextMeshProUGUI _txtLanguageLabel;
        [SerializeField] private Button _btnHelp;
        [SerializeField] private GameObject _setupGuideModal;

        private readonly Color _activeTabColor = new Color32(0xFC, 0xDA, 0x21, 0xFF);   // Gold
        private readonly Color _inactiveTabColor = new Color32(0x20, 0x61, 0x72, 0xFF); // Dark Teal

        private void Awake()
        {
            EnsureSubViews();
            SetupButtonListeners();
        }

        private void OnEnable()
        {
            if (_setupGuideModal == null)
            {
                var gm = transform.Find("SetupGuideModal");
                if (gm != null) _setupGuideModal = gm.gameObject;
            }
            if (_setupGuideModal != null)
            {
                _setupGuideModal.SetActive(false);
            }

            SetupButtonListeners();
            LocalizationManager.OnLanguageChanged += UpdateAllLocalizedTexts;
            UpdateAllLocalizedTexts(LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLanguage : AppLanguage.English);
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= UpdateAllLocalizedTexts;
        }

        private void EnsureSubViews()
        {
            if (_generalSettingsView == null)
            {
                _generalSettingsView = GetComponentInChildren<PreGameGeneralSettingsView>(true);
                if (_generalSettingsView == null)
                {
                    var target = _panelBasicContent != null ? _panelBasicContent : gameObject;
                    _generalSettingsView = target.AddComponent<PreGameGeneralSettingsView>();
                }
            }
            _generalSettingsView.AutoWireIfNull(transform);

            if (_giftsTableView == null)
            {
                _giftsTableView = GetComponentInChildren<PreGameGiftsTableView>(true);
                if (_giftsTableView == null)
                {
                    var target = _panelBasicContent != null ? _panelBasicContent : gameObject;
                    _giftsTableView = target.AddComponent<PreGameGiftsTableView>();
                }
            }
            _giftsTableView.AutoWireIfNull(transform);

            if (_backendSettingsView == null)
            {
                _backendSettingsView = GetComponentInChildren<PreGameBackendSettingsView>(true);
                if (_backendSettingsView == null)
                {
                    var target = _panelAdvancedContent != null ? _panelAdvancedContent : gameObject;
                    _backendSettingsView = target.AddComponent<PreGameBackendSettingsView>();
                }
            }
            _backendSettingsView.AutoWireIfNull(transform);

            if (_liveVerifier == null)
            {
                _liveVerifier = GetComponentInChildren<PreGameLiveVerifier>(true);
                if (_liveVerifier == null)
                {
                    _liveVerifier = gameObject.AddComponent<PreGameLiveVerifier>();
                }
            }
            _liveVerifier.AutoWireIfNull(transform);
        }

        private void SetupButtonListeners()
        {
            if (_btnTabBasic != null) _btnTabBasic.onClick.AddListener(() => SwitchTab(true));
            if (_btnTabAdvanced != null) _btnTabAdvanced.onClick.AddListener(() => SwitchTab(false));

            if (_btnResetDefaults != null) _btnResetDefaults.onClick.AddListener(OnResetDefaultsClicked);
            if (_btnSaveConfig != null) _btnSaveConfig.onClick.AddListener(OnSaveConfigClicked);
            if (_btnTestMode != null) _btnTestMode.onClick.AddListener(OnTestModeClicked);
            if (_btnClose != null) _btnClose.onClick.AddListener(OnCloseClicked);

            if (_btnLanguageToggle == null)
            {
                var btn = transform.Find("Window/Header/BtnLanguageToggle");
                if (btn != null) _btnLanguageToggle = btn.GetComponent<Button>();
            }
            if (_txtLanguageLabel == null && _btnLanguageToggle != null)
            {
                _txtLanguageLabel = _btnLanguageToggle.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (_btnLanguageToggle != null)
            {
                _btnLanguageToggle.onClick.RemoveAllListeners();
                _btnLanguageToggle.onClick.AddListener(OnLanguageToggleClicked);
            }

            if (_btnHelp == null)
            {
                var h = transform.Find("Window/Header/BtnHelp");
                if (h != null) _btnHelp = h.GetComponent<Button>();
            }
            if (_btnHelp != null)
            {
                _btnHelp.onClick.RemoveAllListeners();
                _btnHelp.onClick.AddListener(OpenSetupGuide);
            }

            if (_setupGuideModal == null)
            {
                var gm = transform.Find("SetupGuideModal");
                if (gm != null) _setupGuideModal = gm.gameObject;
            }
            if (_setupGuideModal != null)
            {
                var btnBackdrop = _setupGuideModal.GetComponent<Button>();
                if (btnBackdrop != null)
                {
                    btnBackdrop.onClick.RemoveAllListeners();
                    btnBackdrop.onClick.AddListener(CloseSetupGuide);
                }
                var btnCloseGuide = _setupGuideModal.transform.Find("Card/BtnClose")?.GetComponent<Button>();
                if (btnCloseGuide != null)
                {
                    btnCloseGuide.onClick.RemoveAllListeners();
                    btnCloseGuide.onClick.AddListener(CloseSetupGuide);
                }
                var btnGotIt = _setupGuideModal.transform.Find("Card/BtnGotIt")?.GetComponent<Button>();
                if (btnGotIt != null)
                {
                    btnGotIt.onClick.RemoveAllListeners();
                    btnGotIt.onClick.AddListener(CloseSetupGuide);
                }
            }

            _generalSettingsView?.Initialize(onInfiniteToggled: () => { });

            if (_generalSettingsView != null && _generalSettingsView.InputUsername != null)
            {
                _generalSettingsView.InputUsername.onValueChanged.RemoveAllListeners();
                _generalSettingsView.InputUsername.onValueChanged.AddListener(newVal =>
                {
                    _liveVerifier?.OnUsernameChanged(newVal);
                });
            }

            _backendSettingsView?.Initialize(
                onBrowseClicked: OnBrowseBackendDirClicked,
                onCheckPortClicked: OnCheckPortClicked,
                onStartClicked: OnStartBackendClicked,
                onSetupClicked: OnSetupBackendClicked,
                onStopClicked: OnStopBackendClicked
            );

            _liveVerifier?.Initialize(
                onCheckLiveClicked: OnCheckLiveClicked,
                onGoLiveClicked: OnGoLiveClicked
            );
        }

        public void SwitchTab(bool isBasic)
        {
            if (_panelBasicContent != null) _panelBasicContent.SetActive(isBasic);
            if (_panelAdvancedContent != null)
            {
                _panelAdvancedContent.SetActive(!isBasic);
                if (!isBasic)
                {
                    var sr = _panelAdvancedContent.GetComponent<ScrollRect>();
                    if (sr != null) sr.normalizedPosition = new Vector2(0f, 1f);
                }
            }

            var theme = HudTheme.Current;
            Sprite pillGold = theme != null && theme.pillGold != null ? theme.pillGold : null;
            Sprite pillDark = theme != null && theme.pillDark != null ? theme.pillDark : null;

            if (_tabBasicHighlight != null)
            {
                if (pillGold != null && pillDark != null)
                {
                    _tabBasicHighlight.sprite = isBasic ? pillGold : pillDark;
                    _tabBasicHighlight.type = Image.Type.Sliced;
                    _tabBasicHighlight.color = Color.white;
                }
                else
                {
                    _tabBasicHighlight.color = isBasic ? _activeTabColor : _inactiveTabColor;
                }
            }

            if (_tabAdvancedHighlight != null)
            {
                if (pillGold != null && pillDark != null)
                {
                    _tabAdvancedHighlight.sprite = !isBasic ? pillGold : pillDark;
                    _tabAdvancedHighlight.type = Image.Type.Sliced;
                    _tabAdvancedHighlight.color = Color.white;
                }
                else
                {
                    _tabAdvancedHighlight.color = !isBasic ? _activeTabColor : _inactiveTabColor;
                }
            }

            // Text colors
            var txtBasic = _btnTabBasic != null ? _btnTabBasic.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (txtBasic != null) txtBasic.color = isBasic ? new Color32(0x1A, 0x1A, 0x1A, 0xFF) : new Color32(0xBF, 0xE3, 0xE8, 0xFF);

            var txtAdv = _btnTabAdvanced != null ? _btnTabAdvanced.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (txtAdv != null) txtAdv.color = !isBasic ? new Color32(0x1A, 0x1A, 0x1A, 0xFF) : new Color32(0xBF, 0xE3, 0xE8, 0xFF);

            // Icon colors: dark on gold tab, light/blue on dark tab
            var iconBasic = _btnTabBasic != null ? _btnTabBasic.transform.Find("TabIcon")?.GetComponent<Image>() : null;
            if (iconBasic != null) iconBasic.color = isBasic ? new Color32(0x1A, 0x1A, 0x1A, 0xFF) : new Color32(0xBF, 0xE3, 0xE8, 0xFF);

            var iconAdv = _btnTabAdvanced != null ? _btnTabAdvanced.transform.Find("TabIcon")?.GetComponent<Image>() : null;
            if (iconAdv != null) iconAdv.color = !isBasic ? new Color32(0x1A, 0x1A, 0x1A, 0xFF) : new Color32(0x40, 0xAD, 0xFF, 0xFF);
        }

        public void PopulateUI(PreGameConfigData data)
        {
            BindConfigData(data);
        }

        public void BindConfigData(PreGameConfigData data)
        {
            if (data == null) return;
            EnsureSubViews();

            _generalSettingsView?.BindConfig(data);
            _giftsTableView?.RebuildGiftsList(data.gifts);
            _backendSettingsView?.BindConfig(data);

            _liveVerifier?.SetLiveVerifiedUI(false, "NOT VERIFIED");

            SwitchTab(true);
        }

        public void ReadUIIntoData()
        {
            if (PreGameConfigManager.Instance == null || PreGameConfigManager.Instance.CurrentConfig == null) return;
            var data = PreGameConfigManager.Instance.CurrentConfig;

            _generalSettingsView?.ReadConfig(data);
            _giftsTableView?.SaveCurrentValues();
            _backendSettingsView?.ReadConfig(data);
        }

        #region Top-Level Action Handlers

        public void OnResetDefaultsClicked()
        {
            PreGameConfigManager.Instance?.ResetToDefaults();
        }

        public void OnSaveConfigClicked()
        {
            ReadUIIntoData();
            if (PreGameConfigManager.Instance != null)
            {
                PreGameConfigManager.Instance.SaveConfig();
                PreGameConfigManager.Instance.ApplyConfigToRuntime(connectTikTok: false);
            }

            StartCoroutine(ShowSavedFeedback());
        }

        private IEnumerator ShowSavedFeedback()
        {
            var txt = _btnSaveConfig != null ? _btnSaveConfig.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (txt != null)
            {
                string origText = txt.text;
                var loc = LocalizationManager.Instance;
                txt.text = loc != null ? loc.Get("FEEDBACK_SAVED", "SAVED!") : "SAVED!";
                yield return new WaitForSecondsRealtime(1.2f);
                if (txt != null) txt.text = origText;
            }
        }

        public void OnTestModeClicked()
        {
            ReadUIIntoData();
            PreGameConfigManager.Instance?.StartTestMode();
        }

        public void OnGoLiveClicked()
        {
            if (_liveVerifier != null && !_liveVerifier.IsLiveVerified)
            {
                _liveVerifier.SetLiveVerifiedUI(false, "VERIFY FIRST");
                return;
            }

            ReadUIIntoData();
            PreGameConfigManager.Instance?.StartGoLive();
        }

        public void OnCloseClicked()
        {
            if (PreGameConfigManager.Instance != null)
            {
                // Discard uncommitted UI modifications and reload saved config
                PreGameConfigManager.Instance.LoadConfig();
                PreGameConfigManager.Instance.CloseConfigUI();
            }
        }

        #endregion

        #region Backward-Compatible Forwarders

        public void OnCheckLiveClicked()
        {
            ReadUIIntoData();
            var data = PreGameConfigManager.Instance?.CurrentConfig;
            string username = data != null ? data.tiktokUsername : "";
            int socketPort = data != null && data.backendSocketPort > 0 ? data.backendSocketPort : 3001;
            _liveVerifier?.VerifyLive(username, socketPort);
        }

        public void SetLiveVerifiedUI(bool isVerified, string statusText)
        {
            _liveVerifier?.SetLiveVerifiedUI(isVerified, statusText);
        }

        public void OnBrowseBackendDirClicked()
        {
            _backendSettingsView?.BrowseBackendDirectory(selectedPath =>
            {
                if (PreGameConfigManager.Instance?.CurrentConfig != null)
                {
                    PreGameConfigManager.Instance.CurrentConfig.backendDirectory = selectedPath;
                }
            });
        }

        public void OnCheckPortClicked()
        {
            ReadUIIntoData();
            var data = PreGameConfigManager.Instance?.CurrentConfig;
            int httpPort = data != null && data.backendPort > 0 ? data.backendPort : 9091;
            int socketPort = data != null && data.backendSocketPort > 0 ? data.backendSocketPort : 3001;
            _backendSettingsView?.CheckPorts(httpPort, socketPort);
        }

        public void OnStartBackendClicked()
        {
            ReadUIIntoData();

            var data = PreGameConfigManager.Instance?.CurrentConfig;
            string dir = data != null && !string.IsNullOrEmpty(data.backendDirectory) ? data.backendDirectory : SteamRush.Features.Backend.TikTokBackendManager.DefaultLocalPath;
            int httpPort = data != null && data.backendPort > 0 ? data.backendPort : 9091;
            int socketPort = data != null && data.backendSocketPort > 0 ? data.backendSocketPort : 3001;
            string apiKey = data != null && !string.IsNullOrEmpty(data.eulerApiKey) ? data.eulerApiKey : SteamRush.Features.Backend.TikTokBackendManager.DefaultEulerApiKey;

            _backendSettingsView?.StartBackend(dir, httpPort, socketPort, apiKey);
        }

        public void OnSetupBackendClicked()
        {
            ReadUIIntoData();
            var data = PreGameConfigManager.Instance?.CurrentConfig;
            string dir = data != null && !string.IsNullOrEmpty(data.backendDirectory) ? data.backendDirectory : SteamRush.Features.Backend.TikTokBackendManager.DefaultLocalPath;
            int httpPort = data != null && data.backendPort > 0 ? data.backendPort : 9091;
            int socketPort = data != null && data.backendSocketPort > 0 ? data.backendSocketPort : 3001;
            string apiKey = data != null && !string.IsNullOrEmpty(data.eulerApiKey) ? data.eulerApiKey : SteamRush.Features.Backend.TikTokBackendManager.DefaultEulerApiKey;

            _backendSettingsView?.SetupBackend(dir, httpPort, socketPort, apiKey);
        }

        public void OnStopBackendClicked()
        {
            ReadUIIntoData();
            var data = PreGameConfigManager.Instance?.CurrentConfig;
            int httpPort = data != null && data.backendPort > 0 ? data.backendPort : 9091;
            int socketPort = data != null && data.backendSocketPort > 0 ? data.backendSocketPort : 3001;

            _backendSettingsView?.StopBackend(httpPort, socketPort);
        }

        #endregion

        #region Localization

        private void OnLanguageToggleClicked()
        {
            try { AudioManager.Instance?.PlayUISFX(SFXType.UIButtonClick); } catch {}
            LocalizationManager.Instance?.ToggleLanguage();
        }

        public void OpenSetupGuide()
        {
            try { AudioManager.Instance?.PlayUISFX(SFXType.UIButtonClick); } catch {}
            if (_setupGuideModal == null)
            {
                var gm = transform.Find("SetupGuideModal");
                if (gm != null) _setupGuideModal = gm.gameObject;
            }
            if (_setupGuideModal != null)
            {
                _setupGuideModal.SetActive(true);
                _setupGuideModal.transform.SetAsLastSibling();
                UpdateSetupGuideTexts(LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLanguage : AppLanguage.English);
            }
        }

        public void CloseSetupGuide()
        {
            try { AudioManager.Instance?.PlayUISFX(SFXType.UIButtonClick); } catch {}
            if (_setupGuideModal == null)
            {
                var gm = transform.Find("SetupGuideModal");
                if (gm != null) _setupGuideModal = gm.gameObject;
            }
            if (_setupGuideModal != null)
            {
                _setupGuideModal.SetActive(false);
            }
        }

        public void UpdateSetupGuideTexts(AppLanguage lang)
        {
            if (_setupGuideModal == null)
            {
                var gm = transform.Find("SetupGuideModal");
                if (gm != null) _setupGuideModal = gm.gameObject;
            }
            if (_setupGuideModal == null) return;
            var loc = LocalizationManager.Instance;
            if (loc == null) return;

            UpdateTextChild(_setupGuideModal.transform, "Card/Header/TitleText", loc.Get("GUIDE_TITLE"));
            UpdateTextChild(_setupGuideModal.transform, "Card/Header/SubtitleText", loc.Get("GUIDE_SUBTITLE"));

            UpdateTextChild(_setupGuideModal.transform, "Card/Content/Step1/Badge/Text", loc.Get("GUIDE_STEP1_TAG"));
            UpdateTextChild(_setupGuideModal.transform, "Card/Content/Step1/Title", loc.Get("GUIDE_STEP1_TITLE"));
            UpdateTextChild(_setupGuideModal.transform, "Card/Content/Step1/Desc", loc.Get("GUIDE_STEP1_DESC"));

            UpdateTextChild(_setupGuideModal.transform, "Card/Content/Step2/Badge/Text", loc.Get("GUIDE_STEP2_TAG"));
            UpdateTextChild(_setupGuideModal.transform, "Card/Content/Step2/Title", loc.Get("GUIDE_STEP2_TITLE"));
            UpdateTextChild(_setupGuideModal.transform, "Card/Content/Step2/Desc", loc.Get("GUIDE_STEP2_DESC"));

            UpdateTextChild(_setupGuideModal.transform, "Card/Content/Step3/Badge/Text", loc.Get("GUIDE_STEP3_TAG"));
            UpdateTextChild(_setupGuideModal.transform, "Card/Content/Step3/Title", loc.Get("GUIDE_STEP3_TITLE"));
            UpdateTextChild(_setupGuideModal.transform, "Card/Content/Step3/Desc", loc.Get("GUIDE_STEP3_DESC"));

            UpdateTextChild(_setupGuideModal.transform, "Card/BtnGotIt/Text", loc.Get("GUIDE_BTN_GOTIT"));
        }

        public void UpdateAllLocalizedTexts(AppLanguage lang)
        {
            var loc = LocalizationManager.Instance;
            if (loc == null) return;

            // 1. Language Toggle Button Label
            if (_txtLanguageLabel == null && _btnLanguageToggle != null)
            {
                _txtLanguageLabel = _btnLanguageToggle.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (_txtLanguageLabel != null)
            {
                _txtLanguageLabel.text = lang == AppLanguage.Vietnamese ? "VI" : "EN";
            }

            // 2. Header Title & Subtitle
            var header = transform.Find("Window/Header");
            if (header != null)
            {
                var txtTitle = header.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
                if (txtTitle != null) txtTitle.text = loc.Get("HEADER_TITLE", txtTitle.text);

                var txtSub = header.Find("SubtitleText")?.GetComponent<TextMeshProUGUI>();
                if (txtSub != null) txtSub.text = loc.Get("HEADER_SUBTITLE", txtSub.text);
            }

            // 3. Tab Buttons
            if (_btnTabBasic != null)
            {
                var t = _btnTabBasic.GetComponentInChildren<TextMeshProUGUI>();
                if (t != null) t.text = loc.Get("TAB_BASIC", t.text);
            }
            if (_btnTabAdvanced != null)
            {
                var t = _btnTabAdvanced.GetComponentInChildren<TextMeshProUGUI>();
                if (t != null) t.text = loc.Get("TAB_ADVANCED", t.text);
            }

            // 4. Basic Panel Banners & Labels
            if (_panelBasicContent != null)
            {
                UpdateTextChild(_panelBasicContent.transform, "Banner_TIKTOK LIVE HOST CHANNEL/Text", loc.Get("BANNER_HOST_CHANNEL"));
                UpdateTextChild(_panelBasicContent.transform, "UserRow/InputUsername/Text Area/Placeholder", loc.Get("PLACEHOLDER_USERNAME"));
                UpdateTextChild(_panelBasicContent.transform, "UserRow/BtnCheckLive/Text", loc.Get("BTN_CHECK_LIVE"));

                UpdateTextChild(_panelBasicContent.transform, "Banner_TARGET DISTANCE & BATON RELAY/Text", loc.Get("BANNER_TARGET_DISTANCE"));
                UpdateTextChild(_panelBasicContent.transform, "DistanceRow/InputTargetDistance/Text Area/Placeholder", loc.Get("PLACEHOLDER_DISTANCE"));
                UpdateTextChild(_panelBasicContent.transform, "DistanceRow/Row_Infinite Run:/Label", loc.Get("LABEL_INFINITE_RUN"));

                UpdateTextChild(_panelBasicContent.transform, "Banner_STREAM GIFTS & INTERACTION TRIGGERS/Text", loc.Get("BANNER_STREAM_GIFTS"));

                // Table Headers
                var th = _panelBasicContent.transform.Find("TableHeader");
                if (th != null)
                {
                    UpdateTextChild(th, "ThActive", loc.Get("TH_ACTIVE"));
                    UpdateTextChild(th, "ThFeature", loc.Get("TH_FEATURE"));
                    UpdateTextChild(th, "ThGift", loc.Get("TH_GIFT"));
                    UpdateTextChild(th, "ThStat", loc.Get("TH_STAT"));
                    UpdateTextChild(th, "ThDesc", loc.Get("TH_DESC"));
                    UpdateTextChild(th, "ThOrder", loc.Get("TH_ORDER"));
                }
            }

            // 5. Advanced Panel Banners & Labels
            if (_panelAdvancedContent != null)
            {
                var adv = _panelAdvancedContent.transform;
                UpdateTextChild(adv, "Viewport/Content/Banner_FAN FACTION & RUNNER ENERGY/Text", loc.Get("BANNER_FAN_ENERGY"));
                UpdateTextChild(adv, "Viewport/Content/Banner_ANTI FACTION & TRAFFIC HAZARDS/Text", loc.Get("BANNER_ANTI_HAZARDS"));
                UpdateTextChild(adv, "Viewport/Content/Banner_COLLISION PENALTIES/Text", loc.Get("BANNER_PENALTIES"));
                UpdateTextChild(adv, "Viewport/Content/Banner_OFFLINE LIVE STREAM SIMULATION/Text", loc.Get("BANNER_SIMULATION"));
                UpdateTextChild(adv, "Viewport/Content/Banner_HUD & OVERLAY DISPLAY/Text", loc.Get("BANNER_HUD_DISPLAY"));
                UpdateTextChild(adv, "Viewport/Content/Banner_TIKTOK LIVE BACKEND & CONNECTION/Text", loc.Get("BANNER_BACKEND"));

                // Labels
                UpdateSettingLabel(adv, "Row_Lane change energy cost", loc.Get("LBL_LANE_COST"));
                UpdateSettingLabel(adv, "Row_Jump energy cost", loc.Get("LBL_JUMP_COST"));
                UpdateSettingLabel(adv, "Row_Freedom Charm duration", loc.Get("LBL_FREE_CONTROL_DUR"));
                UpdateSettingLabel(adv, "Row_Sprint Boost duration", loc.Get("LBL_SPRINT_DUR"));
                UpdateSettingLabel(adv, "Row_Enable manual keyboard controls", loc.Get("LBL_KEYBOARD_CTRL"));
                UpdateSettingLabel(adv, "Row_Auto-spawn car", loc.Get("LBL_AUTO_SPAWN"));
                UpdateSettingLabel(adv, "Row_Max concurrent cars", loc.Get("LBL_MAX_CARS"));
                UpdateSettingLabel(adv, "Row_Distance penalty", loc.Get("LBL_DISTANCE_PENALTY"));
                UpdateSettingLabel(adv, "Row_Energy lost on Sedan", loc.Get("LBL_SEDAN_PENALTY"));
                UpdateSettingLabel(adv, "Row_Energy lost on Hunting Beast", loc.Get("LBL_PICKUP_PENALTY"));
                UpdateSettingLabel(adv, "Row_Energy lost on Train", loc.Get("LBL_HEAVY_PENALTY"));
                UpdateSettingLabel(adv, "Row_Auto live simulation", loc.Get("LBL_SIM_MASTER"));
                UpdateSettingLabel(adv, "Row_Simulate chat comments", loc.Get("LBL_SIM_CHATS"));
                UpdateSettingLabel(adv, "Row_Simulate viewer gifts", loc.Get("LBL_SIM_GIFTS"));
                UpdateSettingLabel(adv, "Row_Simulate continuous hearts", loc.Get("LBL_SIM_LIKES"));
                UpdateSettingLabel(adv, "Row_Simulate baton handover", loc.Get("LBL_SIM_FOLLOWS"));
                UpdateSettingLabel(adv, "Row_Simulate stream broadcast delay", loc.Get("LBL_SIM_DELAY"));
                UpdateSettingLabel(adv, "Row_Show How-To-Play Guide", loc.Get("LBL_HUD_HOWTOPLAY"));
                UpdateSettingLabel(adv, "Row_Show Gift Info Panel", loc.Get("LBL_HUD_GIFTINFO"));
                UpdateSettingLabel(adv, "Row_Show Match Timer", loc.Get("LBL_HUD_TIMER"));
                UpdateSettingLabel(adv, "Row_Show Skill & Buff Timer Circles", loc.Get("LBL_HUD_CIRCLES"));
                UpdateSettingLabel(adv, "Row_Show Debug UI", loc.Get("LBL_HUD_DEBUGUI"));
                UpdateSettingLabel(adv, "Row_Backend HTTP Port", loc.Get("LBL_BACKEND_HTTP"));
                UpdateSettingLabel(adv, "Row_Socket.IO Port", loc.Get("LBL_BACKEND_SOCKET"));
                UpdateSettingLabel(adv, "Row_Euler API Key", loc.Get("LBL_EULER_KEY"));
                UpdateSettingLabel(adv, "Row_Backend Folder Path", loc.Get("LBL_BACKEND_DIR"));

                // Buttons in backend
                UpdateTextChild(adv, "Viewport/Content/Row_Euler API Key (EULER_API_KEY):/BtnOpenWeb/Text", loc.Get("BTN_GET_KEY"));
                UpdateTextChild(adv, "Viewport/Content/Row_BackendActionButtons/BtnCheckPort/Text", loc.Get("BTN_CHECK_PORTS"));
                UpdateTextChild(adv, "Viewport/Content/Row_BackendActionButtons/BtnStartBackend/Text", loc.Get("BTN_START_BACKEND"));
                UpdateTextChild(adv, "Viewport/Content/Row_BackendActionButtons/BtnSetupBackend/Text", loc.Get("BTN_SETUP_BACKEND"));
                UpdateTextChild(adv, "Viewport/Content/Row_BackendActionButtons/BtnStopBackend/Text", loc.Get("BTN_STOP_BACKEND"));

                // Reset buttons in rows
                foreach (var btn in adv.GetComponentsInChildren<Button>(true))
                {
                    if (btn.name == "BtnReset" || btn.name == "BtnResetDir")
                    {
                        var t = btn.GetComponentInChildren<TextMeshProUGUI>();
                        if (t != null)
                        {
                            t.text = loc.Get("BTN_RESET", "RESET");
                            t.enableAutoSizing = true;
                            t.fontSizeMin = 9f;
                            t.fontSizeMax = 13.5f;
                        }
                    }
                }
            }

            // 6. Bottom Bar Buttons
            if (_btnResetDefaults != null)
            {
                var t = _btnResetDefaults.GetComponentInChildren<TextMeshProUGUI>();
                if (t != null) t.text = loc.Get("BTN_RESET_DEFAULTS", t.text);
            }
            if (_btnSaveConfig != null)
            {
                var t = _btnSaveConfig.GetComponentInChildren<TextMeshProUGUI>();
                if (t != null) t.text = loc.Get("BTN_SAVE_CONFIG", t.text);
            }
            if (_btnTestMode != null)
            {
                var t = _btnTestMode.GetComponentInChildren<TextMeshProUGUI>();
                if (t != null) t.text = loc.Get("BTN_TEST_MODE", t.text);
            }
            var btnGoLive = transform.Find("Window/BottomBar/BtnGoLive");
            if (btnGoLive != null)
            {
                var t = btnGoLive.GetComponentInChildren<TextMeshProUGUI>();
                if (t != null) t.text = loc.Get("BTN_START_STREAM", t.text);
            }

            // 7. Refresh Gifts Table rows with localized defaults
            var cfg = PreGameConfigManager.Instance?.CurrentConfig;
            if (cfg != null && _giftsTableView != null)
            {
                _giftsTableView.RebuildGiftsList(cfg.gifts);
            }

            // 8. Update Setup Guide Modal texts
            UpdateSetupGuideTexts(lang);
        }

        private static void UpdateTextChild(Transform root, string path, string newText)
        {
            if (root == null || string.IsNullOrEmpty(newText)) return;
            var t = root.Find(path);
            if (t != null)
            {
                var tmp = t.GetComponent<TextMeshProUGUI>();
                if (tmp != null) tmp.text = newText;
            }
            else
            {
                // Fallback: search by name
                string childName = path.Contains("/") ? path.Substring(path.LastIndexOf('/') + 1) : path;
                foreach (var c in root.GetComponentsInChildren<Transform>(true))
                {
                    if (c.name == childName)
                    {
                        var tmp = c.GetComponent<TextMeshProUGUI>();
                        if (tmp != null) { tmp.text = newText; break; }
                    }
                }
            }
        }

        private static void UpdateSettingLabel(Transform root, string prefix, string newText)
        {
            if (root == null || string.IsNullOrEmpty(newText)) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith(prefix))
                {
                    var label = t.Find("Label")?.GetComponent<TextMeshProUGUI>();
                    if (label != null) label.text = newText;
                    return;
                }
            }
        }

        #endregion
    }
}

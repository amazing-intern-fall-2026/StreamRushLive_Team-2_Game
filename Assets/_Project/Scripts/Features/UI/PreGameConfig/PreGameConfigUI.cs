using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SteamRush.Features.UI;

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

        private readonly Color _activeTabColor = new Color32(0xFC, 0xDA, 0x21, 0xFF);   // Gold
        private readonly Color _inactiveTabColor = new Color32(0x20, 0x61, 0x72, 0xFF); // Dark Teal

        private void Awake()
        {
            EnsureSubViews();
            SetupButtonListeners();
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

            bool hasSavedUser = !string.IsNullOrEmpty(data.tiktokUsername);
            string initialStatus = hasSavedUser
                ? "SAVED (@" + data.tiktokUsername + ")"
                : "NOT VERIFIED";
            _liveVerifier?.SetLiveVerifiedUI(hasSavedUser, initialStatus);

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
                txt.text = "SAVED!";
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
            ReadUIIntoData();
            if (PreGameConfigManager.Instance != null)
            {
                PreGameConfigManager.Instance.SaveConfig();
                PreGameConfigManager.Instance.ApplyConfigToRuntime(connectTikTok: false);
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
                    PreGameConfigManager.Instance.SaveConfig();
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
            PreGameConfigManager.Instance?.SaveConfig();

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
    }
}

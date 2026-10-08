using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SteamRush.Core;
using SteamRush.Features.StreamIntegration;
using SteamRush.Features.Backend;

namespace SteamRush.Features.UI.PreGameConfig
{
    public class PreGameConfigUI : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private Button _btnTabBasic;
        [SerializeField] private Button _btnTabAdvanced;
        [SerializeField] private GameObject _panelBasicContent;
        [SerializeField] private GameObject _panelAdvancedContent;
        [SerializeField] private Image _tabBasicHighlight;
        [SerializeField] private Image _tabAdvancedHighlight;

        [Header("Basic Inputs")]
        [SerializeField] private TMP_InputField _inputUsername;
        [SerializeField] private Button _btnCheckLive;
        [SerializeField] private TextMeshProUGUI _txtLiveStatus;
        [SerializeField] private TMP_InputField _inputTargetDistance;
        [SerializeField] private Toggle _toggleInfiniteDistance;
        [SerializeField] private Transform _giftsContainer;
        [SerializeField] private GameObject _giftRowTemplate;

        private bool _isLiveVerified = false;
        private string _verifiedUsername = "";
        private Coroutine _checkLiveCoroutine;
        private float _lastFiniteDistanceKm = 1f;

        [Header("Advanced Inputs")]
        [SerializeField] private TMP_InputField _inputLaneCost;
        [SerializeField] private TMP_InputField _inputJumpCost;
        [SerializeField] private TMP_InputField _inputFreeControlDuration;
        [SerializeField] private TMP_InputField _inputSprintDuration;
        [SerializeField] private Toggle _toggleAutoSpawnCar;
        [SerializeField] private TMP_InputField _inputMaxCars;
        [SerializeField] private TMP_InputField _inputDistancePenalty;
        [SerializeField] private TMP_InputField _inputSedanEnergyPenalty;
        [SerializeField] private TMP_InputField _inputPickupEnergyPenalty;
        [SerializeField] private TMP_InputField _inputHeavyEnergyPenalty;

        [Header("Live Demo Simulation Toggles")]
        [SerializeField] private Toggle _toggleLiveDemoMaster;
        [SerializeField] private Toggle _toggleSimulatedChats;
        [SerializeField] private Toggle _toggleSimulatedGifts;
        [SerializeField] private Toggle _toggleSimulatedLikes;
        [SerializeField] private Toggle _toggleSimulatedFollowers;
        [SerializeField] private Toggle _toggleSimulatedDelay;
        [SerializeField] private Toggle _toggleShowHowToPlayGuide;
        [SerializeField] private Toggle _toggleShowGiftInfoPanel;
        [SerializeField] private Toggle _toggleShowStopwatch;
        [SerializeField] private Toggle _toggleShowTimerCircles;
        [SerializeField] private Toggle _toggleDebugUI;

        [Header("TikTok Live Backend Inputs & Buttons")]
        [SerializeField] private TMP_InputField _inputBackendPort;
        [SerializeField] private TMP_InputField _inputBackendSocketPort;
        [SerializeField] private TMP_InputField _inputEulerApiKey;
        [SerializeField] private TMP_InputField _inputBackendDir;
        [SerializeField] private TextMeshProUGUI _txtBackendStatus;
        [SerializeField] private Button _btnBrowseBackendDir;
        [SerializeField] private Button _btnCheckPort;
        [SerializeField] private Button _btnStartBackend;
        [SerializeField] private Button _btnSetupBackend;
        [SerializeField] private Button _btnStopBackend;
        [SerializeField] private Button _btnGetEulerKey;

        [Header("Action Buttons")]
        [SerializeField] private Button _btnResetDefaults;
        [SerializeField] private Button _btnSaveConfig;
        [SerializeField] private Button _btnTestMode;
        [SerializeField] private Button _btnGoLive;
        [SerializeField] private Button _btnClose;

        private readonly Color _activeTabColor = new Color32(0xFC, 0xDA, 0x21, 0xFF);   // Gold
        private readonly Color _inactiveTabColor = new Color32(0x20, 0x61, 0x72, 0xFF); // Dark Teal
        private readonly Color _activeDistBtnColor = new Color32(0x40, 0xAD, 0xFF, 0xFF);
        private readonly Color _inactiveDistBtnColor = new Color32(0x18, 0x36, 0x48, 0xFF);

        private void Awake()
        {
            SetupButtonListeners();
        }

        private void SetupButtonListeners()
        {
            if (_btnTabBasic != null) _btnTabBasic.onClick.AddListener(() => SwitchTab(true));
            if (_btnTabAdvanced != null) _btnTabAdvanced.onClick.AddListener(() => SwitchTab(false));

            if (_toggleInfiniteDistance != null)
            {
                _toggleInfiniteDistance.onValueChanged.AddListener(OnInfiniteToggleChanged);
            }

            if (_btnResetDefaults != null) _btnResetDefaults.onClick.AddListener(OnResetDefaultsClicked);
            if (_btnSaveConfig != null) _btnSaveConfig.onClick.AddListener(OnSaveConfigClicked);
            if (_btnTestMode != null) _btnTestMode.onClick.AddListener(OnTestModeClicked);
            if (_btnGoLive != null) _btnGoLive.onClick.AddListener(OnGoLiveClicked);
            if (_btnClose != null) _btnClose.onClick.AddListener(OnCloseClicked);

            if (_btnCheckLive != null)
            {
                _btnCheckLive.onClick.RemoveAllListeners();
                _btnCheckLive.onClick.AddListener(OnCheckLiveClicked);
            }

            if (_inputUsername != null)
            {
                _inputUsername.onValueChanged.RemoveAllListeners();
                _inputUsername.onValueChanged.AddListener(newVal =>
                {
                    string clean = newVal?.Trim().TrimStart('@') ?? "";
                    if (_isLiveVerified && !string.Equals(clean, _verifiedUsername, StringComparison.OrdinalIgnoreCase))
                    {
                        _isLiveVerified = false;
                        _verifiedUsername = "";
                        SetLiveVerifiedUI(false, "NOT VERIFIED");
                    }
                });
            }

            if (_toggleShowHowToPlayGuide != null)
            {
                _toggleShowHowToPlayGuide.onValueChanged.AddListener(val =>
                {
                    var canvas = GetComponentInParent<Canvas>();
                    var guide = canvas != null ? canvas.transform.Find("HowToPlayGuide") : null;
                    if (guide != null) guide.gameObject.SetActive(val);
                });
            }

            if (_toggleShowGiftInfoPanel != null)
            {
                _toggleShowGiftInfoPanel.onValueChanged.AddListener(val =>
                {
                    var canvas = GetComponentInParent<Canvas>();
                    var panel = canvas != null ? canvas.transform.Find("GiftInfoPanel") : null;
                    if (panel != null) panel.gameObject.SetActive(val);
                });
            }

            if (_toggleShowStopwatch != null)
            {
                _toggleShowStopwatch.onValueChanged.AddListener(val =>
                {
                    var canvas = GetComponentInParent<Canvas>();
                    var stopwatch = canvas != null ? canvas.transform.Find("ElapsedTimeStopwatch") : null;
                    if (stopwatch != null)
                    {
                        var swComp = stopwatch.GetComponent<Views.ElapsedTimeStopwatch>();
                        if (swComp != null) swComp.SetDisplayVisible(val);
                        else stopwatch.gameObject.SetActive(val);
                    }
                });
            }

            if (_toggleShowTimerCircles != null)
            {
                _toggleShowTimerCircles.onValueChanged.AddListener(val =>
                {
                    var stackMgr = Views.TimerCircleVerticalStackManager.Instance ?? FindFirstObjectByType<Views.TimerCircleVerticalStackManager>();
                    if (stackMgr != null)
                    {
                        var cg = stackMgr.GetComponent<CanvasGroup>();
                        if (cg == null) cg = stackMgr.gameObject.AddComponent<CanvasGroup>();
                        cg.alpha = val ? 1f : 0f;
                        cg.interactable = val;
                        cg.blocksRaycasts = val;
                    }
                });
            }

            if (_btnGetEulerKey != null)
            {
                _btnGetEulerKey.onClick.RemoveListener(OnGetEulerKeyClicked);
                _btnGetEulerKey.onClick.AddListener(OnGetEulerKeyClicked);
            }

            if (_btnBrowseBackendDir != null) _btnBrowseBackendDir.onClick.AddListener(OnBrowseBackendDirClicked);
            if (_btnCheckPort != null) _btnCheckPort.onClick.AddListener(OnCheckPortClicked);
            if (_btnStartBackend != null) _btnStartBackend.onClick.AddListener(OnStartBackendClicked);
            if (_btnSetupBackend != null) _btnSetupBackend.onClick.AddListener(OnSetupBackendClicked);
            if (_btnStopBackend != null) _btnStopBackend.onClick.AddListener(OnStopBackendClicked);
        }

        private void OnGetEulerKeyClicked()
        {
            Debug.Log("[PreGameConfigUI] Opening EulerStream API Key website: https://www.eulerstream.com/");
            Application.OpenURL("https://www.eulerstream.com/");
        }

        private void OnInfiniteToggleChanged(bool isInfinite)
        {
            if (_inputTargetDistance != null)
            {
                _inputTargetDistance.interactable = !isInfinite;
                if (isInfinite)
                {
                    // Cache the current finite distance before switching to Infinite
                    string rawText = _inputTargetDistance.text.Replace(',', '.').Trim();
                    if (!string.Equals(rawText, "Infinite", StringComparison.OrdinalIgnoreCase) &&
                        float.TryParse(rawText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float kmVal) &&
                        kmVal > 0f && kmVal < 900000f)
                    {
                        _lastFiniteDistanceKm = kmVal;
                        if (PreGameConfigManager.Instance?.CurrentConfig != null)
                        {
                            PreGameConfigManager.Instance.CurrentConfig.finiteTargetDistanceMeters = kmVal * 1000f;
                        }
                    }
                    else if (PreGameConfigManager.Instance?.CurrentConfig != null)
                    {
                        float savedMeters = PreGameConfigManager.Instance.CurrentConfig.finiteTargetDistanceMeters;
                        if (savedMeters > 0f && savedMeters < 900000000f)
                        {
                            _lastFiniteDistanceKm = savedMeters / 1000f;
                        }
                    }

                    _inputTargetDistance.text = "Infinite";
                }
                else
                {
                    // Restoring from Infinite back to finite distance
                    float km = _lastFiniteDistanceKm;
                    if (km <= 0f || km >= 900000f)
                    {
                        if (PreGameConfigManager.Instance?.CurrentConfig != null)
                        {
                            float m = PreGameConfigManager.Instance.CurrentConfig.finiteTargetDistanceMeters;
                            if (m > 0f && m < 900000000f) km = m / 1000f;
                            else km = 1f;
                        }
                        else
                        {
                            km = 1f;
                        }
                    }

                    _lastFiniteDistanceKm = km;
                    _inputTargetDistance.text = km.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }

        public void SwitchTab(bool isBasic)
        {
            if (_panelBasicContent != null) _panelBasicContent.SetActive(isBasic);
            if (_panelAdvancedContent != null) _panelAdvancedContent.SetActive(!isBasic);

            var theme = HudTheme.Current;
            if (_tabBasicHighlight != null)
            {
                if (theme != null && theme.pillGold != null && theme.pillDark != null)
                {
                    _tabBasicHighlight.sprite = isBasic ? theme.pillGold : theme.pillDark;
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
                if (theme != null && theme.pillGold != null && theme.pillDark != null)
                {
                    _tabAdvancedHighlight.sprite = !isBasic ? theme.pillGold : theme.pillDark;
                    _tabAdvancedHighlight.type = Image.Type.Sliced;
                    _tabAdvancedHighlight.color = Color.white;
                }
                else
                {
                    _tabAdvancedHighlight.color = !isBasic ? _activeTabColor : _inactiveTabColor;
                }
            }

            var txtBasic = _btnTabBasic != null ? _btnTabBasic.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (txtBasic != null) txtBasic.color = isBasic ? new Color32(0x1A, 0x1A, 0x1A, 0xFF) : new Color32(0xBF, 0xE3, 0xE8, 0xFF);

            var txtAdv = _btnTabAdvanced != null ? _btnTabAdvanced.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (txtAdv != null) txtAdv.color = !isBasic ? new Color32(0x1A, 0x1A, 0x1A, 0xFF) : new Color32(0xBF, 0xE3, 0xE8, 0xFF);
        }

        public void PopulateUI(PreGameConfigData data)
        {
            if (data == null) return;

            // Basic Tab
            if (_inputUsername != null) _inputUsername.text = data.tiktokUsername;

            // Sanitize finite target distance if invalid or stuck from legacy values
            if (data.finiteTargetDistanceMeters <= 0f || data.finiteTargetDistanceMeters >= 900000000f)
            {
                if (data.targetDistanceMeters > 0f && data.targetDistanceMeters < 900000000f && data.targetDistanceMeters != 1000000f)
                {
                    data.finiteTargetDistanceMeters = data.targetDistanceMeters;
                }
                else
                {
                    data.finiteTargetDistanceMeters = 1000f;
                }
            }

            _lastFiniteDistanceKm = data.finiteTargetDistanceMeters / 1000f;

            if (_toggleInfiniteDistance != null) _toggleInfiniteDistance.isOn = data.isInfiniteDistance;
            if (_inputTargetDistance != null)
            {
                _inputTargetDistance.interactable = !data.isInfiniteDistance;
                if (data.isInfiniteDistance)
                {
                    _inputTargetDistance.text = "Infinite";
                }
                else
                {
                    // Clean legacy 1,000km artifact or infinite value
                    if (data.targetDistanceMeters >= 900000000f || data.targetDistanceMeters <= 0f || (data.targetDistanceMeters == 1000000f && data.finiteTargetDistanceMeters <= 10000f))
                    {
                        data.targetDistanceMeters = data.finiteTargetDistanceMeters;
                    }

                    float km = data.targetDistanceMeters / 1000f;
                    _lastFiniteDistanceKm = km;
                    _inputTargetDistance.text = km.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            // Rebuild Gifts list
            RebuildGiftsList(data.gifts);

            // Advanced Tab
            if (_inputLaneCost != null) _inputLaneCost.text = data.laneChangeEnergyCost.ToString();
            if (_inputJumpCost != null) _inputJumpCost.text = data.jumpEnergyCost.ToString();
            if (_inputFreeControlDuration != null) _inputFreeControlDuration.text = data.freeControlDuration.ToString("F0");
            if (_inputSprintDuration != null) _inputSprintDuration.text = data.sprintBuffDuration.ToString("F0");
            if (_toggleAutoSpawnCar != null) _toggleAutoSpawnCar.isOn = data.autoSpawnOnFullEnergy;
            if (_inputMaxCars != null) _inputMaxCars.text = data.maxConcurrentCars.ToString();
            if (_inputDistancePenalty != null) _inputDistancePenalty.text = data.distancePenaltyMeters.ToString("F0");
            if (_inputSedanEnergyPenalty != null) _inputSedanEnergyPenalty.text = data.sedanEnergyPenaltyPercent.ToString("F0");
            if (_inputPickupEnergyPenalty != null) _inputPickupEnergyPenalty.text = data.pickupEnergyPenaltyPercent.ToString("F0");
            if (_inputHeavyEnergyPenalty != null) _inputHeavyEnergyPenalty.text = data.heavyTruckEnergyPenaltyPercent.ToString("F0");

            // Live Demo Simulation Toggles
            if (_toggleLiveDemoMaster != null) _toggleLiveDemoMaster.isOn = data.enableLiveDemoSimulation;
            if (_toggleSimulatedChats != null) _toggleSimulatedChats.isOn = data.enableSimulatedChats;
            if (_toggleSimulatedGifts != null) _toggleSimulatedGifts.isOn = data.enableSimulatedGifts;
            if (_toggleSimulatedLikes != null) _toggleSimulatedLikes.isOn = data.enableSimulatedLikes;
            if (_toggleSimulatedFollowers != null) _toggleSimulatedFollowers.isOn = data.enableSimulatedFollowers;
            if (_toggleSimulatedDelay != null) _toggleSimulatedDelay.isOn = data.enableSimulatedStreamDelay;
            if (_toggleShowHowToPlayGuide != null) _toggleShowHowToPlayGuide.isOn = data.showHowToPlayGuide;
            if (_toggleShowGiftInfoPanel != null) _toggleShowGiftInfoPanel.isOn = data.showGiftInfoPanel;
            if (_toggleShowStopwatch != null) _toggleShowStopwatch.isOn = data.showStopwatchTimer;
            if (_toggleShowTimerCircles != null) _toggleShowTimerCircles.isOn = data.showTimerCircles;
            if (_toggleDebugUI != null) _toggleDebugUI.isOn = data.enableDebugUI;

            // TikTok Live Backend Fields
            if (_inputBackendPort != null) _inputBackendPort.text = (data.backendPort > 0 ? data.backendPort : 9091).ToString();
            if (_inputBackendSocketPort != null) _inputBackendSocketPort.text = (data.backendSocketPort > 0 ? data.backendSocketPort : 3001).ToString();
            if (_inputEulerApiKey != null) _inputEulerApiKey.text = !string.IsNullOrEmpty(data.eulerApiKey) ? data.eulerApiKey : TikTokBackendManager.DefaultEulerApiKey;
            if (_inputBackendDir != null) _inputBackendDir.text = !string.IsNullOrEmpty(data.backendDirectory) ? data.backendDirectory : TikTokBackendManager.DefaultLocalPath;
            if (_txtBackendStatus != null) _txtBackendStatus.text = "<color=#BFE3E8>Port Status: Not checked</color>";

            // Check Live Verification state: only require initial verification once; retain status when opening settings
            var liveClient = FindFirstObjectByType<TikTokLiveClient>();
            string currentInputUser = data.tiktokUsername?.Trim().TrimStart('@') ?? "";
            bool isClientLiveConnected = liveClient != null && (liveClient.IsTikTokLiveConnected || (liveClient.IsConnected && !string.IsNullOrEmpty(liveClient.CurrentRoomId)));
            bool isSameUser = !string.IsNullOrEmpty(currentInputUser) && (
                (liveClient != null && string.Equals(liveClient.TikTokUniqueId, currentInputUser, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(_verifiedUsername, currentInputUser, StringComparison.OrdinalIgnoreCase)
            );

            if (isClientLiveConnected && isSameUser)
            {
                _isLiveVerified = true;
                _verifiedUsername = currentInputUser;
                SetLiveVerifiedUI(true, "CONNECTED");
            }
            else if (_isLiveVerified && isSameUser)
            {
                // Already verified previously for this username - keep verified without requiring re-check
                SetLiveVerifiedUI(true, "VERIFIED");
            }
            else
            {
                _isLiveVerified = false;
                _verifiedUsername = "";
                SetLiveVerifiedUI(false, "NOT VERIFIED");
            }

            SwitchTab(true);
        }

        private void RebuildGiftsList(List<PreGameGiftItemConfig> gifts)
        {
            if (_giftsContainer == null) return;

            // Clear old rows
            for (int i = _giftsContainer.childCount - 1; i >= 0; i--)
            {
                var child = _giftsContainer.GetChild(i);
                if (_giftRowTemplate != null && child.gameObject == _giftRowTemplate) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(child.gameObject);
                else Destroy(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }

            if (gifts == null) return;

            // Ensure TableHeader has ThStat column
            var header = _giftsContainer?.parent?.parent?.Find("TableHeader") ?? _giftsContainer?.parent?.Find("TableHeader");
            if (header != null)
            {
                var thStat = header.Find("ThStat");
                if (thStat == null)
                {
                    var thActive = header.Find("ThActive")?.GetComponent<RectTransform>();
                    if (thActive != null) PreGameUIBuilder.SetRect(thActive, 8f, 0f, 60f, 28f);

                    var thFeat = header.Find("ThFeature")?.GetComponent<RectTransform>();
                    if (thFeat != null) PreGameUIBuilder.SetRect(thFeat, 74f, 0f, 142f, 28f);

                    var thGift = header.Find("ThGift")?.GetComponent<RectTransform>();
                    if (thGift != null) PreGameUIBuilder.SetRect(thGift, 222f, 0f, 145f, 28f);

                    var thDesc = header.Find("ThDesc")?.GetComponent<RectTransform>();
                    if (thDesc != null) PreGameUIBuilder.SetRect(thDesc, 450f, 0f, 194f, 28f);

                    var thOrder = header.Find("ThOrder")?.GetComponent<RectTransform>();
                    if (thOrder != null) PreGameUIBuilder.SetRect(thOrder, 648f, 0f, 58f, 28f);

                    var font = header.Find("ThActive")?.GetComponent<TextMeshProUGUI>()?.font;
                    var newStatObj = PreGameUIBuilder.CreateHeaderColumn(header, "ThStat", "STAT", font);
                    PreGameUIBuilder.SetRect(newStatObj.GetComponent<RectTransform>(), 372f, 0f, 72f, 28f);
                }
            }

            // Cache gift icons from TikTokGiftRouter and AvailableGifts
            var router = FindFirstObjectByType<TikTokGiftRouter>();
            var iconMap = new Dictionary<string, Sprite>();
            if (router != null && router.GiftMappings != null)
            {
                foreach (var m in router.GiftMappings)
                {
                    if (m.giftIcon != null && !iconMap.ContainsKey(m.giftName.ToLowerInvariant()))
                    {
                        iconMap[m.giftName.ToLowerInvariant()] = m.giftIcon;
                    }
                }
            }

            foreach (var opt in PreGameConfigData.AvailableGifts)
            {
                string key = opt.giftName.ToLowerInvariant();
                if (!iconMap.ContainsKey(key))
                {
                    Sprite s = PreGameUIBuilder.ResolveGiftIcon(opt.giftId, opt.giftName);
                    if (s != null) iconMap[key] = s;
                }
            }

            for (int i = 0; i < gifts.Count; i++)
            {
                int index = i;
                var item = gifts[i];
                GameObject rowObj;

                if (_giftRowTemplate != null)
                {
                    rowObj = Instantiate(_giftRowTemplate, _giftsContainer);
                    rowObj.SetActive(true);
                }
                else
                {
                    rowObj = CreateFallbackGiftRow(item, index);
                }

                BindGiftRow(rowObj, item, index, gifts, iconMap);
            }
        }

        private void BindGiftRow(GameObject rowObj, PreGameGiftItemConfig item, int index, List<PreGameGiftItemConfig> gifts, Dictionary<string, Sprite> iconMap)
        {
            // 2. Feature Name
            var featTxt = rowObj.transform.Find("FeatureNameText")?.GetComponent<TextMeshProUGUI>();
            if (featTxt != null)
            {
                string fName = string.IsNullOrEmpty(item.featureName) ? PreGameGiftItemConfig.GetDefaultFeatureName(item.action) : item.featureName;
                if (fName == "+300 Blue Energy" || fName == "300 Blue Energy" || (item.action == GiftActionType.Blue_EnergyBottle && fName.Contains("300")))
                {
                    fName = "Blue Energy";
                }
                else if (fName == "+Red Energy" || (item.action == GiftActionType.Red_EnergyBottle && fName.StartsWith("+")))
                {
                    fName = "Red Energy";
                }
                else if (fName.Equals("Meme Dance", StringComparison.OrdinalIgnoreCase) || (item.action == GiftActionType.Special_GiftDance && fName.Contains("Meme")))
                {
                    fName = "Dance";
                }
                item.featureName = fName;
                featTxt.text = fName;
            }

            // 3. Gift Image
            var iconImg = rowObj.transform.Find("Icon")?.GetComponent<Image>();

            void UpdateRowVisualState(bool enabled)
            {
                float alpha = enabled ? 1f : 0.45f;
                if (featTxt != null) featTxt.color = new Color(1f, 1f, 1f, alpha);
                if (iconImg != null && iconImg.sprite != null)
                {
                    iconImg.color = new Color(1f, 1f, 1f, alpha);
                }
                var statIn = rowObj.transform.Find("StatInput")?.GetComponent<TMP_InputField>();
                if (statIn != null)
                {
                    statIn.interactable = enabled;
                    var sImg = statIn.GetComponent<Image>();
                    if (sImg != null) sImg.color = new Color(0.08f, 0.20f, 0.28f, 0.95f * alpha);
                    if (statIn.textComponent != null) statIn.textComponent.color = new Color(1f, 1f, 1f, alpha);
                }
                var descIn = rowObj.transform.Find("DescInput")?.GetComponent<TMP_InputField>();
                if (descIn != null)
                {
                    descIn.interactable = enabled;
                    var dImg = descIn.GetComponent<Image>();
                    if (dImg != null) dImg.color = new Color(0.08f, 0.20f, 0.28f, 0.95f * alpha);
                    if (descIn.textComponent != null) descIn.textComponent.color = new Color(1f, 1f, 1f, alpha);
                }
                var dd = rowObj.transform.Find("GiftDropdown")?.GetComponent<TMP_Dropdown>();
                if (dd != null)
                {
                    dd.interactable = enabled;
                    var ddImg = dd.GetComponent<Image>();
                    if (ddImg != null) ddImg.color = new Color(0.08f, 0.20f, 0.28f, 0.95f * alpha);
                }
            }

            void UpdateIcon(string giftName, int giftId)
            {
                if (iconImg == null) return;

                Sprite resolved = null;
                string key = (giftName ?? "").ToLowerInvariant().Trim();

                if (iconMap != null && !string.IsNullOrEmpty(key) && iconMap.TryGetValue(key, out var s) && s != null)
                {
                    resolved = s;
                }

                if (resolved == null)
                {
                    resolved = PreGameUIBuilder.ResolveGiftIcon(giftId, giftName);
                }

                if (resolved != null)
                {
                    iconImg.sprite = resolved;
                    iconImg.color = new Color(1f, 1f, 1f, item.isEnabled ? 1f : 0.45f);
                    iconImg.enabled = true;
                }
                else
                {
                    // Never leave an empty sprite enabled as white rectangle
                    iconImg.sprite = null;
                    iconImg.color = Color.clear;
                    iconImg.enabled = false;
                }
            }

            UpdateIcon(item.giftName, item.giftId);
            UpdateRowVisualState(item.isEnabled);

            // 1. Enable Toggle
            var toggle = rowObj.transform.Find("ToggleEnable")?.GetComponent<Toggle>();
            if (toggle != null)
            {
                var swapper = toggle.GetComponent<Views.ToggleSpriteSwapper>();
                toggle.isOn = item.isEnabled;
                if (swapper != null) swapper.SyncVisual();

                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener((isOn) =>
                {
                    item.isEnabled = isOn;
                    if (swapper != null) swapper.SyncVisual();
                    UpdateIcon(item.giftName, item.giftId);
                    UpdateRowVisualState(isOn);
                });
            }

            // 4. Gift Dropdown
            var dropdown = rowObj.transform.Find("GiftDropdown")?.GetComponent<TMP_Dropdown>();
            if (dropdown != null)
            {
                dropdown.ClearOptions();
                var options = new List<TMP_Dropdown.OptionData>();
                int selectedIndex = 0;
                for (int i = 0; i < PreGameConfigData.AvailableGifts.Length; i++)
                {
                    var opt = PreGameConfigData.AvailableGifts[i];
                    Sprite icon = null;
                    if (iconMap != null && iconMap.ContainsKey(opt.giftName.ToLowerInvariant()))
                    {
                        icon = iconMap[opt.giftName.ToLowerInvariant()];
                    }
                    options.Add(new TMP_Dropdown.OptionData(opt.giftName) { image = icon });
                    if (opt.giftId == item.giftId || string.Equals(opt.giftName, item.giftName, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                    }
                }
                dropdown.AddOptions(options);
                dropdown.value = selectedIndex;
                dropdown.RefreshShownValue();

                dropdown.onValueChanged.RemoveAllListeners();
                dropdown.onValueChanged.AddListener((newIdx) =>
                {
                    if (newIdx >= 0 && newIdx < PreGameConfigData.AvailableGifts.Length)
                    {
                        var selectedGift = PreGameConfigData.AvailableGifts[newIdx];
                        item.giftId = selectedGift.giftId;
                        item.giftName = selectedGift.giftName;
                        UpdateIcon(selectedGift.giftName, selectedGift.giftId);
                    }
                });
            }

            // Ensure StatInput exists and elements are aligned
            var statInputTr = rowObj.transform.Find("StatInput");
            if (statInputTr == null)
            {
                var togRt = rowObj.transform.Find("ToggleEnable")?.GetComponent<RectTransform>();
                if (togRt != null) PreGameUIBuilder.SetRect(togRt, 10f, 0f, 56f, 22f);

                var fnRt = rowObj.transform.Find("FeatureNameText")?.GetComponent<RectTransform>();
                if (fnRt != null) PreGameUIBuilder.SetRect(fnRt, 74f, 0f, 142f, 36f);

                var iconRt = rowObj.transform.Find("Icon")?.GetComponent<RectTransform>();
                if (iconRt != null) PreGameUIBuilder.SetRect(iconRt, 222f, 0f, 30f, 30f);

                var ddRt = rowObj.transform.Find("GiftDropdown")?.GetComponent<RectTransform>();
                if (ddRt != null) PreGameUIBuilder.SetRect(ddRt, 256f, 0f, 110f, 34f);

                var diRt = rowObj.transform.Find("DescInput")?.GetComponent<RectTransform>();
                if (diRt != null) PreGameUIBuilder.SetRect(diRt, 450f, 0f, 194f, 34f);

                var upRt = rowObj.transform.Find("BtnUp")?.GetComponent<RectTransform>();
                if (upRt != null) PreGameUIBuilder.SetRect(upRt, 648f, 0f, 26f, 28f);

                var downRt = rowObj.transform.Find("BtnDown")?.GetComponent<RectTransform>();
                if (downRt != null) PreGameUIBuilder.SetRect(downRt, 678f, 0f, 26f, 28f);

                var font = featTxt != null ? featTxt.font : null;
                var statObj = PreGameUIBuilder.CreateStatInputField(rowObj.transform, "StatInput", font);
                PreGameUIBuilder.SetRect(statObj.GetComponent<RectTransform>(), 372f, 0f, 72f, 34f);
                statInputTr = statObj.transform;
            }

            // 5. Stat Input (Value of Gift) - Only show if this action has a stat
            bool hasStat = PreGameGiftItemConfig.HasStatForAction(item.action);
            if (statInputTr != null)
            {
                if (!hasStat)
                {
                    // Gift has no numeric stat (e.g. Follower Runner, VIP Ticket) -> Hide completely as requested
                    statInputTr.gameObject.SetActive(false);
                }
                else
                {
                    statInputTr.gameObject.SetActive(true);
                    if (item.customValue <= 0f)
                    {
                        item.customValue = PreGameGiftItemConfig.GetDefaultStat(item.action);
                    }

                    var statInput = statInputTr.GetComponent<TMP_InputField>();
                    if (statInput != null)
                    {
                        statInput.contentType = TMP_InputField.ContentType.DecimalNumber;
                        statInput.text = item.customValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                        var placeholder = statInput.placeholder as TextMeshProUGUI;
                        if (placeholder != null)
                        {
                            string unit = PreGameGiftItemConfig.GetStatUnit(item.action);
                            placeholder.text = string.IsNullOrEmpty(unit) ? "0" : unit;
                        }

                        statInput.onValueChanged.RemoveAllListeners();
                        statInput.onValueChanged.AddListener((val) =>
                        {
                            string clean = val.Replace(',', '.').Trim();
                            if (float.TryParse(clean, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed) && parsed > 0f)
                            {
                                item.customValue = parsed;
                            }
                        });
                    }
                }
            }

            // 6. Description Input
            var descInput = rowObj.transform.Find("DescInput")?.GetComponent<TMP_InputField>();
            if (descInput != null)
            {
                descInput.text = item.description;
                descInput.onValueChanged.RemoveAllListeners();
                descInput.onValueChanged.AddListener((val) => item.description = val);
            }

            // 7. Up Button
            var btnUp = rowObj.transform.Find("BtnUp")?.GetComponent<Button>();
            if (btnUp != null)
            {
                btnUp.interactable = index > 0;
                btnUp.onClick.RemoveAllListeners();
                btnUp.onClick.AddListener(() =>
                {
                    if (index > 0)
                    {
                        var temp = gifts[index];
                        gifts[index] = gifts[index - 1];
                        gifts[index - 1] = temp;
                        RebuildGiftsList(gifts);
                    }
                });
            }

            // 8. Down Button
            var btnDown = rowObj.transform.Find("BtnDown")?.GetComponent<Button>();
            if (btnDown != null)
            {
                btnDown.interactable = index < gifts.Count - 1;
                btnDown.onClick.RemoveAllListeners();
                btnDown.onClick.AddListener(() =>
                {
                    if (index < gifts.Count - 1)
                    {
                        var temp = gifts[index];
                        gifts[index] = gifts[index + 1];
                        gifts[index + 1] = temp;
                        RebuildGiftsList(gifts);
                    }
                });
            }
        }

        private GameObject CreateFallbackGiftRow(PreGameGiftItemConfig item, int index)
        {
            GameObject row = new GameObject($"GiftRow_{index}", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(_giftsContainer, false);
            var rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 42f);
            var img = row.GetComponent<Image>();
            img.color = new Color(0.08f, 0.2f, 0.28f, 0.85f);
            return row;
        }

        public void ReadUIIntoData()
        {
            if (PreGameConfigManager.Instance == null || PreGameConfigManager.Instance.CurrentConfig == null) return;
            var data = PreGameConfigManager.Instance.CurrentConfig;

            // Username
            if (_inputUsername != null)
            {
                data.tiktokUsername = _inputUsername.text.Trim().TrimStart('@');
            }

            // Target Distance (km input -> meters internal)
            if (_toggleInfiniteDistance != null && _toggleInfiniteDistance.isOn)
            {
                data.isInfiniteDistance = true;
                data.targetDistanceMeters = 999999f * 1000f;
                // data.finiteTargetDistanceMeters is preserved untouched
            }
            else
            {
                data.isInfiniteDistance = false;
                float kmVal = 0f;
                bool parsed = false;

                if (_inputTargetDistance != null)
                {
                    string rawText = _inputTargetDistance.text.Replace(',', '.').Trim();
                    if (!string.Equals(rawText, "Infinite", StringComparison.OrdinalIgnoreCase))
                    {
                        parsed = float.TryParse(rawText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out kmVal);
                    }
                }

                if (parsed && kmVal > 0f)
                {
                    data.finiteTargetDistanceMeters = Mathf.Max(50f, kmVal * 1000f);
                    data.targetDistanceMeters = data.finiteTargetDistanceMeters;
                    _lastFiniteDistanceKm = kmVal;
                }
                else
                {
                    // Fallback to preserved finite target distance or default 1km
                    float fallback = (data.finiteTargetDistanceMeters > 0f && data.finiteTargetDistanceMeters < 900000000f)
                        ? data.finiteTargetDistanceMeters
                        : 1000f;
                    data.finiteTargetDistanceMeters = fallback;
                    data.targetDistanceMeters = fallback;
                    _lastFiniteDistanceKm = fallback / 1000f;
                    if (_inputTargetDistance != null)
                    {
                        _inputTargetDistance.text = _lastFiniteDistanceKm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }

            // Energy Costs
            if (_inputLaneCost != null && int.TryParse(_inputLaneCost.text, out int laneCost))
                data.laneChangeEnergyCost = Mathf.Max(0, laneCost);

            if (_inputJumpCost != null && int.TryParse(_inputJumpCost.text, out int jumpCost))
                data.jumpEnergyCost = Mathf.Max(0, jumpCost);

            // Buff Durations
            if (_inputFreeControlDuration != null && float.TryParse(_inputFreeControlDuration.text, out float freeDuration))
                data.freeControlDuration = Mathf.Max(1f, freeDuration);

            if (_inputSprintDuration != null && float.TryParse(_inputSprintDuration.text, out float sprintDuration))
                data.sprintBuffDuration = Mathf.Max(1f, sprintDuration);

            // Anti Settings
            if (_toggleAutoSpawnCar != null)
                data.autoSpawnOnFullEnergy = _toggleAutoSpawnCar.isOn;

            if (_inputMaxCars != null && int.TryParse(_inputMaxCars.text, out int maxCars))
                data.maxConcurrentCars = Mathf.Clamp(maxCars, 1, 3);

            // Penalties
            if (_inputDistancePenalty != null && float.TryParse(_inputDistancePenalty.text, out float distPen))
                data.distancePenaltyMeters = Mathf.Max(0f, distPen);

            if (_inputSedanEnergyPenalty != null && float.TryParse(_inputSedanEnergyPenalty.text, out float sedanPen))
                data.sedanEnergyPenaltyPercent = Mathf.Clamp(sedanPen, 0f, 100f);

            if (_inputPickupEnergyPenalty != null && float.TryParse(_inputPickupEnergyPenalty.text, out float pickupPen))
                data.pickupEnergyPenaltyPercent = Mathf.Clamp(pickupPen, 0f, 100f);

            if (_inputHeavyEnergyPenalty != null && float.TryParse(_inputHeavyEnergyPenalty.text, out float heavyPen))
                data.heavyTruckEnergyPenaltyPercent = Mathf.Clamp(heavyPen, 0f, 100f);

            // Live Demo Simulation Reads
            if (_toggleLiveDemoMaster != null)
                data.enableLiveDemoSimulation = _toggleLiveDemoMaster.isOn;
            if (_toggleSimulatedChats != null)
                data.enableSimulatedChats = _toggleSimulatedChats.isOn;
            if (_toggleSimulatedGifts != null)
                data.enableSimulatedGifts = _toggleSimulatedGifts.isOn;
            if (_toggleSimulatedLikes != null)
                data.enableSimulatedLikes = _toggleSimulatedLikes.isOn;
            if (_toggleSimulatedFollowers != null)
                data.enableSimulatedFollowers = _toggleSimulatedFollowers.isOn;
            if (_toggleSimulatedDelay != null)
                data.enableSimulatedStreamDelay = _toggleSimulatedDelay.isOn;
            if (_toggleShowHowToPlayGuide != null)
                data.showHowToPlayGuide = _toggleShowHowToPlayGuide.isOn;
            if (_toggleShowGiftInfoPanel != null)
                data.showGiftInfoPanel = _toggleShowGiftInfoPanel.isOn;
            if (_toggleShowStopwatch != null)
                data.showStopwatchTimer = _toggleShowStopwatch.isOn;
            if (_toggleShowTimerCircles != null)
                data.showTimerCircles = _toggleShowTimerCircles.isOn;
            if (_toggleDebugUI != null)
                data.enableDebugUI = _toggleDebugUI.isOn;

            // Backend Reads
            if (_inputBackendPort != null && int.TryParse(_inputBackendPort.text, out int bPort))
                data.backendPort = bPort;
            if (_inputBackendSocketPort != null && int.TryParse(_inputBackendSocketPort.text, out int sPort))
                data.backendSocketPort = sPort;
            if (_inputEulerApiKey != null)
                data.eulerApiKey = _inputEulerApiKey.text.Trim();
            if (_inputBackendDir != null)
                data.backendDirectory = _inputBackendDir.text.Trim();

            // Flush active gift row StatInput and DescInput values
            if (_giftsContainer != null && data.gifts != null)
            {
                int rIdx = 0;
                for (int i = 0; i < _giftsContainer.childCount; i++)
                {
                    var child = _giftsContainer.GetChild(i);
                    if (_giftRowTemplate != null && child.gameObject == _giftRowTemplate) continue;
                    if (rIdx < data.gifts.Count)
                    {
                        var statIn = child.Find("StatInput")?.GetComponent<TMP_InputField>();
                        if (statIn != null && statIn.gameObject.activeSelf)
                        {
                            string raw = statIn.text.Replace(',', '.').Trim();
                            if (float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed) && parsed > 0f)
                            {
                                data.gifts[rIdx].customValue = parsed;
                            }
                        }

                        var descIn = child.Find("DescInput")?.GetComponent<TMP_InputField>();
                        if (descIn != null)
                        {
                            data.gifts[rIdx].description = descIn.text;
                        }
                    }
                    rIdx++;
                }
            }
        }

        #region TikTok Backend Handlers

        public void OnCheckPortClicked()
        {
            ReadUIIntoData();
            var data = PreGameConfigManager.Instance?.CurrentConfig;
            int httpPort = data != null && data.backendPort > 0 ? data.backendPort : 9091;
            int socketPort = data != null && data.backendSocketPort > 0 ? data.backendSocketPort : 3001;

            if (_txtBackendStatus != null)
            {
                _txtBackendStatus.text = "<color=#FCDA21>Checking ports...</color>";
            }

            TikTokBackendManager.Instance.CheckPort(socketPort, (socketOpen, _) =>
            {
                TikTokBackendManager.Instance.CheckPort(httpPort, (httpOpen, _) =>
                {
                    if (_txtBackendStatus != null)
                    {
                        string sStatus = socketOpen ? "<color=#76D12C>OPEN</color>" : "<color=#FF4D57>CLOSED</color>";
                        string hStatus = httpOpen ? "<color=#76D12C>OPEN</color>" : "<color=#FF4D57>CLOSED</color>";
                        _txtBackendStatus.text = $"Status: Socket {socketPort}: {sStatus} | HTTP {httpPort}: {hStatus}";
                    }
                });
            });
        }

        public void OnStartBackendClicked()
        {
            ReadUIIntoData();
            if (PreGameConfigManager.Instance != null)
            {
                PreGameConfigManager.Instance.SaveConfig();
            }

            var data = PreGameConfigManager.Instance?.CurrentConfig;
            string dir = data != null && !string.IsNullOrEmpty(data.backendDirectory) ? data.backendDirectory : TikTokBackendManager.DefaultLocalPath;
            int httpPort = data != null && data.backendPort > 0 ? data.backendPort : 9091;
            int socketPort = data != null && data.backendSocketPort > 0 ? data.backendSocketPort : 3001;
            string apiKey = data != null && !string.IsNullOrEmpty(data.eulerApiKey) ? data.eulerApiKey : TikTokBackendManager.DefaultEulerApiKey;

            if (_txtBackendStatus != null)
            {
                _txtBackendStatus.text = "<color=#FCDA21>Launching backend & testing ports...</color>";
            }

            TikTokBackendManager.Instance.StartBackend(dir, httpPort, socketPort, apiKey, (success, msg) =>
            {
                if (_txtBackendStatus != null)
                {
                    _txtBackendStatus.text = success ? $"<color=#76D12C>[OK] {msg}</color>" : $"<color=#FF4D57>[ERROR] {msg}</color>";
                }
            });
        }

        public void OnSetupBackendClicked()
        {
            ReadUIIntoData();
            var data = PreGameConfigManager.Instance?.CurrentConfig;
            string dir = data != null && !string.IsNullOrEmpty(data.backendDirectory) ? data.backendDirectory : TikTokBackendManager.DefaultLocalPath;
            int httpPort = data != null && data.backendPort > 0 ? data.backendPort : 9091;
            int socketPort = data != null && data.backendSocketPort > 0 ? data.backendSocketPort : 3001;
            string apiKey = data != null && !string.IsNullOrEmpty(data.eulerApiKey) ? data.eulerApiKey : TikTokBackendManager.DefaultEulerApiKey;

            if (_txtBackendStatus != null)
            {
                _txtBackendStatus.text = "<color=#FCDA21>Checking Bun runtime (bun --version)...</color>";
            }

            TikTokBackendManager.Instance.SetupBackendFromGit(dir, httpPort, socketPort, apiKey, (success, msg) =>
            {
                if (_txtBackendStatus != null)
                {
                    _txtBackendStatus.text = success ? $"<color=#76D12C>[OK] {msg}</color>" : $"<color=#FF4D57>[ERROR] {msg}</color>";
                }
            }, progressMsg =>
            {
                if (_txtBackendStatus != null)
                {
                    _txtBackendStatus.text = $"<color=#FCDA21>{progressMsg}</color>";
                }
            });
        }

        public void OnStopBackendClicked()
        {
            ReadUIIntoData();
            var data = PreGameConfigManager.Instance?.CurrentConfig;
            int httpPort = data != null && data.backendPort > 0 ? data.backendPort : 9091;
            int socketPort = data != null && data.backendSocketPort > 0 ? data.backendSocketPort : 3001;

            if (_txtBackendStatus != null)
            {
                _txtBackendStatus.text = "<color=#FCDA21>Stopping backend...</color>";
            }

            TikTokBackendManager.Instance.StopBackend(httpPort, socketPort, (success, msg) =>
            {
                if (_txtBackendStatus != null)
                {
                    _txtBackendStatus.text = success ? $"<color=#76D12C>[OK] {msg}</color>" : $"<color=#FF4D57>[ERROR] {msg}</color>";
                }
            });
        }

        public void OnBrowseBackendDirClicked()
        {
            string current = _inputBackendDir != null ? _inputBackendDir.text : "";
            string selected = TikTokBackendManager.BrowseFolderDialog("Select TikTok Live Backend Folder", current);
            if (!string.IsNullOrEmpty(selected))
            {
                if (_inputBackendDir != null)
                {
                    _inputBackendDir.text = selected;
                }
                if (PreGameConfigManager.Instance?.CurrentConfig != null)
                {
                    PreGameConfigManager.Instance.CurrentConfig.backendDirectory = selected;
                    PreGameConfigManager.Instance.SaveConfig();
                }
            }
        }

        #endregion

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

        private System.Collections.IEnumerator ShowSavedFeedback()
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
            if (!_isLiveVerified)
            {
                SetLiveVerifiedUI(false, "VERIFY FIRST");
                return;
            }

            ReadUIIntoData();
            PreGameConfigManager.Instance?.StartGoLive();
        }

        public void SetLiveVerifiedUI(bool isVerified, string statusText)
        {
            _isLiveVerified = isVerified;
            if (_txtLiveStatus != null)
            {
                if (isVerified)
                {
                    _txtLiveStatus.text = $"<color=#30E070>{statusText}</color>";
                }
                else if (statusText.Contains("CHECKING"))
                {
                    _txtLiveStatus.text = $"<color=#FCBA03>{statusText}</color>";
                }
                else if (statusText.Contains("OFFLINE") || statusText.Contains("FAILED") || statusText.Contains("NOT FOUND") || statusText.Contains("ERROR") || statusText.Contains("TIMEOUT") || statusText.Contains("OFF") || statusText.Contains("VERIFY") || statusText.Contains("USER"))
                {
                    _txtLiveStatus.text = $"<color=#FF4050>{statusText}</color>";
                }
                else
                {
                    _txtLiveStatus.text = $"<color=#88A0B0>{statusText}</color>";
                }
            }

            if (_btnGoLive != null)
            {
                _btnGoLive.interactable = isVerified;
                var btnImg = _btnGoLive.GetComponent<Image>();
                var btnTxt = _btnGoLive.GetComponentInChildren<TextMeshProUGUI>();
                if (btnImg != null)
                {
                    btnImg.color = isVerified ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.65f);
                }
                if (btnTxt != null)
                {
                    btnTxt.color = isVerified ? Color.black : new Color(0.3f, 0.3f, 0.3f, 0.7f);
                }
            }
        }

        public void OnCheckLiveClicked()
        {
            if (_checkLiveCoroutine != null)
            {
                StopCoroutine(_checkLiveCoroutine);
            }
            _checkLiveCoroutine = StartCoroutine(CheckLiveRoutine());
        }

        private IEnumerator CheckLiveRoutine()
        {
            string username = _inputUsername != null ? _inputUsername.text.Trim().TrimStart('@') : "";
            if (string.IsNullOrEmpty(username))
            {
                SetLiveVerifiedUI(false, "ENTER @USER");
                yield break;
            }

            SetLiveVerifiedUI(false, "CHECKING...");
            if (_btnCheckLive != null) _btnCheckLive.interactable = false;

            // 1. Check if backend port is responding
            int socketPort = 3001;
            if (PreGameConfigManager.Instance?.CurrentConfig != null && PreGameConfigManager.Instance.CurrentConfig.backendSocketPort > 0)
            {
                socketPort = PreGameConfigManager.Instance.CurrentConfig.backendSocketPort;
            }

            bool portChecked = false;
            bool portOpen = false;
            TikTokBackendManager.Instance.CheckPort(socketPort, (isOpen, msg) =>
            {
                portOpen = isOpen;
                portChecked = true;
            });

            float portWait = 2.5f;
            while (!portChecked && portWait > 0f)
            {
                portWait -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (!portOpen)
            {
                SetLiveVerifiedUI(false, "BACKEND OFF");
                if (_btnCheckLive != null) _btnCheckLive.interactable = true;
                Debug.LogWarning($"[PreGameConfigUI] Cannot check live: Backend socket port {socketPort} is closed. Please start backend in Advanced tab!");
                yield break;
            }

            // 2. Connect client to test room connection
            var client = FindFirstObjectByType<TikTokLiveClient>();
            if (client == null)
            {
                var go = new GameObject("TikTokLiveClient");
                client = go.AddComponent<TikTokLiveClient>();
            }

            client.SetServerUrl($"http://localhost:{socketPort}");

            bool connected = false;
            bool disconnected = false;
            string disconnectReason = "";
            string roomId = "";

            Action<TikTokConnectedEvent> onConn = evt =>
            {
                connected = true;
                roomId = evt.RoomId;
            };
            Action<TikTokDisconnectedEvent> onDisc = evt =>
            {
                disconnected = true;
                disconnectReason = evt.Reason;
            };

            EventBus.Subscribe(onConn);
            EventBus.Subscribe(onDisc);

            client.ConnectWithUsername(username);

            float timeout = 12f;
            float elapsed = 0f;
            while (!connected && !disconnected && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            EventBus.Unsubscribe(onConn);
            EventBus.Unsubscribe(onDisc);

            if (connected)
            {
                _verifiedUsername = username;
                SetLiveVerifiedUI(true, "CONNECTED");
                Debug.Log($"<color=#30E070>[PreGameConfigUI] TikTok Live connection verified for @{username} (Room: {roomId})</color>");
            }
            else if (disconnected)
            {
                SetLiveVerifiedUI(false, "OFFLINE");
                Debug.LogWarning($"[PreGameConfigUI] TikTok Live connection failed for @{username}: {disconnectReason}");
            }
            else
            {
                SetLiveVerifiedUI(false, "TIMEOUT");
                Debug.LogWarning($"[PreGameConfigUI] TikTok Live connection check timed out for @{username}. Verify user is live.");
            }

            if (_btnCheckLive != null) _btnCheckLive.interactable = true;
            _checkLiveCoroutine = null;
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
    }
}

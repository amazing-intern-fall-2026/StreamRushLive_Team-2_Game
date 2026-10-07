using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SteamRush.Features.StreamIntegration;

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
        [SerializeField] private TMP_InputField _inputTargetDistance;
        [SerializeField] private Toggle _toggleInfiniteDistance;
        [SerializeField] private Transform _giftsContainer;
        [SerializeField] private GameObject _giftRowTemplate;

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
        [SerializeField] private Toggle _toggleDebugUI;

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
        }

        private void OnInfiniteToggleChanged(bool isInfinite)
        {
            if (_inputTargetDistance != null)
            {
                _inputTargetDistance.interactable = !isInfinite;
                if (isInfinite)
                {
                    _inputTargetDistance.text = "Infinite (∞)";
                }
                else
                {
                    float km = 1f;
                    if (PreGameConfigManager.Instance?.CurrentConfig != null)
                    {
                        float m = PreGameConfigManager.Instance.CurrentConfig.targetDistanceMeters;
                        if (m > 0 && m <= 1000000f) km = m / 1000f;
                    }
                    _inputTargetDistance.text = km.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }

        public void SwitchTab(bool isBasic)
        {
            if (_panelBasicContent != null) _panelBasicContent.SetActive(isBasic);
            if (_panelAdvancedContent != null) _panelAdvancedContent.SetActive(!isBasic);

            if (_tabBasicHighlight != null) _tabBasicHighlight.color = isBasic ? _activeTabColor : _inactiveTabColor;
            if (_tabAdvancedHighlight != null) _tabAdvancedHighlight.color = !isBasic ? _activeTabColor : _inactiveTabColor;

            var txtBasic = _btnTabBasic != null ? _btnTabBasic.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (txtBasic != null) txtBasic.color = isBasic ? Color.black : Color.white;

            var txtAdv = _btnTabAdvanced != null ? _btnTabAdvanced.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (txtAdv != null) txtAdv.color = !isBasic ? Color.black : Color.white;
        }

        public void PopulateUI(PreGameConfigData data)
        {
            if (data == null) return;

            // Basic Tab
            if (_inputUsername != null) _inputUsername.text = data.tiktokUsername;
            if (_toggleInfiniteDistance != null) _toggleInfiniteDistance.isOn = data.isInfiniteDistance;
            if (_inputTargetDistance != null)
            {
                _inputTargetDistance.interactable = !data.isInfiniteDistance;
                if (data.isInfiniteDistance)
                {
                    _inputTargetDistance.text = "Infinite (∞)";
                }
                else
                {
                    float km = data.targetDistanceMeters / 1000f;
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
            if (_toggleDebugUI != null) _toggleDebugUI.isOn = data.enableDebugUI;

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

            // Cache gift icons from TikTokGiftRouter
            var router = FindFirstObjectByType<TikTokGiftRouter>();
            var iconMap = new Dictionary<string, Sprite>();
            if (router != null)
            {
                foreach (var m in router.GiftMappings)
                {
                    if (m.giftIcon != null && !iconMap.ContainsKey(m.giftName.ToLowerInvariant()))
                    {
                        iconMap[m.giftName.ToLowerInvariant()] = m.giftIcon;
                    }
                }
            }
#if UNITY_EDITOR
            string dir = "Assets/_Project/Textures/TikTokGifts";
            foreach (var opt in PreGameConfigData.AvailableGifts)
            {
                string key = opt.giftName.ToLowerInvariant();
                if (!iconMap.ContainsKey(key))
                {
                    Sprite s = null;
                    if (opt.giftId > 0) s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{opt.giftId}.png");
                    if (s == null) s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{key}.png");
                    if (s != null) iconMap[key] = s;
                }
            }
#endif

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
            // 1. Enable Toggle
            var toggle = rowObj.transform.Find("ToggleEnable")?.GetComponent<Toggle>();
            if (toggle != null)
            {
                toggle.isOn = item.isEnabled;
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener((isOn) => item.isEnabled = isOn);
            }

            // 2. Feature Name
            var featTxt = rowObj.transform.Find("FeatureNameText")?.GetComponent<TextMeshProUGUI>();
            if (featTxt != null)
            {
                featTxt.text = string.IsNullOrEmpty(item.featureName) ? PreGameGiftItemConfig.GetDefaultFeatureName(item.action) : item.featureName;
            }

            // 3. Gift Image
            var iconImg = rowObj.transform.Find("Icon")?.GetComponent<Image>();
            void UpdateIcon(string giftName)
            {
                if (iconImg != null && iconMap != null)
                {
                    string key = (giftName ?? "").ToLowerInvariant();
                    if (iconMap.ContainsKey(key))
                    {
                        iconImg.sprite = iconMap[key];
                        iconImg.enabled = true;
                    }
                }
            }
            UpdateIcon(item.giftName);

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
                        UpdateIcon(selectedGift.giftName);
                    }
                });
            }

            // 5. Description Input
            var descInput = rowObj.transform.Find("DescInput")?.GetComponent<TMP_InputField>();
            if (descInput != null)
            {
                descInput.text = item.description;
                descInput.onValueChanged.RemoveAllListeners();
                descInput.onValueChanged.AddListener((val) => item.description = val);
            }

            // 6. Up Button
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

            // 7. Down Button
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
            }
            else
            {
                data.isInfiniteDistance = false;
                if (_inputTargetDistance != null)
                {
                    string rawText = _inputTargetDistance.text.Replace(',', '.').Trim();
                    if (float.TryParse(rawText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float kmVal))
                    {
                        data.targetDistanceMeters = Mathf.Max(50f, kmVal * 1000f);
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
            if (_toggleDebugUI != null)
                data.enableDebugUI = _toggleDebugUI.isOn;
        }

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
    }
}

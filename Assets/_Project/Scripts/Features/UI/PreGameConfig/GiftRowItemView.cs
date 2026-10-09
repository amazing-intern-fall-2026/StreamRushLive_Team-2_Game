using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SteamRush.Features.StreamIntegration;
using SteamRush.Features.UI.Views;

namespace SteamRush.Features.UI.PreGameConfig
{
    /// <summary>
    /// View component for individual Gift rows in PreGameConfig UI.
    /// Manages data binding, inputs, dropdown selection, and ordering.
    /// </summary>
    public class GiftRowItemView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Toggle _toggleEnable;
        [SerializeField] private ToggleSpriteSwapper _swapper;
        [SerializeField] private TextMeshProUGUI _txtFeatureName;
        [SerializeField] private Image _imgIcon;
        [SerializeField] private TMP_Dropdown _dropdownGift;
        [SerializeField] private TMP_InputField _inputStat;
        [SerializeField] private TMP_InputField _inputDesc;
        [SerializeField] private Button _btnUp;
        [SerializeField] private Button _btnDown;

        private PreGameGiftItemConfig _item;

        private void Awake()
        {
            AutoWireIfNull();
        }

        public void AutoWireIfNull()
        {
            if (_toggleEnable == null) _toggleEnable = transform.Find("ToggleEnable")?.GetComponent<Toggle>();
            if (_swapper == null && _toggleEnable != null) _swapper = _toggleEnable.GetComponent<ToggleSpriteSwapper>();
            if (_txtFeatureName == null) _txtFeatureName = transform.Find("FeatureNameText")?.GetComponent<TextMeshProUGUI>();
            if (_imgIcon == null) _imgIcon = transform.Find("Icon")?.GetComponent<Image>();
            if (_dropdownGift == null) _dropdownGift = transform.Find("GiftDropdown")?.GetComponent<TMP_Dropdown>();
            if (_inputStat == null) _inputStat = transform.Find("StatInput")?.GetComponent<TMP_InputField>();
            if (_inputDesc == null) _inputDesc = transform.Find("DescInput")?.GetComponent<TMP_InputField>();
            if (_btnUp == null) _btnUp = transform.Find("BtnUp")?.GetComponent<Button>();
            if (_btnDown == null) _btnDown = transform.Find("BtnDown")?.GetComponent<Button>();
        }

        public void Bind(
            PreGameGiftItemConfig item,
            int index,
            int totalCount,
            Dictionary<string, Sprite> iconMap,
            Action<int> onMoveUp,
            Action<int> onMoveDown)
        {
            _item = item;
            AutoWireIfNull();

            // 1. Feature Name
            if (_txtFeatureName != null)
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
                _txtFeatureName.text = fName;
            }

            // 2. Icon & Visual alpha
            UpdateIcon(item.giftName, item.giftId, iconMap);
            UpdateVisualState(item.isEnabled);

            // 3. Toggle Enable
            if (_toggleEnable != null)
            {
                _toggleEnable.isOn = item.isEnabled;
                if (_swapper != null) _swapper.SyncVisual();

                _toggleEnable.onValueChanged.RemoveAllListeners();
                _toggleEnable.onValueChanged.AddListener(isOn =>
                {
                    item.isEnabled = isOn;
                    if (_swapper != null) _swapper.SyncVisual();
                    UpdateIcon(item.giftName, item.giftId, iconMap);
                    UpdateVisualState(isOn);
                });
            }

            // 4. Gift Dropdown
            if (_dropdownGift != null)
            {
                _dropdownGift.ClearOptions();
                var options = new List<TMP_Dropdown.OptionData>();
                int selectedIndex = 0;
                for (int i = 0; i < PreGameConfigData.AvailableGifts.Length; i++)
                {
                    var opt = PreGameConfigData.AvailableGifts[i];
                    Sprite icon = null;
                    if (iconMap != null && iconMap.TryGetValue(opt.giftName.ToLowerInvariant(), out var sp))
                    {
                        icon = sp;
                    }
                    options.Add(new TMP_Dropdown.OptionData(opt.giftName) { image = icon });
                    if (opt.giftId == item.giftId || string.Equals(opt.giftName, item.giftName, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                    }
                }
                _dropdownGift.AddOptions(options);
                _dropdownGift.value = selectedIndex;
                _dropdownGift.RefreshShownValue();

                _dropdownGift.onValueChanged.RemoveAllListeners();
                _dropdownGift.onValueChanged.AddListener(newIdx =>
                {
                    if (newIdx >= 0 && newIdx < PreGameConfigData.AvailableGifts.Length)
                    {
                        var selectedGift = PreGameConfigData.AvailableGifts[newIdx];
                        item.giftId = selectedGift.giftId;
                        item.giftName = selectedGift.giftName;
                        UpdateIcon(selectedGift.giftName, selectedGift.giftId, iconMap);
                    }
                });
            }

            // 5. Stat Input
            bool hasStat = PreGameGiftItemConfig.HasStatForAction(item.action);
            if (_inputStat != null)
            {
                _inputStat.gameObject.SetActive(hasStat);
                if (hasStat)
                {
                    if (item.customValue <= 0f)
                    {
                        item.customValue = PreGameGiftItemConfig.GetDefaultStat(item.action);
                    }
                    _inputStat.contentType = TMP_InputField.ContentType.DecimalNumber;
                    _inputStat.text = item.customValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                    if (_inputStat.placeholder is TextMeshProUGUI placeholder)
                    {
                        string unit = PreGameGiftItemConfig.GetStatUnit(item.action);
                        placeholder.text = string.IsNullOrEmpty(unit) ? "0" : unit;
                    }

                    _inputStat.onValueChanged.RemoveAllListeners();
                    _inputStat.onValueChanged.AddListener(val =>
                    {
                        string clean = val.Replace(',', '.').Trim();
                        if (float.TryParse(clean, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed) && parsed > 0f)
                        {
                            item.customValue = parsed;
                        }
                    });
                }
            }

            // 6. Description Input
            if (_inputDesc != null)
            {
                _inputDesc.text = item.description;
                _inputDesc.onValueChanged.RemoveAllListeners();
                _inputDesc.onValueChanged.AddListener(val => item.description = val);
            }

            // 7. Up / Down Buttons
            if (_btnUp != null)
            {
                _btnUp.interactable = index > 0;
                _btnUp.onClick.RemoveAllListeners();
                _btnUp.onClick.AddListener(() => onMoveUp?.Invoke(index));
            }

            if (_btnDown != null)
            {
                _btnDown.interactable = index < totalCount - 1;
                _btnDown.onClick.RemoveAllListeners();
                _btnDown.onClick.AddListener(() => onMoveDown?.Invoke(index));
            }
        }

        private void UpdateIcon(string giftName, int giftId, Dictionary<string, Sprite> iconMap)
        {
            if (_imgIcon == null) return;

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
                _imgIcon.sprite = resolved;
                _imgIcon.color = new Color(1f, 1f, 1f, (_item != null && _item.isEnabled) ? 1f : 0.45f);
                _imgIcon.enabled = true;
            }
            else
            {
                _imgIcon.sprite = null;
                _imgIcon.color = Color.clear;
                _imgIcon.enabled = false;
            }
        }

        private void UpdateVisualState(bool enabled)
        {
            float alpha = enabled ? 1f : 0.45f;
            if (_txtFeatureName != null) _txtFeatureName.color = new Color(1f, 1f, 1f, alpha);
            if (_imgIcon != null && _imgIcon.sprite != null) _imgIcon.color = new Color(1f, 1f, 1f, alpha);

            if (_inputStat != null)
            {
                _inputStat.interactable = enabled;
                var sImg = _inputStat.GetComponent<Image>();
                if (sImg != null) sImg.color = new Color(0.08f, 0.20f, 0.28f, 0.95f * alpha);
                if (_inputStat.textComponent != null) _inputStat.textComponent.color = new Color(1f, 1f, 1f, alpha);
            }

            if (_inputDesc != null)
            {
                _inputDesc.interactable = enabled;
                var dImg = _inputDesc.GetComponent<Image>();
                if (dImg != null) dImg.color = new Color(0.08f, 0.20f, 0.28f, 0.95f * alpha);
                if (_inputDesc.textComponent != null) _inputDesc.textComponent.color = new Color(1f, 1f, 1f, alpha);
            }

            if (_dropdownGift != null)
            {
                _dropdownGift.interactable = enabled;
                var ddImg = _dropdownGift.GetComponent<Image>();
                if (ddImg != null) ddImg.color = new Color(0.08f, 0.20f, 0.28f, 0.95f * alpha);
                var arrowImg = _dropdownGift.transform.Find("Arrow")?.GetComponent<Image>();
                if (arrowImg != null) arrowImg.color = new Color(0.75f, 0.89f, 0.91f, alpha);
            }
        }

        public void SaveCurrentValues()
        {
            if (_item == null) return;
            if (_inputStat != null && _inputStat.gameObject.activeSelf)
            {
                string raw = _inputStat.text.Replace(',', '.').Trim();
                if (float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed) && parsed > 0f)
                {
                    _item.customValue = parsed;
                }
            }

            if (_inputDesc != null)
            {
                _item.description = _inputDesc.text;
            }
        }
    }
}

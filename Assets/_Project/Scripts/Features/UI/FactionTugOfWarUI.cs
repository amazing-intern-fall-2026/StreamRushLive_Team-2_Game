using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI
{
    // View thuan hien 2 thanh Fan (xanh, neo trai) / Anti (do, neo phai) - GDD v1.3 muc 4.
    // KHONG tu doc Dictionary cua FactionTugOfWarManager - chi nhan gia tri qua SetFactionValues()
    // (Manager goi qua UnityEvent, noi trong Inspector), dung SRP.
    public class FactionTugOfWarUI : MonoBehaviour
    {
        [Header("Fan (xanh, trai)")]
        [SerializeField] private Image _fanFillImage;
        [SerializeField] private TMP_Text _fanValueLabel;
        [SerializeField] private int _fanMaxValue = 1000;

        [Header("Anti (do, phai)")]
        [SerializeField] private Image _antiFillImage;
        [SerializeField] private TMP_Text _antiValueLabel;
        [SerializeField] private int _antiMaxValue = 500;

        // Duoc FactionTugOfWarManager.FactionValuesChanged goi moi lan co Like moi.
        public void SetFactionValues(int fanValue, int antiValue)
        {
            if (_fanFillImage != null)
            {
                _fanFillImage.fillAmount = _fanMaxValue > 0 ? Mathf.Clamp01((float)fanValue / _fanMaxValue) : 0f;
            }

            if (_fanValueLabel != null)
            {
                _fanValueLabel.text = fanValue.ToString();
            }

            if (_antiFillImage != null)
            {
                _antiFillImage.fillAmount = _antiMaxValue > 0 ? Mathf.Clamp01((float)antiValue / _antiMaxValue) : 0f;
            }

            if (_antiValueLabel != null)
            {
                _antiValueLabel.text = $"{antiValue}/{_antiMaxValue}";
            }
        }
    }
}

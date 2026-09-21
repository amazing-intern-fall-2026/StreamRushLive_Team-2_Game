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
        [SerializeField] private int _fanMaxValue = 100;

        [Header("Anti (do, phai)")]
        [SerializeField] private Image _antiFillImage;
        [SerializeField] private TMP_Text _antiValueLabel;
        [SerializeField] private int _antiMaxValue = 500;

        private static Sprite _fallbackWhiteSprite;
        private float _targetFanFill;
        private float _targetAntiFill;

        private void Awake()
        {
            EnsureSpriteAssigned(_fanFillImage);
            EnsureSpriteAssigned(_antiFillImage);
            if (_fanFillImage != null) _targetFanFill = _fanFillImage.fillAmount;
            if (_antiFillImage != null) _targetAntiFill = _antiFillImage.fillAmount;
        }

        private void Update()
        {
            if (_fanFillImage != null)
            {
                _fanFillImage.fillAmount = Mathf.Lerp(_fanFillImage.fillAmount, _targetFanFill, Time.deltaTime * 12f);
            }

            if (_antiFillImage != null)
            {
                _antiFillImage.fillAmount = Mathf.Lerp(_antiFillImage.fillAmount, _targetAntiFill, Time.deltaTime * 12f);
            }
        }

        private static void EnsureSpriteAssigned(Image img)
        {
            if (img != null && img.sprite == null)
            {
                if (_fallbackWhiteSprite == null)
                {
                    Texture2D whiteTex = Texture2D.whiteTexture;
                    _fallbackWhiteSprite = Sprite.Create(whiteTex, new Rect(0, 0, whiteTex.width, whiteTex.height), new Vector2(0.5f, 0.5f));
                }
                img.sprite = _fallbackWhiteSprite;
            }
        }

        // Duoc FactionTugOfWarManager.FactionValuesChanged goi moi lan co Like moi hoac tieu hao nang luong fast.
        public void SetFactionValues(int fanValue, int antiValue)
        {
            EnsureSpriteAssigned(_fanFillImage);
            EnsureSpriteAssigned(_antiFillImage);

            _targetFanFill = _fanMaxValue > 0 ? Mathf.Clamp01((float)fanValue / _fanMaxValue) : 0f;
            _targetAntiFill = _antiMaxValue > 0 ? Mathf.Clamp01((float)antiValue / _antiMaxValue) : 0f;

            if (_fanValueLabel != null)
            {
                _fanValueLabel.text = $"FAN\n{fanValue}/{_fanMaxValue}";
            }

            if (_antiValueLabel != null)
            {
                _antiValueLabel.text = $"ANTI\n{antiValue}/{_antiMaxValue}";
            }
        }
    }
}

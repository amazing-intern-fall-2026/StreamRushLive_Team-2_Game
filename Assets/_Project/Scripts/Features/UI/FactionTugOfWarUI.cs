using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI
{
    // View thuan hien 2 the Fan (xanh, trai) / Anti (do, phai) - GDD v1.3.1 muc 6.
    // KHONG tu doc du lieu cua FactionTugOfWarManager - chi nhan gia tri qua SetFactionValues()
    // (Manager goi qua UnityEvent, noi trong Inspector), dung SRP.
    // Nut "+X% Tim"/"+X Tim" o day la badge TINH (theo dung thiet ke goc), khong gan logic dong -
    // xem [[feedback]] ngay 22/09: gia tri chi mang tinh minh hoa, se dinh nghia lai sau.
    public class FactionTugOfWarUI : MonoBehaviour
    {
        [Header("Fan (xanh, trai)")]
        [SerializeField] private Image _fanFillImage;
        [SerializeField] private TMP_Text _fanValueLabel;
        [SerializeField] private int _fanMaxValue = 100;
        [SerializeField] private RectTransform _fanHandle;

        [Header("Anti (do, phai)")]
        [SerializeField] private Image _antiFillImage;
        [SerializeField] private TMP_Text _antiValueLabel;
        [SerializeField] private int _antiMaxValue = 300;
        [SerializeField] private RectTransform _antiHandle;

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
                UpdateHandlePosition(_fanHandle, _fanFillImage);
            }

            if (_antiFillImage != null)
            {
                _antiFillImage.fillAmount = Mathf.Lerp(_antiFillImage.fillAmount, _targetAntiFill, Time.deltaTime * 12f);
                UpdateHandlePosition(_antiHandle, _antiFillImage);
            }
        }

        // Glow nho bam theo dung mep tren cua vach fill hien tai - doc chieu cao thuc te tu chinh
        // RectTransform cua fillImage (khong hardcode so, vi Image kieu Filled khong tu resize).
        private static void UpdateHandlePosition(RectTransform handle, Image fillImage)
        {
            if (handle == null) return;
            float trackHeight = fillImage.rectTransform.rect.height;
            Vector2 pos = handle.anchoredPosition;
            pos.y = fillImage.fillAmount * trackHeight;
            handle.anchoredPosition = pos;
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
        // Fan hien theo % (nang luong con lai so voi muc toi da) - Anti hien theo phan so thuc
        // (can biet chinh xac con bao nhieu tim nua thi cham nguong 500 sinh xe).
        public void SetFactionValues(int fanValue, int antiValue)
        {
            EnsureSpriteAssigned(_fanFillImage);
            EnsureSpriteAssigned(_antiFillImage);

            _targetFanFill = _fanMaxValue > 0 ? Mathf.Clamp01((float)fanValue / _fanMaxValue) : 0f;
            _targetAntiFill = _antiMaxValue > 0 ? Mathf.Clamp01((float)antiValue / _antiMaxValue) : 0f;

            if (_fanValueLabel != null)
            {
                int fanPercent = _fanMaxValue > 0 ? Mathf.RoundToInt(100f * fanValue / _fanMaxValue) : 0;
                _fanValueLabel.text = $"{fanPercent}%";
            }

            if (_antiValueLabel != null)
            {
                _antiValueLabel.text = $"{antiValue}/{_antiMaxValue}";
            }
        }
    }
}

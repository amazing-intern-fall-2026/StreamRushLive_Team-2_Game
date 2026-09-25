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
        [SerializeField] private int _fanMaxValue = 1000;
        [SerializeField] private RectTransform _fanHandle;

        [Header("Anti (do, phai)")]
        [SerializeField] private Image _antiFillImage;
        [SerializeField] private TMP_Text _antiValueLabel;
        [SerializeField] private int _antiMaxValue = 1000;
        [SerializeField] private RectTransform _antiHandle;

        public int FanMaxValue => _fanMaxValue;
        public int AntiMaxValue => _antiMaxValue;

        private static Sprite _fallbackWhiteSprite;
        private float _targetFanFill;
        private float _targetAntiFill;
        private float _currentFanFill;
        private float _currentAntiFill;

        private void Awake()
        {
            EnsureSpriteAssigned(_fanFillImage);
            EnsureSpriteAssigned(_antiFillImage);
        }

        private void Update()
        {
            if (_fanFillImage != null)
            {
                _currentFanFill = Mathf.Lerp(_currentFanFill, _targetFanFill, Time.deltaTime * 12f);
                ApplyFillHeight(_fanFillImage, _currentFanFill);
                UpdateHandlePosition(_fanHandle, _fanFillImage);
            }

            if (_antiFillImage != null)
            {
                _currentAntiFill = Mathf.Lerp(_currentAntiFill, _targetAntiFill, Time.deltaTime * 12f);
                ApplyFillHeight(_antiFillImage, _currentAntiFill);
                UpdateHandlePosition(_antiHandle, _antiFillImage);
            }
        }

        // Resize RectTransform theo chieu cao thay vi dung Image.fillAmount, vi Image kieu Filled
        // KHONG ho tro 9-slice (2 dau pill se bi keo meo) - cung logic da dung o ProgressBarController.
        private static void ApplyFillHeight(Image fillImage, float ratio)
        {
            var fillRect = fillImage.rectTransform;
            float trackHeight = ((RectTransform)fillRect.parent).rect.height;
            fillRect.sizeDelta = new Vector2(fillRect.sizeDelta.x, trackHeight * ratio);
        }

        // Glow nho bam theo dung mep tren cua fill hien tai - fill gio la RectTransform duoc
        // resize truc tiep (pivot day, gan bottom) nen rect.height chinh la vi tri can bam.
        private static void UpdateHandlePosition(RectTransform handle, Image fillImage)
        {
            if (handle == null) return;
            Vector2 pos = handle.anchoredPosition;
            pos.y = fillImage.rectTransform.rect.height;
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

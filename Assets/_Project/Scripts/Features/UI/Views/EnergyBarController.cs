using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI.Views
{
    // View: chỉ hiển thị thanh năng lượng, không truy cập module khác (SRP).
    public class EnergyBarController : MonoBehaviour
    {
        [SerializeField] private Image fillBar;

        private void Awake()
        {
            SetEnergy(1f);
        }

        // currentEnergy: giá trị đã chuẩn hoá 0..1; phía gọi (module Like/Energy) tự chuẩn hoá trước khi truyền vào.
        public void SetEnergy(float currentEnergy)
        {
            if (fillBar != null)
            {
                float clamped = Mathf.Clamp01(currentEnergy);
                fillBar.enabled = clamped > 0.0001f;

                // Resize theo chiều rộng thay vì dùng fillAmount, vì Image kiểu Filled
                // không hỗ trợ 9-slice (2 đầu bo tròn sẽ bị kéo méo).
                var fillRect = fillBar.rectTransform;
                float fullWidth = ((RectTransform)fillRect.parent).rect.width;
                if (fullWidth <= 0f)
                {
                    fullWidth = 1920f;
                }
                fillRect.sizeDelta = new Vector2(fullWidth * clamped, fillRect.sizeDelta.y);
            }
        }
    }
}

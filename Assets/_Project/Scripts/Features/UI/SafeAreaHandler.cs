using UnityEngine;

namespace SteamRush.Features.UI
{
    // Co giãn RectTransform theo Screen.safeArea (né notch/tai thỏ của máy), đồng thời chừa thêm
    // khoảng trống thủ công ở đáy màn hình cho khung chat bình luận TikTok đè lên - phần này
    // KHÔNG nằm trong Screen.safeArea vì đó là overlay riêng của app TikTok, hệ điều hành không
    // biết để tính vào safe area. Gắn script này lên 1 container rỗng làm cha của các phần tử HUD
    // cần né vùng chat (vd. ChatInputField, các nút/khung ở nửa dưới màn hình).
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaHandler : MonoBehaviour
    {
        [Tooltip("Khoảng trống thêm ở đáy màn hình (tỉ lệ 0..1 theo chiều cao) để né khung chat TikTok.")]
        [SerializeField, Range(0f, 0.5f)] private float _extraBottomRatio = 0.12f;

        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            // Safe area có thể đổi khi xoay máy hoặc thay đổi độ phân giải - so cache trước khi tính lại cho rẻ.
            if (Screen.safeArea != _lastSafeArea || new Vector2Int(Screen.width, Screen.height) != _lastScreenSize)
            {
                Apply();
            }
        }

        private void Apply()
        {
            Rect safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // Chừa thêm đáy cho khung chat TikTok - nằm ngoài phạm vi Screen.safeArea của hệ điều hành.
            anchorMin.y = Mathf.Clamp01(anchorMin.y + _extraBottomRatio);

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
        }
    }
}

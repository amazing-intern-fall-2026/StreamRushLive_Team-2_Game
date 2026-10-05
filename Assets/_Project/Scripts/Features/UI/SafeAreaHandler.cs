using UnityEngine;

namespace SteamRush.Features.UI
{
    // Adapts RectTransform to Screen.safeArea (camera cutouts/notches) with additional bottom margin
    // for TikTok live stream chat overlay.
    // ExecuteAlways: chay ca trong Edit Mode de Scene/Game view preview dung vi tri thuc te
    // (khong phai doi vao Play Mode moi thay dung, tranh nham lam gia dinh vi tri sai khi chinh UI).
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaHandler : MonoBehaviour
    {
        [Tooltip("Additional bottom offset (normalized 0..1 of screen height) to accommodate TikTok chat overlay.")]
        [SerializeField, Range(0f, 0.5f)] private float _extraBottomRatio = 0.12f;

        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void OnEnable()
        {
            _rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            // Recompute safe area when screen orientation or resolution changes
            if (Screen.safeArea != _lastSafeArea || new Vector2Int(Screen.width, Screen.height) != _lastScreenSize)
            {
                Apply();
            }
        }

        private void Apply()
        {
            // ExecuteAlways co the goi Apply() vao dung luc Screen.width/height con la 0 (vd. khung
            // hinh dau tien luc vua vao Play Mode, hoac Editor chua kip resize Game view) - chia cho
            // 0 se ghi NaN vinh vien vao anchorMin/anchorMax, lam ca RectTransform lan cac con ben
            // duoi (ChatInputField, QuickHelpBar) bien mat het. Bo qua lan goi do, cho lan Update() ke
            // tiep khi Screen co kich thuoc hop le.
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // Add bottom clearance for TikTok chat overlay
            anchorMin.y = Mathf.Clamp01(anchorMin.y + _extraBottomRatio);

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
        }
    }
}

using UnityEngine;

namespace SteamRush.Features.UI
{
    // Adapts RectTransform to Screen.safeArea (camera cutouts/notches) with additional bottom margin
    // for TikTok live stream chat overlay.
    // ExecuteAlways: active in Edit Mode so Scene/Game view previews layout accurately.
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
            // Guard against zero dimensions on initial editor/play mode frame to avoid NaN anchors
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

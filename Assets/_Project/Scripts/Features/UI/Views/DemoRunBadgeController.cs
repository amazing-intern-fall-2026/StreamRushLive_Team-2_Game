using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Visual controller for the bottom-left "RUN DEMO" indicator watermark.
    /// Pulses subtly when active in Test/Demo mode.
    /// </summary>
    public class DemoRunBadgeController : MonoBehaviour
    {
        [SerializeField] private Image _dotImage;
        [SerializeField] private TextMeshProUGUI _textMesh;

        public void Setup(Image dot, TextMeshProUGUI text)
        {
            _dotImage = dot;
            _textMesh = text;
        }

        private void OnEnable()
        {
            if (_dotImage != null)
            {
                var c = _dotImage.color;
                c.a = 1f;
                _dotImage.color = c;
            }
        }

        private void Update()
        {
            if (_dotImage != null)
            {
                // Smooth breathing pulse for the indicator dot
                float alpha = 0.60f + 0.40f * Mathf.Sin(Time.unscaledTime * 3.5f);
                var c = _dotImage.color;
                c.a = alpha;
                _dotImage.color = c;
            }
        }
    }
}

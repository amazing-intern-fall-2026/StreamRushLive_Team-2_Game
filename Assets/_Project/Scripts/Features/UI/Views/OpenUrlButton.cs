using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Reliable URL opener component for UI Buttons.
    /// Serializes URL into scene YAML and binds listener persistently in Awake/OnEnable,
    /// preventing lost delegates from Editor-time AddListener calls.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class OpenUrlButton : MonoBehaviour
    {
        [SerializeField] private string _targetUrl = "https://www.eulerstream.com/";
        private Button _button;

        public string TargetUrl
        {
            get => _targetUrl;
            set => _targetUrl = value;
        }

        public void Setup(string url)
        {
            _targetUrl = url;
            BindListener();
        }

        private void Awake()
        {
            BindListener();
        }

        private void OnEnable()
        {
            BindListener();
        }

        private void BindListener()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.RemoveListener(OpenUrl);
                _button.onClick.AddListener(OpenUrl);
            }
        }

        public void OpenUrl()
        {
            if (!string.IsNullOrEmpty(_targetUrl))
            {
                Debug.Log($"<color=#00E5FF>[OpenUrlButton] Opening URL: {_targetUrl}</color>");
                Application.OpenURL(_targetUrl);
            }
            else
            {
                Debug.LogWarning("[OpenUrlButton] Target URL is null or empty!");
            }
        }
    }
}

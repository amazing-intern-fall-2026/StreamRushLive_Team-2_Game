using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Reliable reset button component for TMP_InputField.
    /// Preserves reset callback persistently without relying on Editor-time UnityEvent delegates.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class InputFieldResetButton : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _targetInput;
        [SerializeField] private string _defaultValue;
        private Button _button;

        public void Setup(TMP_InputField targetInput, string defaultValue)
        {
            _targetInput = targetInput;
            _defaultValue = defaultValue;
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
                _button.onClick.RemoveListener(ResetValue);
                _button.onClick.AddListener(ResetValue);
            }
        }

        public void ResetValue()
        {
            if (_targetInput != null)
            {
                _targetInput.text = _defaultValue;
            }
        }
    }
}

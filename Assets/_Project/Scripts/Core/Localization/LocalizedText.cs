using UnityEngine;
using TMPro;

namespace SteamRush.Core.Localization
{
    /// <summary>
    /// Automatically updates a TMP_Text component based on LocalizationManager key.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string _localizationKey;
        [SerializeField] private bool _useUppercase = false;

        private TMP_Text _textComponent;

        public string LocalizationKey
        {
            get => _localizationKey;
            set
            {
                _localizationKey = value;
                Refresh();
            }
        }

        private void Awake()
        {
            _textComponent = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
            Refresh();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(AppLanguage lang)
        {
            Refresh();
        }

        public void Refresh()
        {
            if (_textComponent == null) _textComponent = GetComponent<TMP_Text>();
            if (_textComponent == null || string.IsNullOrEmpty(_localizationKey)) return;

            if (LocalizationManager.Instance != null)
            {
                string localized = LocalizationManager.Instance.Get(_localizationKey, _textComponent.text);
                _textComponent.text = _useUppercase ? localized.ToUpper() : localized;
            }
        }
    }
}

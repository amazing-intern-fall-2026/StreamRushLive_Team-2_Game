using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Swaps toggle background sprite between ON and OFF sprites reactively.
    /// Resilient to runtime listeners being wiped or programmatically altered.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    [DisallowMultipleComponent]
    public class ToggleSpriteSwapper : MonoBehaviour
    {
        [SerializeField] private Toggle _toggle;
        [SerializeField] private Image _targetImage;
        [SerializeField] private Sprite _onSprite;
        [SerializeField] private Sprite _offSprite;

        private bool _lastState;
        private bool _isInitialized;

        public void Setup(Sprite onSprite, Sprite offSprite, Image targetImage = null)
        {
            _onSprite = onSprite;
            _offSprite = offSprite;
            _toggle = GetComponent<Toggle>();
            _targetImage = targetImage != null ? targetImage : GetComponent<Image>();
            _isInitialized = true;
            if (_toggle != null)
            {
                _lastState = _toggle.isOn;
                SyncVisual();
            }
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            if (_toggle != null)
            {
                _lastState = _toggle.isOn;
                SyncVisual();
            }
        }

        private void Update()
        {
            if (_toggle != null && (!_isInitialized || _toggle.isOn != _lastState))
            {
                _isInitialized = true;
                _lastState = _toggle.isOn;
                SyncVisual();
            }
        }

        private void EnsureReferences()
        {
            if (_toggle == null) _toggle = GetComponent<Toggle>();
            if (_targetImage == null) _targetImage = GetComponent<Image>();

            // Auto fallback load sprites if missing
            if (_onSprite == null)
            {
                _onSprite = Resources.Load<Sprite>("Toggle_ON");
#if UNITY_EDITOR
                if (_onSprite == null)
                {
                    _onSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hyper_Casual_UI/Sprites/Toggle/Toggle_ON.png");
                }
#endif
            }

            if (_offSprite == null)
            {
                _offSprite = Resources.Load<Sprite>("Toggle_Off");
#if UNITY_EDITOR
                if (_offSprite == null)
                {
                    _offSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hyper_Casual_UI/Sprites/Toggle/Toggle_Off.png");
                }
#endif
            }
        }

        public void SyncVisual()
        {
            EnsureReferences();
            if (_targetImage == null || _toggle == null) return;

            Sprite targetSprite = _toggle.isOn ? _onSprite : _offSprite;
            if (targetSprite != null && _targetImage.sprite != targetSprite)
            {
                _targetImage.sprite = targetSprite;
            }
        }
    }
}

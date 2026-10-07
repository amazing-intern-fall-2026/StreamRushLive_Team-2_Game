using UnityEngine;
using UnityEngine.UI;
using SteamRush.Features.UI.PreGameConfig;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Controls the HUD Settings (Gear) button.
    /// Toggles the PreGameConfig modal during gameplay or pre-game.
    /// Supports button click and keyboard shortcut (Escape).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PreGameConfigButtonController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button _button;

        [Header("Keyboard Controls")]
        [SerializeField] private bool _enableHotKey = true;
        [SerializeField] private KeyCode _hotKey = KeyCode.Escape;

        private void Awake()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnButtonClicked);
                _button.onClick.AddListener(OnButtonClicked);
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnButtonClicked);
            }
        }

        private void Update()
        {
            if (!_enableHotKey) return;

            // Don't trigger hotkey if user is actively typing in an input field
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
            {
                var sel = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
                if (sel.GetComponent<TMPro.TMP_InputField>() != null || sel.GetComponent<UnityEngine.UI.InputField>() != null)
                {
                    return;
                }
            }

            bool keyPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (_hotKey == KeyCode.Escape && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    keyPressed = true;
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(_hotKey))
            {
                keyPressed = true;
            }
#endif
            if (keyPressed)
            {
                OnButtonClicked();
            }
        }

        public void OnButtonClicked()
        {
            var mgr = PreGameConfigManager.Instance;
            if (mgr != null)
            {
                mgr.ToggleConfigUI();
            }
        }
    }
}

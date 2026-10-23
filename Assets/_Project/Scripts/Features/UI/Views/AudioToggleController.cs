using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SteamRush.Features.UI;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Controls the HUD Game Audio toggle button.
    /// Toggles Mute / Unmute when clicked or via 'M' key shortcut.
    /// Provides clear visual feedback on the button icon, color, and outline glow.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class AudioToggleController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button _button;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _bgImage;
        [SerializeField] private Outline _outline;
        [SerializeField] private TextMeshProUGUI _labelText;

        [Header("Sprites")]
        [SerializeField] private Sprite _soundOnSprite;
        [SerializeField] private Sprite _soundOffSprite;

        [Header("Visual Feedback Colors")]
        [SerializeField] private Color _iconOnColor = Color.white;
        [SerializeField] private Color _iconOffColor = new Color(1f, 0.45f, 0.45f, 1f);

        [SerializeField] private Color _bgOnColor = new Color(0.08f, 0.22f, 0.32f, 0.95f);
        [SerializeField] private Color _bgOffColor = new Color(0.28f, 0.10f, 0.12f, 0.95f);

        [SerializeField] private Color _outlineOnColor = new Color(0.0f, 0.85f, 1.0f, 0.75f);
        [SerializeField] private Color _outlineOffColor = new Color(1.0f, 0.35f, 0.2f, 0.85f);

#pragma warning disable CS0414
        [Header("Keyboard Controls")]
        [SerializeField] private KeyCode _shortcutKey = KeyCode.M;
#pragma warning restore CS0414

        [Header("HUD Feedback")]
        [SerializeField] private bool _showStatusPopup = true;

        private bool _isMuted = false;

        public bool IsMuted => _isMuted;

        public float CurrentVolume
        {
            get => AudioManager.Instance != null ? AudioManager.Instance.MasterVolume : AudioListener.volume;
            set => SetVolume(value, true);
        }

        private void Awake()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_bgImage == null) _bgImage = GetComponent<Image>();
            if (_outline == null) _outline = GetComponent<Outline>();

            // Ensure any legacy mini volume popup child is cleaned up
            var legacyPopup = transform.Find("MiniAudioVolumePopup");
            if (legacyPopup != null)
            {
                Destroy(legacyPopup.gameObject);
            }

            if (_button != null)
            {
                _button.onClick.RemoveListener(OnButtonClicked);
                _button.onClick.AddListener(OnButtonClicked);
            }
        }

        private void Start()
        {
            if (AudioManager.Instance != null)
            {
                _isMuted = AudioManager.Instance.IsMuted;
                AudioManager.Instance.OnMuteStateChanged += OnExternalMuteStateChanged;
                AudioManager.Instance.OnVolumeChanged += OnExternalVolumeChanged;
            }
            else
            {
                _isMuted = AudioListener.volume <= 0.001f || PlayerPrefs.GetInt("Game_Audio_Muted", 0) == 1;
            }

            UpdateVisuals(false);
        }

        private void OnDestroy()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.OnMuteStateChanged -= OnExternalMuteStateChanged;
                AudioManager.Instance.OnVolumeChanged -= OnExternalVolumeChanged;
            }

            if (_button != null)
            {
                _button.onClick.RemoveListener(OnButtonClicked);
            }
        }

        private void Update()
        {
            bool toggleKeyPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame)
            {
                toggleKeyPressed = true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(_shortcutKey))
            {
                toggleKeyPressed = true;
            }
#else
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame)
            {
                toggleKeyPressed = true;
            }
#endif

            if (toggleKeyPressed)
            {
                var currentObj = EventSystem.current?.currentSelectedGameObject;
                if (currentObj != null && (currentObj.GetComponent<TMP_InputField>() != null || currentObj.GetComponent<InputField>() != null))
                {
                    return;
                }

                ToggleAudio();
            }
        }

        private void OnButtonClicked()
        {
            ToggleAudio();
        }

        public void ToggleAudio()
        {
            SetMuted(!_isMuted, true);
        }

        public void SetMuted(bool muted, bool showToast = true)
        {
            _isMuted = muted;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMuted(_isMuted);
            }
            else
            {
                AudioListener.volume = _isMuted ? 0f : PlayerPrefs.GetFloat("Game_Audio_MasterVolume", 1f);
                PlayerPrefs.SetInt("Game_Audio_Muted", _isMuted ? 1 : 0);
                PlayerPrefs.Save();
            }

            UpdateVisuals(showToast);
        }

        public void SetVolume(float volume, bool showFeedback = true)
        {
            float clamped = Mathf.Clamp01(volume);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.MasterVolume = clamped;
            }
            else
            {
                if (!_isMuted) AudioListener.volume = clamped;
                PlayerPrefs.SetFloat("Game_Audio_MasterVolume", clamped);
                PlayerPrefs.Save();
            }

            UpdateVisuals(false);

            if (showFeedback && _showStatusPopup)
            {
                var hud = FindFirstObjectByType<HUDManager>();
                if (hud != null)
                {
                    int percent = Mathf.RoundToInt(clamped * 100f);
                    string msg = _isMuted ? "Game Audio: MUTED" : $"Game Audio: {percent}%";
                    hud.ShowStatusPopup(msg, !_isMuted);
                }
            }
        }

        private void OnExternalMuteStateChanged(bool muted)
        {
            if (_isMuted != muted)
            {
                _isMuted = muted;
                UpdateVisuals(false);
            }
        }

        private void OnExternalVolumeChanged(float volume)
        {
            UpdateVisuals(false);
        }

        private void UpdateVisuals(bool showToast)
        {
            // 1. Icon Sprite & Color
            if (_iconImage != null)
            {
                if (_isMuted)
                {
                    if (_soundOffSprite != null) _iconImage.sprite = _soundOffSprite;
                    _iconImage.color = _iconOffColor;
                }
                else
                {
                    if (_soundOnSprite != null) _iconImage.sprite = _soundOnSprite;
                    _iconImage.color = _iconOnColor;
                }
            }

            // 2. Button Background Color
            if (_bgImage != null)
            {
                _bgImage.color = _isMuted ? _bgOffColor : _bgOnColor;
            }

            // 3. Outline Glow Color
            if (_outline != null)
            {
                _outline.effectColor = _isMuted ? _outlineOffColor : _outlineOnColor;
            }

            // 4. Label Text (if present)
            if (_labelText != null)
            {
                _labelText.text = _isMuted ? "OFF" : "ON";
                _labelText.color = _isMuted ? _iconOffColor : _iconOnColor;
            }

            // 5. Toast / Status Popup Notification
            if (showToast && _showStatusPopup)
            {
                var hud = FindFirstObjectByType<HUDManager>();
                if (hud != null)
                {
                    string msg = _isMuted ? "Game Audio: MUTED" : "Game Audio: ON";
                    hud.ShowStatusPopup(msg, !_isMuted);
                }
            }
        }

#if UNITY_EDITOR
        public void ConfigureReferences(Image icon, Image bg, Outline outline, TextMeshProUGUI label, Sprite soundOn, Sprite soundOff)
        {
            _iconImage = icon;
            _bgImage = bg;
            _outline = outline;
            _labelText = label;
            _soundOnSprite = soundOn;
            _soundOffSprite = soundOff;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}

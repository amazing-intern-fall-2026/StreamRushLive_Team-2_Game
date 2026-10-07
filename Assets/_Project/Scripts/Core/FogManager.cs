using System.Collections;
using UnityEngine;

namespace SteamRush.Features.Environment
{
    /// <summary>
    /// Manages RenderSettings fog transitions between default and dense fog.
    /// </summary>
    public class FogManager : MonoBehaviour
    {
        public static FogManager Instance { get; private set; }

        [Header("Target Fog Settings (When Hazard Fog is active)")]
        [Tooltip("Fog start distance when activated (default: -3.5)")]
        [SerializeField] private float _targetStartDistance = -3.5f;

        [Tooltip("Fog end distance when activated")]
        [SerializeField] private float _targetEndDistance = 35f;

        [Header("Default Settings (Auto-cached on Awake)")]
        [Tooltip("Automatically reads Lighting Settings fog values on Awake")]
        [SerializeField] private bool _autoCacheOnAwake = true;

        [SerializeField] private bool _defaultFogEnabled = true;
        [SerializeField] private FogMode _defaultFogMode = FogMode.Linear;
        [SerializeField] private float _defaultStartDistance = 45f;
        [SerializeField] private float _defaultEndDistance = 115f;
        [SerializeField] private Color _defaultFogColor = new Color(0.867f, 0.906f, 0.949f, 1f); // #DDE7F2

        [Header("Transition Settings")]
        [Tooltip("Fog transition duration in seconds")]
        [SerializeField] private float _transitionDuration = 1.0f;

        private Coroutine _fadeCoroutine;

        public float DefaultStartDistance => _defaultStartDistance;
        public float TargetStartDistance => _targetStartDistance;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (_autoCacheOnAwake)
            {
                CacheCurrentFogSettings();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                // Restore default fog settings on disable/scene exit
                ResetFog();
                Instance = null;
            }
        }

        /// <summary>
        /// Saves current RenderSettings fog configuration as default values.
        /// </summary>
        [ContextMenu("Cache Current Lighting Fog")]
        public void CacheCurrentFogSettings()
        {
            _defaultFogEnabled = RenderSettings.fog;
            _defaultFogMode = RenderSettings.fogMode;
            _defaultStartDistance = RenderSettings.fogStartDistance;
            _defaultEndDistance = RenderSettings.fogEndDistance;
            _defaultFogColor = RenderSettings.fogColor;
        }

        /// <summary>
        /// Sets fog start distance immediately.
        /// </summary>
        /// <param name="startDistance">Target fog start distance (default: -3.5f)</param>
        [ContextMenu("Set Fog Start (-3.5)")]
        public void SetFogStart(float startDistance = -3.5f)
        {
            StopCurrentTransition();

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = startDistance;

            Debug.Log($"[FogManager] Fog Start set to {startDistance} (End = {RenderSettings.fogEndDistance})");
        }

        /// <summary>
        /// Restores fog to initial settings immediately.
        /// </summary>
        [ContextMenu("Reset Fog To Default")]
        public void ResetFog()
        {
            StopCurrentTransition();

            RenderSettings.fog = _defaultFogEnabled;
            RenderSettings.fogMode = _defaultFogMode;
            RenderSettings.fogStartDistance = _defaultStartDistance;
            RenderSettings.fogEndDistance = _defaultEndDistance;
            RenderSettings.fogColor = _defaultFogColor;

            Debug.Log($"[FogManager] Fog reset to defaults: Start = {_defaultStartDistance}, End = {_defaultEndDistance}");
        }

        /// <summary>
        /// Smoothly interpolates fog start distance to target over duration.
        /// </summary>
        public void SetFogStartSmooth(float targetStart = -3.5f, float duration = -1f)
        {
            float dur = duration > 0f ? duration : _transitionDuration;
            StartFadeTransition(targetStart, _targetEndDistance, dur);
        }

        /// <summary>
        /// Smoothly restores fog to initial settings over duration.
        /// </summary>
        public void ResetFogSmooth(float duration = -1f)
        {
            float dur = duration > 0f ? duration : _transitionDuration;
            StartFadeTransition(_defaultStartDistance, _defaultEndDistance, dur);
        }

        private void StartFadeTransition(float targetStart, float targetEnd, float duration)
        {
            StopCurrentTransition();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            _fadeCoroutine = StartCoroutine(FadeFogRoutine(targetStart, targetEnd, duration));
        }

        private void StopCurrentTransition()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
        }

        private IEnumerator FadeFogRoutine(float targetStart, float targetEnd, float duration)
        {
            float initialStart = RenderSettings.fogStartDistance;
            float initialEnd = RenderSettings.fogEndDistance;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Use SmoothStep for natural fog transition
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                RenderSettings.fogStartDistance = Mathf.Lerp(initialStart, targetStart, smoothT);
                RenderSettings.fogEndDistance = Mathf.Lerp(initialEnd, targetEnd, smoothT);
                yield return null;
            }

            RenderSettings.fogStartDistance = targetStart;
            RenderSettings.fogEndDistance = targetEnd;
            _fadeCoroutine = null;
        }
    }
}

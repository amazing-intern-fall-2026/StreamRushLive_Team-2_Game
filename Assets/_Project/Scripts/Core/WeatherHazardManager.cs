using System.Collections;
using UnityEngine;

namespace SteamRush.Features.Environment
{
    /// <summary>
    /// Manages weather hazard events (rain FX and dense fog).
    /// </summary>
    public class WeatherHazardManager : MonoBehaviour
    {
        public static WeatherHazardManager Instance { get; private set; }

        [Header("Target Effect Objects")]
        [Tooltip("FX_Rain GameObject in Hierarchy. If left empty, automatically finds 'FX_Rain'.")]
        [SerializeField] private GameObject _rainEffectObject;

        [Tooltip("FogManager reference. If left empty, automatically resolves in scene.")]
        [SerializeField] private FogManager _fogManager;

        [Header("Hazard Configuration")]
        [Tooltip("Weather hazard duration in seconds (default: 60s).")]
        [SerializeField] private float _hazardDuration = 60f;

        [Tooltip("Target Fog Start distance during hazard")]
        [SerializeField] private float _fogStartTarget = -3.5f;

        [Tooltip("Fog transition duration in seconds")]
        [SerializeField] private float _fogFadeDuration = 1.5f;

        private Coroutine _hazardRoutine;
        private bool _isHazardActive;

        public bool IsHazardActive => _isHazardActive;
        public float HazardDuration => _hazardDuration;

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

            FindReferencesIfMissing();
        }

        private void Start()
        {
            FindReferencesIfMissing();

            // Ensure FX_Rain is initially disabled
            if (_rainEffectObject != null && _rainEffectObject.activeSelf)
            {
                _rainEffectObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                StopWeatherHazardImmediate();
                Instance = null;
            }
        }

        /// <summary>
        /// Resolves FX_Rain and FogManager in Hierarchy if unassigned.
        /// </summary>
        public void FindReferencesIfMissing()
        {
            if (_fogManager == null)
            {
                _fogManager = FogManager.Instance ?? FindFirstObjectByType<FogManager>();
                if (_fogManager == null)
                {
                    // Auto-create FogManager if missing from scene
                    var fogObj = new GameObject("FogManager");
                    _fogManager = fogObj.AddComponent<FogManager>();
                }
            }

            if (_rainEffectObject == null)
            {
                // Search directly by GameObject name FX_Rain
                var found = GameObject.Find("FX_Rain");
                if (found != null)
                {
                    _rainEffectObject = found;
                }
                else
                {
                    // Case-insensitive fallback search for rain effect
                    var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var t in allTransforms)
                    {
                        if (t.name.Equals("FX_Rain", System.StringComparison.OrdinalIgnoreCase) ||
                            t.name.Contains("FX_Rain"))
                        {
                            _rainEffectObject = t.gameObject;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Activates rain weather hazard: enables FX_Rain and sets dense fog for duration.
        /// </summary>
        /// <param name="duration">Hazard duration in seconds (default: 60s)</param>
        [ContextMenu("Trigger Weather Hazard (60s)")]
        public void TriggerWeatherHazard(float duration = -1f)
        {
            float activeDuration = duration > 0f ? duration : _hazardDuration;

            FindReferencesIfMissing();

            if (_hazardRoutine != null)
            {
                StopCoroutine(_hazardRoutine);
            }

            _hazardRoutine = StartCoroutine(WeatherHazardRoutine(activeDuration));
        }

        /// <summary>
        /// Immediately stops weather hazard, disables rain, and restores fog.
        /// </summary>
        [ContextMenu("Stop Weather Hazard")]
        public void StopWeatherHazard()
        {
            if (_hazardRoutine != null)
            {
                StopCoroutine(_hazardRoutine);
                _hazardRoutine = null;
            }

            StopWeatherHazardImmediate();
        }

        private IEnumerator WeatherHazardRoutine(float duration)
        {
            _isHazardActive = true;
            Debug.Log($"[WeatherHazardManager] Active hazard: FX_Rain & Fog Start = {_fogStartTarget} for {duration}s.");

            if (_rainEffectObject != null)
            {
                _rainEffectObject.SetActive(true);
                var particles = _rainEffectObject.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particles)
                {
                    ps.Play();
                }
            }
            else
            {
                Debug.LogWarning("[WeatherHazardManager] GameObject 'FX_Rain' not found in Hierarchy!");
            }

            AudioManager.Instance?.PlayLoopSFX(SFXType.EnvironmentRain, 0.7f);

            if (_fogManager != null)
            {
                _fogManager.SetFogStartSmooth(_fogStartTarget, _fogFadeDuration);
            }

            yield return new WaitForSeconds(duration);

            Debug.Log("[WeatherHazardManager] Hazard duration finished. Resetting FX_Rain and fog.");

            if (_rainEffectObject != null)
            {
                var particles = _rainEffectObject.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particles)
                {
                    ps.Stop();
                }
                // Disable GameObject after 1.5s to let remaining rain particles finish
                StartCoroutine(DeactivateRainAfterDelay(1.5f));
            }

            if (_fogManager != null)
            {
                _fogManager.ResetFogSmooth(_fogFadeDuration);
            }

            AudioManager.Instance?.StopLoopSFX();

            _isHazardActive = false;
            _hazardRoutine = null;
        }

        private IEnumerator DeactivateRainAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!_isHazardActive && _rainEffectObject != null)
            {
                _rainEffectObject.SetActive(false);
            }
        }

        private void StopWeatherHazardImmediate()
        {
            _isHazardActive = false;
            AudioManager.Instance?.StopLoopSFX();

            if (_rainEffectObject != null)
            {
                var particles = _rainEffectObject.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particles)
                {
                    ps.Stop();
                }
                _rainEffectObject.SetActive(false);
            }

            if (_fogManager != null)
            {
                _fogManager.ResetFog();
            }
        }
    }
}

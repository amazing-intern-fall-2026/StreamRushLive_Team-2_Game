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
        [Tooltip("Đối tượng FX_Rain trong Hierarchy. Nếu để trống, script sẽ tự tìm đối tượng có tên 'FX_Rain'")]
        [SerializeField] private GameObject _rainEffectObject;

        [Tooltip("Tham chiếu tới FogManager. Nếu để trống, script sẽ tự tìm.")]
        [SerializeField] private FogManager _fogManager;

        [Header("Hazard Configuration")]
        [Tooltip("Thời gian duy trì thời tiết bất lợi (mặc định 60 giây)")]
        [SerializeField] private float _hazardDuration = 60f;

        [Tooltip("Khoảng cách Fog Start khi có sương mù")]
        [SerializeField] private float _fogStartTarget = -3.5f;

        [Tooltip("Thời gian chuyển mượt sương mù khi bắt đầu và kết thúc (giây)")]
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

            // Đảm bảo FX_Rain ban đầu đang tắt
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
        /// Tự động tìm kiếm FX_Rain và FogManager trong Hierarchy nếu chưa được gán qua Inspector.
        /// </summary>
        public void FindReferencesIfMissing()
        {
            if (_fogManager == null)
            {
                _fogManager = FogManager.Instance ?? FindFirstObjectByType<FogManager>();
                if (_fogManager == null)
                {
                    // Tự động tạo FogManager nếu chưa có trong scene
                    var fogObj = new GameObject("FogManager");
                    _fogManager = fogObj.AddComponent<FogManager>();
                }
            }

            if (_rainEffectObject == null)
            {
                // Tìm kiếm trực tiếp theo tên FX_Rain trong Hierarchy
                var found = GameObject.Find("FX_Rain");
                if (found != null)
                {
                    _rainEffectObject = found;
                }
                else
                {
                    // Tìm kiếm không phân biệt hoa thường hoặc chứa chữ Rain
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
        /// Kích hoạt Gift Thời Tiết Bất Lợi: Chạy FX_Rain và bật Fog Start (-3.5f) trong 60 giây.
        /// </summary>
        /// <param name="duration">Thời gian duy trì (mặc định 60s)</param>
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
        /// Dừng ngay lập tức hiệu ứng thời tiết, tắt mưa và khôi phục sương mù.
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
                // Tắt hẳn GameObject sau 1.5s để hạt mưa cũ rơi hết
                StartCoroutine(DeactivateRainAfterDelay(1.5f));
            }

            if (_fogManager != null)
            {
                _fogManager.ResetFogSmooth(_fogFadeDuration);
            }

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

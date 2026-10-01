using System.Collections;
using UnityEngine;

namespace SteamRush.Features.Environment
{
    /// <summary>
    /// Script chuyên quản lý sương mù (Fog) trong RenderSettings.
    /// Cho phép chuyển đổi khoảng cách Fog Start (-3.5f cho sương mù dày đặc) và khôi phục về mặc định.
    /// </summary>
    public class FogManager : MonoBehaviour
    {
        public static FogManager Instance { get; private set; }

        [Header("Target Fog Settings (Khi kích hoạt sương mù)")]
        [Tooltip("Khoảng cách bắt đầu sương mù khi kích hoạt (mặc định -3.5)")]
        [SerializeField] private float _targetStartDistance = -3.5f;

        [Tooltip("Khoảng cách kết thúc sương mù khi kích hoạt")]
        [SerializeField] private float _targetEndDistance = 35f;

        [Header("Default Settings (Tự động lưu khi bắt đầu game)")]
        [Tooltip("Tự động đọc các thông số Fog trong Lighting Settings khi Awake")]
        [SerializeField] private bool _autoCacheOnAwake = true;

        [SerializeField] private bool _defaultFogEnabled = true;
        [SerializeField] private FogMode _defaultFogMode = FogMode.Linear;
        [SerializeField] private float _defaultStartDistance = 45f;
        [SerializeField] private float _defaultEndDistance = 115f;
        [SerializeField] private Color _defaultFogColor = new Color(0.867f, 0.906f, 0.949f, 1f); // #DDE7F2

        [Header("Transition Settings")]
        [Tooltip("Thời gian chuyển đổi sương mù mượt mà (giây)")]
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
                // Đảm bảo khôi phục lại Fog ban đầu khi thoát scene hoặc kết thúc game
                ResetFog();
                Instance = null;
            }
        }

        /// <summary>
        /// Lưu lại cấu hình Fog hiện tại trong RenderSettings làm giá trị mặc định.
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
        /// Đặt Fog Start thành -3.5 (hoặc giá trị truyền vào) ngay lập tức.
        /// </summary>
        /// <param name="startDistance">Mặc định là -3.5f</param>
        [ContextMenu("Set Fog Start (-3.5)")]
        public void SetFogStart(float startDistance = -3.5f)
        {
            StopCurrentTransition();

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = startDistance;

            Debug.Log($"[FogManager] Đã set Fog Start = {startDistance} (End = {RenderSettings.fogEndDistance})");
        }

        /// <summary>
        /// Khôi phục Fog về lại trạng thái ban đầu ngay lập tức.
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

            Debug.Log($"[FogManager] Đã reset Fog về ban đầu: Start = {_defaultStartDistance}, End = {_defaultEndDistance}");
        }

        /// <summary>
        /// Chuyển đổi Fog Start mượt mà dần dần về -3.5f trong một khoảng thời gian.
        /// </summary>
        public void SetFogStartSmooth(float targetStart = -3.5f, float duration = -1f)
        {
            float dur = duration > 0f ? duration : _transitionDuration;
            StartFadeTransition(targetStart, _targetEndDistance, dur);
        }

        /// <summary>
        /// Khôi phục Fog về lại ban đầu mượt mà dần dần trong một khoảng thời gian.
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
                // Dùng SmoothStep để hiệu ứng sương chuyển động tự nhiên
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

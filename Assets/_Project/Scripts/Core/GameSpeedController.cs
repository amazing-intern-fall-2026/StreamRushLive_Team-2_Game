namespace SteamRush.Core
{
    using UnityEngine;

    /// <summary>
    /// Điều phối tốc độ cho toàn bộ thế giới đang cuộn (Track, Background, tốc độ spawn vật cản...).
    /// Tăng dần theo thời gian (difficulty ramp); khi Runner va chạm vật cản, gọi TriggerRecovery()
    /// để tốc độ tụt về 0 rồi tăng dần trở lại.
    /// </summary>
    public class GameSpeedController : MonoBehaviour
    {
        public static GameSpeedController Instance { get; private set; }

        [Header("Base Speed")]
        [SerializeField] private float _baseSpeed = 8f;

        [Header("Speed Ramp")]
        [Tooltip("Reference benchmark time in seconds (e.g. 60 = 1 minute).")]
        [SerializeField] private float _rampReferenceSeconds = 60f;
        [Tooltip("Target speed multiplier at benchmark time (e.g. 1.2 = +20%).")]
        [SerializeField] private float _rampReferenceMultiplier = 1.2f;

        [Header("Impact Recovery")]
        [Tooltip("Duration in seconds to recover speed back to normal after impact.")]
        [SerializeField] private float _recoveryDuration = 1.5f;

        private float _rampIncreasePerSecond;
        private float _elapsedTime;
        private float _recoveryTimer;

        public float CurrentSpeed { get; private set; }

        private void Awake()
        {
            Instance = this;
            _rampIncreasePerSecond = (_rampReferenceMultiplier - 1f) / Mathf.Max(0.01f, _rampReferenceSeconds);
            CurrentSpeed = _baseSpeed;
        }

        private void Update()
        {
            _elapsedTime += Time.deltaTime;
            float targetSpeed = _baseSpeed * (1f + _rampIncreasePerSecond * _elapsedTime);

            if (_recoveryTimer > 0f)
            {
                _recoveryTimer -= Time.deltaTime;
                float recoveryProgress = 1f - Mathf.Clamp01(_recoveryTimer / _recoveryDuration);
                CurrentSpeed = Mathf.Lerp(0f, targetSpeed, recoveryProgress);
            }
            else
            {
                CurrentSpeed = targetSpeed;
            }
        }

        /// <summary>Gọi khi Runner va chạm vật cản: tốc độ tụt về 0 rồi hồi phục dần trở lại.</summary>
        public void TriggerRecovery()
        {
            _recoveryTimer = _recoveryDuration;
        }
    }
}
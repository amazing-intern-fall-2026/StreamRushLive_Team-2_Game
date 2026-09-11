namespace SteamRush.Core
{
    using UnityEngine;

    ///  tốc độ cho toàn bộ thế giới đang cuộn (Track, Background, tốc độ spawn
    /// vật cản...). Tăng dần theo thời gian (difficulty ramp); khi Runner va chạm vật cản, gọi
    /// TriggerRecovery() để tốc độ tụt về 0 rồi tăng dần trở lại — việc "đóng băng khung hình"
    /// thật sự nên xử lý bằng Time.timeScale ở nơi khác (RunnerCollisionHandler), vì lúc đó
    /// Time.deltaTime tự động = 0 nên class này tự động "đứng hình" theo mà không cần thêm
    /// logic riêng — giữ đúng nguyên tắc Single Responsibility.
    
    public class GameSpeedController : MonoBehaviour
    {
        public static GameSpeedController Instance { get; private set; }

        [Header("Tốc độ nền")]
        [SerializeField] private float _baseSpeed = 8f;

        [Header("Tăng tốc dần theo thời gian")]
        [Tooltip("Mốc thời gian tham chiếu (giây), ví dụ 60 = 1 phút.")]
        [SerializeField] private float _rampReferenceSeconds = 60f;
        [Tooltip("Hệ số tốc độ muốn đạt tại mốc tham chiếu, ví dụ 1.2 = tăng 20%. Tiếp tục tăng đều mãi sau đó.")]
        [SerializeField] private float _rampReferenceMultiplier = 1.2f;

        [Header("Hồi phục sau va chạm")]
        [Tooltip("Thời gian (giây) để tốc độ hồi phục dần từ 0 về mức bình thường sau khi bị va chạm.")]
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
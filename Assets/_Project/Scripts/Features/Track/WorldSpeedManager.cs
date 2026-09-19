using System;
using UnityEngine;

namespace SteamRush.Track
{
    /// <summary>
    /// Điều phối tốc độ cuộn của TOÀN BỘ thế giới (Track, Background, Spawner, MovingWorldObject...).
    /// Runner đứng cố định tại X = 9.55; mọi cảm giác "di chuyển" đến từ việc thay đổi CurrentSpeed.
    ///   - Singleton Instance, Speed Ramp tăng dần theo thời gian, Recovery curve khi va chạm.
    /// Triển khai Controls & World Speed Mechanics + Collision Pipeline:
    ///   - Slide & Active World Deceleration (nhấp nhả / đè giữ Ctrl-S-↓)
    ///   - Active Sprint (giữ Shift/E, tiêu hao Energy)
    ///   - Khi va chạm: giảm về 0 (0.3-0.5s) -> giữ 0 (thời gian ngã tuỳ obstacle)
    ///     -> tăng mượt lại 10.0 m/s
    /// </summary>
    public class WorldSpeedManager : MonoBehaviour
    {
        public static WorldSpeedManager Instance { get; private set; }

        // ============================================================
        // BASE SPEED / RAMP
        // ============================================================
        [Header("Base Speed")]
        [Tooltip("Tốc độ cuộn mặc định khi không Slide / không Sprint. GDD v1.2 = 10.0 m/s.")]
        [SerializeField] private float _baseSpeed = 10f;

        [Header("Speed Ramp")]
        [Tooltip("Reference benchmark time in seconds (e.g. 60 = 1 minute).")]
        [SerializeField] private float _rampReferenceSeconds = 60f;
        [Tooltip("Target speed multiplier at benchmark time (e.g. 1.2 = +20%).")]
        [SerializeField] private float _rampReferenceMultiplier = 1.2f;

        // ============================================================
        // SLIDE / ACTIVE WORLD DECELERATION 
        // ============================================================
        [Header("Slide - Tap")]
        [Tooltip("Thời lượng hiệu ứng khi NHẤP NHẢ (giây). GDD v1.2 = 0.8s.")]
        [SerializeField] private float _slideTapDuration = 0.8f;
        [Tooltip("Tỉ lệ tốc độ còn lại khi Tap (0.5 = giảm 50%, 10.0 -> 5.0 m/s theo GDD).")]
        [SerializeField, Range(0f, 1f)] private float _slideTapSpeedMultiplier = 0.5f;

        [Header("Speed Transition")]
        [Tooltip("Tốc độ tăng mượt khi quay lại bình thường (nhả Slide, hết Tap, ramp bình thường).")]
        [SerializeField] private float _normalAccelRate = 8f;

        // ============================================================
        // SPRINT 
        // ============================================================
        [Header("Sprint")]
        [Tooltip("Tốc độ tối đa khi Sprint. GDD v1.2 = 18.0 m/s.")]
        [SerializeField] private float _sprintMaxSpeed = 18f;
        [Tooltip("Gia tốc Sprint = _normalAccelRate * hệ số này (GDD = 2x).")]
        [SerializeField] private float _sprintAccelMultiplier = 2f;
        [Tooltip("Năng lượng tiêu hao mỗi giây khi Sprint. GDD = 3.0 Energy/s.")]
        [SerializeField] private float _sprintEnergyDrainPerSecond = 3f;

        // ============================================================
        // COLLISION RECOVERY & REVERSE KNOCKBACK
        // ============================================================
        [Header("Collision Recovery & Knockback")]
        [Tooltip("Thời gian giảm về 0 m/s khi không có knockback distance (giây). GDD v1.2 = 0.3-0.5s.")]
        [SerializeField] private float _recoveryDecelDuration = 0.4f;
        [Tooltip("Thời gian hồi phục mặc định nếu obstacle không truyền riêng (giây).")]
        [SerializeField] private float _defaultRecoveryTotalDuration = 1.2f;
        [Tooltip("Thời lượng cơ sở cho xung cuộn ngược thế giới khi bị đẩy lùi (giây).")]
        [SerializeField] private float _baseKnockbackDuration = 0.38f;

        // ============================================================
        // STATE
        // ============================================================
        private enum SpeedState { Normal, SlideTap, SlideHold, Sprint, Recovery }
        private SpeedState _state = SpeedState.Normal;

        private float _rampIncreasePerSecond;
        private float _elapsedTime;
        private float _slideTapTimer;

        // Recovery & Knockback state
        private float _recoveryElapsed;
        private float _recoveryTotalDuration;
        private float _recoverySpeedAtImpact;
        private float _recoveryKnockbackDistance;
        private float _recoveryKnockbackDuration;

        /// <summary>
        /// Tốc độ cuộn hiện tại của thế giới. Giữ public set để tương thích ngược với
        /// TrackTileLooper.WorldSpeed (setter cũ) — KHÔNG tự ý gán từ bên ngoài, hãy dùng
        /// các hàm state (BeginSlideTap/HoldSlide/.../TriggerRecovery) thay vì set trực tiếp.
        /// </summary>
        public float CurrentSpeed { get; set; }
        public float BaseSpeed => _baseSpeed;

        public bool IsSliding => _state == SpeedState.SlideTap || _state == SpeedState.SlideHold;
        public bool IsSprinting => _state == SpeedState.Sprint;
        public bool IsRecovering => _state == SpeedState.Recovery;

        /// <summary>
        /// EnergySystem cần set giá trị này mỗi frame (0-1) trong Update() của nó,
        /// TRƯỚC khi WorldSpeedManager.Update() chạy (hoặc set ngay khi Energy thay đổi).
        /// </summary>
        public float CurrentEnergyPercent01 { get; set; } = 1f;

        /// <summary>Bắn ra mỗi frame khi đang Sprint để EnergySystem trừ năng lượng tương ứng.</summary>
        public event Action<float> OnSprintEnergyConsumed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _rampIncreasePerSecond = (_rampReferenceMultiplier - 1f) / Mathf.Max(0.01f, _rampReferenceSeconds);
            CurrentSpeed = _baseSpeed;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            _elapsedTime += dt;
            float normalTargetSpeed = _baseSpeed * (1f + _rampIncreasePerSecond * _elapsedTime);

            switch (_state)
            {
                case SpeedState.SlideTap:
                    UpdateSlideTap(dt, normalTargetSpeed);
                    break;

                case SpeedState.SlideHold:
                    // Đã loại bỏ chức năng trượt giảm dần tới khi dừng — duy trì tốc độ chạy bình thường
                    CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, normalTargetSpeed, _normalAccelRate * dt);
                    break;

                case SpeedState.Sprint:
                    UpdateSprint(dt);
                    break;

                case SpeedState.Recovery:
                    UpdateRecovery(dt, normalTargetSpeed);
                    break;

                case SpeedState.Normal:
                default:
                    CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, normalTargetSpeed, _normalAccelRate * dt);
                    break;
            }
        }

        private void UpdateSlideTap(float dt, float normalTargetSpeed)
        {
            _slideTapTimer -= dt;
            if (_slideTapTimer <= 0f)
            {
                _state = SpeedState.Normal;
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, normalTargetSpeed, _normalAccelRate * dt);
            }
            // Trong lúc Tap, tốc độ đã set tức thì lúc bấm (xem BeginSlideTap), giữ nguyên.
        }

        private void UpdateSprint(float dt)
        {
            // Hết năng lượng -> tự hủy Sprint, quay lại Normal.
            if (CurrentEnergyPercent01 <= 0f)
            {
                _state = SpeedState.Normal;
                return;
            }

            float sprintAccel = _normalAccelRate * _sprintAccelMultiplier;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, _sprintMaxSpeed, sprintAccel * dt);

            float energyCost = _sprintEnergyDrainPerSecond * dt;
            OnSprintEnergyConsumed?.Invoke(energyCost);
        }

        /// <summary>
        /// Xử lý hồi phục và đẩy lùi thế giới:
        /// - Nếu có knockbackDistance > 0: Thế giới cuộn NGƯỢC CHIỀU (CurrentSpeed < 0) theo xung Sine
        ///   trong _recoveryKnockbackDuration, đẩy lùi vật thể và đường chạy sang phải (+X) tạo cảm giác Runner bị giật lùi.
        /// - Sau đó: Giữ nguyên 0 m/s (Runner đang choáng/ngã).
        /// - Cuối cùng: Tăng mượt trở lại tốc độ chuẩn bằng _normalAccelRate rồi thoát Recovery.
        /// </summary>
        private void UpdateRecovery(float dt, float normalTargetSpeed)
        {
            _recoveryElapsed += dt;

            if (_recoveryKnockbackDistance > 0f && _recoveryElapsed < _recoveryKnockbackDuration)
            {
                // Pha 1: Cuộn thế giới NGƯỢC CHIỀU (Reverse Scroll) theo xung Sine mượt
                // Tích phân xung: V_peak * (2 * T / PI) = distance => V_peak = (PI * distance) / (2 * T)
                float progress = Mathf.Clamp01(_recoveryElapsed / _recoveryKnockbackDuration);
                float peakReverseSpeed = (Mathf.PI * _recoveryKnockbackDistance) / (2f * _recoveryKnockbackDuration);

                // Vận tốc âm -> Thế giới cuộn sang phải (+X), Runner đứng yên có cảm giác bị văng/đẩy lùi về phía sau
                CurrentSpeed = -peakReverseSpeed * Mathf.Sin(progress * Mathf.PI);
            }
            else if (_recoveryElapsed < _recoveryTotalDuration)
            {
                // Pha 2: Dừng tại chỗ (0 m/s) trong lúc Runner hồi phục sau khi bị đẩy lùi
                if (_recoveryKnockbackDistance <= 0f && _recoveryElapsed < _recoveryDecelDuration)
                {
                    float decelProgress = Mathf.Clamp01(_recoveryElapsed / _recoveryDecelDuration);
                    CurrentSpeed = Mathf.Lerp(_recoverySpeedAtImpact, 0f, decelProgress);
                }
                else
                {
                    CurrentSpeed = 0f;
                }
            }
            else
            {
                // Pha 3: Tăng tốc mượt mà trở lại tốc độ chạy chuẩn
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, normalTargetSpeed, _normalAccelRate * dt);
                if (Mathf.Approximately(CurrentSpeed, normalTargetSpeed))
                {
                    _recoveryKnockbackDistance = 0f;
                    _state = SpeedState.Normal;
                }
            }
        }

        // ============================================================
        // PUBLIC INPUT API — gọi từ RunnerInputHandler
        // ============================================================

        /// <summary>Gọi khi người chơi NHẤP NHẢ phím Slide (Ctrl / S / ↓).</summary>
        public void BeginSlideTap()
        {
            if (_state == SpeedState.SlideHold || _state == SpeedState.Recovery) return;

            _state = SpeedState.SlideTap;
            _slideTapTimer = _slideTapDuration;
            CurrentSpeed *= _slideTapSpeedMultiplier; // giảm tức thì -50%
        }

        /// <summary>Đã loại bỏ chức năng trượt giảm dần tới khi dừng khi đè giữ phím trượt.</summary>
        public void HoldSlide()
        {
            // Không hãm tốc độ về 0
        }

        /// <summary>Gọi khi người chơi NHẢ phím Slide sau khi đã ở trạng thái Hold.</summary>
        public void ReleaseSlide()
        {
            if (_state == SpeedState.SlideHold)
            {
                _state = SpeedState.Normal; // Update() sẽ MoveTowards mượt về tốc độ bình thường
            }
            // Nếu đang SlideTap, để nó tự hết theo _slideTapTimer, không cắt ngang.
        }

        /// <summary>Gọi mỗi frame khi người chơi GIỮ Shift/E.</summary>
        public void HoldSprint()
        {
            if (IsSliding || _state == SpeedState.Recovery) return; // Slide/Recovery ưu tiên hơn
            if (CurrentEnergyPercent01 <= 0f) return; // khóa khi hết năng lượng (0%)

            _state = SpeedState.Sprint;
        }

        /// <summary>Gọi khi người chơi NHẢ Shift/E.</summary>
        public void ReleaseSprint()
        {
            if (_state == SpeedState.Sprint)
            {
                _state = SpeedState.Normal;
            }
        }

        /// <summary>
        /// Gọi khi Runner va chạm vật cản:
        /// - Nếu có knockbackDistance: Thế giới giật cuộn ngược lại đẩy lùi Runner về phía sau.
        /// - Sau đó giữ 0 trong lúc Runner đang ngã, rồi tăng mượt trở lại tốc độ chuẩn.
        /// </summary>
        /// <param name="totalRecoveryDuration">Tổng thời gian hồi phục (giây).</param>
        /// <param name="knockbackDistance">Khoảng cách đẩy lùi (mét) của vật cản.</param>
        public void TriggerRecovery(float totalRecoveryDuration = -1f, float knockbackDistance = 0f)
        {
            _recoverySpeedAtImpact = CurrentSpeed;
            _recoveryElapsed = 0f;
            _recoveryKnockbackDistance = Mathf.Max(0f, knockbackDistance);

            if (_recoveryKnockbackDistance > 0f)
            {
                // Thời lượng xung cuộn ngược thế giới (0.35s đến 0.55s tùy cự ly đẩy lùi)
                _recoveryKnockbackDuration = Mathf.Clamp(_baseKnockbackDuration + (_recoveryKnockbackDistance * 0.012f), 0.35f, 0.55f);

                // Tổng thời gian hồi phục phải bao gồm cả pha giật lùi + pha đứng dậy tối thiểu 0.45s
                float minTotalDuration = _recoveryKnockbackDuration + 0.45f;
                _recoveryTotalDuration = totalRecoveryDuration > minTotalDuration ? totalRecoveryDuration : minTotalDuration;
            }
            else
            {
                _recoveryKnockbackDuration = 0f;
                _recoveryTotalDuration = totalRecoveryDuration > 0f ? totalRecoveryDuration : _defaultRecoveryTotalDuration;
            }

            _state = SpeedState.Recovery;
        }
    }
}
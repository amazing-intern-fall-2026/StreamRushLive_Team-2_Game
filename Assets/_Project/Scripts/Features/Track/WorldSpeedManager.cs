using System;
using UnityEngine;

namespace SteamRush.Track
{
    /// <summary>
    /// Điều phối tốc độ cuộn của TOÀN BỘ thế giới (Track, Background, Spawner, MovingWorldObject...).
    /// Runner đứng cố định tại X = 9.55; mọi cảm giác "di chuyển" đến từ việc thay đổi CurrentSpeed.
    ///   - Singleton Instance, Speed Ramp tăng dần theo thời gian, Recovery curve khi va chạm.
    /// Triển khai Controls & World Speed Mechanics + Collision Pipeline (GDD v1.2):
    ///   - Slide & Active World Deceleration (nhấp nhả / đè giữ Ctrl-S-↓)
    ///   - Active Sprint (giữ Shift/E, tiêu hao Energy)
    ///   - Khi va chạm: giảm về 0 (0.3-0.5s) -> giữ 0 (thời gian ngã tuỳ obstacle)
    ///     -> tăng mượt lại 10.0 m/s
    ///
    /// THÊM MỚI cho GDD v1.3 (bản thử nghiệm Chat 3-Lane, song song với v1.2):
    ///   - CommandOverride: state riêng cho lệnh chat "fast"/"slow" từ ChatLaneRunnerController —
    ///     ép tốc độ về 1 mục tiêu cụ thể trong 1 khoảng thời gian cố định rồi tự trả về bình
    ///     thường. KHÔNG dùng chung với Sprint (Sprint cần giữ phím liên tục + khoá theo Energy,
    ///     khác hẳn ngữ nghĩa 1 lệnh chat kích hoạt tức thì trong khung giờ cố định).
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
        [Tooltip("Bật/tắt tăng tốc dần theo thời gian. Mặc định = false để tốc độ không tự tăng dần.")]
        [SerializeField] private bool _enableSpeedRamp = false;
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

        [Header("Slide - Hold")]
        [Tooltip("Tốc độ hãm khi ĐÈ GIỮ, m/s² . GDD v1.2 = 8.0 m/s².")]
        [SerializeField] private float _holdDecelRate = 8f;

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
        [Tooltip("Ngưỡng % Energy (0-1) để khóa Sprint. GDD = 10% -> 0.1.")]
        [SerializeField, Range(0f, 1f)] private float _sprintEnergyLockThreshold = 0.1f;

        // ============================================================
        // COLLISION RECOVERY 
        // ============================================================
        [Header("Collision Recovery")]
        [Tooltip("Thời gian giảm về 0 m/s ngay khi va chạm. GDD v1.2 = 0.3-0.5s.")]
        [SerializeField] private float _recoveryDecelDuration = 0.4f;
        [Tooltip("Thời gian hồi phục mặc định nếu obstacle không truyền riêng (giây).")]
        [SerializeField] private float _defaultRecoveryTotalDuration = 1.2f;

        // ============================================================
        // COMMAND OVERRIDE (GDD v1.3 - lệnh chat fast/slow)
        // ============================================================
        [Header("Command Override (Chat fast sprint - GDD v1.3)")]
        [Tooltip("Tốc độ tăng/giảm mỗi giây khi đang tiến tới mục tiêu Command Override.")]
        [SerializeField] private float _commandOverrideAccelRate = 24f;

        // ============================================================
        // STATE
        // ============================================================
        private enum SpeedState { Normal, SlideTap, SlideHold, Sprint, Recovery, CommandOverride, ReverseKnockback }
        private SpeedState _state = SpeedState.Normal;

        private float _rampIncreasePerSecond;
        private float _elapsedTime;
        private float _slideTapTimer;

        // Recovery state riêng (3 pha: decel -> hold 0 -> reaccel)
        private float _recoveryElapsed;
        private float _recoveryTotalDuration;
        private float _recoverySpeedAtImpact;

        // Reverse Knockback state (GDD v1.2 Mục 4: Xung cuộn ngược thế giới đẩy lùi Runner)
        private float _reverseKnockbackElapsed;
        private float _reverseKnockbackDuration = 0.5f;
        private float _reverseKnockbackPeakSpeed = -8f;

        // Command Override state riêng (GDD v1.3)
        private float _commandOverrideTimer;
        private float _commandOverrideTargetSpeed;

        /// <summary>
        /// Tốc độ cuộn hiện tại của thế giới. Giữ public set để tương thích ngược với
        /// TrackTileLooper.WorldSpeed (setter cũ) — KHÔNG tự ý gán từ bên ngoài, hãy dùng
        /// các hàm state (BeginSlideTap/HoldSlide/.../TriggerRecovery) thay vì set trực tiếp.
        /// </summary>
        public float CurrentSpeed { get; set; }

        public bool IsSliding => _state == SpeedState.SlideTap || _state == SpeedState.SlideHold;
        public bool IsSprinting => _state == SpeedState.Sprint;
        public bool IsRecovering => _state == SpeedState.Recovery;
        public bool IsReverseKnockingBack => _state == SpeedState.ReverseKnockback;
        public bool IsCommandOverrideActive => _state == SpeedState.CommandOverride;

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
            float normalTargetSpeed = _enableSpeedRamp
                ? _baseSpeed * (1f + _rampIncreasePerSecond * _elapsedTime)
                : _baseSpeed;

            switch (_state)
            {
                case SpeedState.SlideTap:
                    UpdateSlideTap(dt, normalTargetSpeed);
                    break;

                case SpeedState.SlideHold:
                    CurrentSpeed = Mathf.Max(0f, CurrentSpeed - _holdDecelRate * dt);
                    break;

                case SpeedState.Sprint:
                    UpdateSprint(dt);
                    break;

                case SpeedState.Recovery:
                    UpdateRecovery(dt, normalTargetSpeed);
                    break;

                case SpeedState.CommandOverride:
                    UpdateCommandOverride(dt, normalTargetSpeed);
                    break;

                case SpeedState.ReverseKnockback:
                    UpdateReverseKnockback(dt, normalTargetSpeed);
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
            // Hết năng lượng / dưới ngưỡng khóa -> tự hủy Sprint, quay lại Normal.
            if (CurrentEnergyPercent01 < _sprintEnergyLockThreshold)
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
        /// 3 pha theo GDD mục 4:
        /// Pha 1 (0 -> _recoveryDecelDuration): giảm đều về 0.
        /// Pha 2 (_recoveryDecelDuration -> _recoveryTotalDuration): giữ nguyên 0 (Runner đang ngã).
        /// Pha 3 (sau _recoveryTotalDuration): tăng mượt trở lại tốc độ chuẩn, rồi thoát Recovery.
        /// </summary>
        private void UpdateRecovery(float dt, float normalTargetSpeed)
        {
            _recoveryElapsed += dt;

            if (_recoveryElapsed < _recoveryDecelDuration)
            {
                float decelProgress = Mathf.Clamp01(_recoveryElapsed / _recoveryDecelDuration);
                CurrentSpeed = Mathf.Lerp(_recoverySpeedAtImpact, 0f, decelProgress);
            }
            else if (_recoveryElapsed < _recoveryTotalDuration)
            {
                CurrentSpeed = 0f;
            }
            else
            {
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, normalTargetSpeed, _normalAccelRate * dt);
                if (Mathf.Approximately(CurrentSpeed, normalTargetSpeed))
                {
                    _state = SpeedState.Normal;
                }
            }
        }

        /// <summary>
        /// GDD v1.3 mục 5.2 (lệnh chat fast/slow): tiến dần CurrentSpeed về
        /// _commandOverrideTargetSpeed, đếm ngược _commandOverrideTimer, hết giờ thì tự thoát về
        /// Normal (Update() sẽ lại tự MoveTowards về tốc độ ramp chuẩn ở frame kế tiếp).
        /// </summary>
        private void UpdateCommandOverride(float dt, float normalTargetSpeed)
        {
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, _commandOverrideTargetSpeed, _commandOverrideAccelRate * dt);

            _commandOverrideTimer -= dt;
            if (_commandOverrideTimer <= 0f)
            {
                _state = SpeedState.Normal;
            }
        }

        /// <summary>
        /// GDD v1.2 Mục 4: Xung cuộn ngược thế giới theo hàm Sine (CurrentSpeed < 0)
        /// Mặt đường, vỉa hè và chướng ngại vật trôi giật lùi về phía sau (+X).
        /// Hết thời lượng sẽ hãm về 0 rồi mượt mà lấy lại tốc độ qua TriggerRecovery.
        /// </summary>
        private void UpdateReverseKnockback(float dt, float normalTargetSpeed)
        {
            _reverseKnockbackElapsed += dt;
            float progress = Mathf.Clamp01(_reverseKnockbackElapsed / _reverseKnockbackDuration);

            if (progress < 1f)
            {
                // Hàm Sine nửa chu kỳ (0 -> 1 -> 0): từ 0 vút lên tốc độ giật lùi cao nhất rồi hãm lại về 0
                CurrentSpeed = _reverseKnockbackPeakSpeed * Mathf.Sin(progress * Mathf.PI);
            }
            else
            {
                CurrentSpeed = 0f;
                // Chuyển sang Recovery để hồi phục mượt mà từ 0 -> tốc độ bình thường
                TriggerRecovery(0.4f);
            }
        }

        // ============================================================
        // PUBLIC INPUT API — gọi từ RunnerInputHandler (GDD v1.2)
        // ============================================================

        /// <summary>Gọi khi người chơi NHẤP NHẢ phím Slide (Ctrl / S / ↓).</summary>
        public void BeginSlideTap()
        {
            if (_state == SpeedState.SlideHold || _state == SpeedState.Recovery) return;

            _state = SpeedState.SlideTap;
            _slideTapTimer = _slideTapDuration;
            CurrentSpeed *= _slideTapSpeedMultiplier; // giảm tức thì -50%
        }

        /// <summary>Gọi mỗi frame khi người chơi ĐÈ GIỮ phím Slide (đã vượt ngưỡng phân biệt Tap/Hold).</summary>
        public void HoldSlide()
        {
            if (_state == SpeedState.Recovery) return; // đang ngã thì không cho Slide
            _state = SpeedState.SlideHold;
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
            if (CurrentEnergyPercent01 < _sprintEnergyLockThreshold) return; // khóa khi thiếu năng lượng

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
        /// Gọi khi Runner va chạm vật cản: tốc độ giảm đều về 0 trong _recoveryDecelDuration (0.3-0.5s),
        /// giữ 0 trong lúc Runner đang ngã, rồi tăng mượt trở lại tốc độ chuẩn.
        /// </summary>
        /// <param name="totalRecoveryDuration">
        /// Tổng thời gian từ lúc va chạm tới lúc bắt đầu tăng tốc lại — theo GDD mỗi loại vật cản
        /// có giá trị riêng (Rào thấp/Xà cao = 1.2s, Xe cắt ngang/Vật rơi = 1.8s...). Nếu obstacle
        /// chưa expose field này thì dùng mặc định _defaultRecoveryTotalDuration.
        /// </param>
        public void TriggerRecovery(float totalRecoveryDuration = -1f)
        {
            _recoverySpeedAtImpact = CurrentSpeed;
            _recoveryElapsed = 0f;
            _recoveryTotalDuration = totalRecoveryDuration > 0f ? totalRecoveryDuration : _defaultRecoveryTotalDuration;
            _state = SpeedState.Recovery;
        }

        // ============================================================
        // PUBLIC INPUT API — gọi từ ChatLaneRunnerController (GDD v1.3)
        // ============================================================

        /// <summary>
        /// GDD v1.3 mục 5.2: lệnh chat "fast" hoặc "slow". Ép CurrentSpeed tiến dần về
        /// <paramref name="targetSpeed"/> trong <paramref name="duration"/> giây, sau đó tự trả
        /// lại Normal (tốc độ ramp chuẩn của GDD v1.2 tiếp tục chạy như cũ).
        /// Va chạm (TriggerRecovery) vẫn luôn được ưu tiên ngắt ngang state này nếu xảy ra.
        /// </summary>
        public void TriggerCommandSpeed(float targetSpeed, float duration)
        {
            if (_state == SpeedState.Recovery) return; // đang ngã thì lệnh chat không có tác dụng

            _commandOverrideTargetSpeed = targetSpeed;
            _commandOverrideTimer = duration;
            _state = SpeedState.CommandOverride;
        }

        /// <summary>
        /// Hủy lệnh Command Override sớm (khi hết năng lượng hoặc muốn trở về bình thường tức thì).
        /// </summary>
        public void CancelCommandSpeed()
        {
            if (_state == SpeedState.CommandOverride)
            {
                _state = SpeedState.Normal;
                _commandOverrideTimer = 0f;
            }
        }

        /// <summary>
        /// GDD v1.2 Mục 4: Kích hoạt xung cuộn ngược thế giới khi va chạm vật cản lớn.
        /// Toàn bộ mặt đường, vỉa hè và tòa nhà sẽ trôi ngược hướng (+X) trong một khoảng thời gian ngắn
        /// theo hàm Sine, tạo cảm giác Runner bị đẩy/kéo giật lùi về vị trí cũ trên cung đường.
        /// </summary>
        /// <param name="peakReverseSpeed">Tốc độ giật lùi cực đại (mặc định -8.5 m/s, giá trị âm).</param>
        /// <param name="duration">Thời lượng giật lùi (mặc định 0.5s).</param>
        public void TriggerReverseWorldKnockback(float peakReverseSpeed = -8.5f, float duration = 0.5f)
        {
            _reverseKnockbackElapsed = 0f;
            _reverseKnockbackDuration = Mathf.Max(0.1f, duration);
            _reverseKnockbackPeakSpeed = -Mathf.Abs(peakReverseSpeed);
            _state = SpeedState.ReverseKnockback;
        }
    }
}
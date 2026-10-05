using System;
using UnityEngine;

namespace SteamRush.Track
{
    /// <summary>
    /// Coordinates scroll speed for the entire world (Track, Background, Spawner, MovingWorldObject...).
    /// Runner remains stationary at X = 9.55; all motion perception comes from CurrentSpeed modulation.
    /// Implements:
    ///   - Controls & World Speed Mechanics + Collision Pipeline (GDD v1.2)
    ///   - Slide & Active Deceleration
    ///   - Active Sprint
    ///   - Collision Recovery Pipeline (Decel -> Hold 0 -> Reaccel)
    ///   - Command Override state for chat commands (fast/slow)
    ///   - Victory state (Finish Line sequence)
    /// </summary>
    public class WorldSpeedManager : MonoBehaviour
    {
        public static WorldSpeedManager Instance { get; private set; }

        [Header("Base Speed")]
        [Tooltip("Default world scroll speed when not sliding or sprinting (default: 10.0 m/s).")]
        [SerializeField] private float _baseSpeed = 10f;

        [Header("Speed Ramp")]
        [Tooltip("Enable/disable passive speed ramp over time.")]
        [SerializeField] private bool _enableSpeedRamp = false;
        [Tooltip("Reference benchmark time in seconds (e.g. 60 = 1 minute).")]
        [SerializeField] private float _rampReferenceSeconds = 60f;
        [Tooltip("Target speed multiplier at benchmark time (e.g. 1.2 = +20%).")]
        [SerializeField] private float _rampReferenceMultiplier = 1.2f;

        [Header("Slide - Tap")]
        [Tooltip("Tap slide duration in seconds (default: 0.8s).")]
        [SerializeField] private float _slideTapDuration = 0.8f;
        [Tooltip("Speed fraction during tap slide (0.5 = 50% speed).")]
        [SerializeField, Range(0f, 1f)] private float _slideTapSpeedMultiplier = 0.5f;

        [Header("Slide - Hold")]
        [Tooltip("Deceleration rate when holding slide (m/s²).")]
        [SerializeField] private float _holdDecelRate = 8f;

        [Header("Speed Transition")]
        [Tooltip("Acceleration rate when returning to normal speed.")]
        [SerializeField] private float _normalAccelRate = 8f;

        [Header("Sprint")]
        [Tooltip("Maximum sprint speed (m/s).")]
        [SerializeField] private float _sprintMaxSpeed = 18f;
        [Tooltip("Sprint acceleration multiplier.")]
        [SerializeField] private float _sprintAccelMultiplier = 2f;
        [Tooltip("Energy consumed per second while sprinting.")]
        [SerializeField] private float _sprintEnergyDrainPerSecond = 3f;
        [Tooltip("Energy threshold (0-1) below which sprint is locked.")]
        [SerializeField, Range(0f, 1f)] private float _sprintEnergyLockThreshold = 0.1f;

        [Header("Collision Recovery")]
        [Tooltip("Deceleration duration to 0 m/s on collision.")]
        [SerializeField] private float _recoveryDecelDuration = 0.4f;
        [Tooltip("Default recovery duration if unspecified by obstacle.")]
        [SerializeField] private float _defaultRecoveryTotalDuration = 1.2f;

        [Header("Command Override (Chat fast sprint - GDD v1.3)")]
        [Tooltip("Acceleration/deceleration rate toward command override target speed.")]
        [SerializeField] private float _commandOverrideAccelRate = 24f;

        [Header("Victory (Finish Line - GDD v1.4.1)")]
        [Tooltip("Deceleration duration to 0 m/s upon crossing finish line.")]
        [SerializeField] private float _victoryDecelDuration = 1.5f;

        private enum SpeedState { Normal, SlideTap, SlideHold, Sprint, Recovery, CommandOverride, ReverseKnockback, Victory }
        private SpeedState _state = SpeedState.Normal;

        private float _rampIncreasePerSecond;
        private float _elapsedTime;
        private float _slideTapTimer;

        // Recovery state (3 phases: decel -> hold 0 -> reaccel)
        private float _recoveryElapsed;
        private float _recoveryTotalDuration;
        private float _recoverySpeedAtImpact;

        // Reverse Knockback state: world scrolls in reverse to simulate pushback
        private float _reverseKnockbackElapsed;
        private float _reverseKnockbackDuration = 0.5f;
        private float _reverseKnockbackPeakSpeed = -8f;

        // Command Override state for chat commands
        private float _commandOverrideTimer;
        private float _commandOverrideTargetSpeed;

        // Victory state at finish line
        private float _victorySpeedAtStart;
        private float _victoryElapsed;

        /// <summary>
        /// Current world scroll speed. Setter kept public for backward compatibility.
        /// Use state methods instead of setting directly from external code.
        /// </summary>
        public float CurrentSpeed { get; set; }
        public float BaseSpeed => _baseSpeed;

        public bool IsSliding => _state == SpeedState.SlideTap || _state == SpeedState.SlideHold;
        public bool IsSprinting => _state == SpeedState.Sprint;
        public bool IsRecovering => _state == SpeedState.Recovery;
        public bool IsReverseKnockingBack => _state == SpeedState.ReverseKnockback;
        public bool IsCommandOverrideActive => _state == SpeedState.CommandOverride;
        public bool IsVictoryStopped => _state == SpeedState.Victory;

        /// <summary>
        /// Normalized energy percent (0-1) supplied by EnergySystem.
        /// </summary>
        public float CurrentEnergyPercent01 { get; set; } = 1f;

        /// <summary>Invoked each frame while sprinting to deduct energy.</summary>
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

                case SpeedState.Victory:
                    UpdateVictory(dt);
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
            // Maintain tap speed during tap slide window
        }

        private void UpdateSprint(float dt)
        {
            // Energy depleted below lock threshold -> cancel sprint, return to Normal
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
        /// 3-phase recovery pipeline:
        /// Phase 1: Decelerate smoothly to 0.
        /// Phase 2: Hold at 0 during knockdown animation.
        /// Phase 3: Smoothly re-accelerate to normal speed and exit recovery.
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
        /// Handles chat command speed overrides (fast/slow) for fixed duration.
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
        /// Executes reverse world scroll impulse on heavy collision (CurrentSpeed < 0).
        /// </summary>
        private void UpdateReverseKnockback(float dt, float normalTargetSpeed)
        {
            _reverseKnockbackElapsed += dt;
            float progress = Mathf.Clamp01(_reverseKnockbackElapsed / _reverseKnockbackDuration);

            if (progress < 1f)
            {
                // Half-cycle sine wave: ramps to peak reverse speed and eases back to 0
                CurrentSpeed = _reverseKnockbackPeakSpeed * Mathf.Sin(progress * Mathf.PI);
            }
            else
            {
                CurrentSpeed = 0f;
                // Transition to recovery to restore normal speed
                TriggerRecovery(0.4f);
            }
        }

        /// <summary>
        /// Smoothly brings world speed to 0 for victory ceremony. Permanent until scene reload.
        /// </summary>
        private void UpdateVictory(float dt)
        {
            _victoryElapsed += dt;
            float progress = Mathf.Clamp01(_victoryElapsed / _victoryDecelDuration);
            CurrentSpeed = Mathf.Lerp(_victorySpeedAtStart, 0f, progress);
        }

        // --- PUBLIC INPUT API (RunnerInputHandler) ---

        /// <summary>Called on tap slide input (Ctrl / S / DownArrow).</summary>
        public void BeginSlideTap()
        {
            if (_state == SpeedState.SlideHold || _state == SpeedState.Recovery || _state == SpeedState.Victory) return;

            _state = SpeedState.SlideTap;
            _slideTapTimer = _slideTapDuration;
            CurrentSpeed *= _slideTapSpeedMultiplier; // instantaneous speed cut
        }

        /// <summary>Called each frame while holding slide input.</summary>
        public void HoldSlide()
        {
            if (_state == SpeedState.Recovery || _state == SpeedState.Victory) return; // ignore slide during recovery/victory
            _state = SpeedState.SlideHold;
        }

        /// <summary>Called when releasing held slide.</summary>
        public void ReleaseSlide()
        {
            if (_state == SpeedState.SlideHold)
            {
                _state = SpeedState.Normal; // smooth return to normal speed
            }
            // Let tap slide complete naturally
        }

        /// <summary>Called each frame while holding sprint input.</summary>
        public void HoldSprint()
        {
            if (IsSliding || _state == SpeedState.Recovery || _state == SpeedState.Victory) return; // slide/recovery/victory take priority
            if (CurrentEnergyPercent01 < _sprintEnergyLockThreshold) return; // locked when energy is insufficient

            _state = SpeedState.Sprint;
        }

        /// <summary>Called when releasing sprint input.</summary>
        public void ReleaseSprint()
        {
            if (_state == SpeedState.Sprint)
            {
                _state = SpeedState.Normal;
            }
        }

        /// <summary>
        /// Called when the runner collides with an obstacle: decelerates to 0 within _recoveryDecelDuration (0.3-0.5s),
        /// holds at 0 during knockdown, and smoothly re-accelerates to normal speed.
        /// </summary>
        /// <param name="totalRecoveryDuration">
        /// Total recovery duration from collision until re-acceleration starts.
        /// If unspecified, uses _defaultRecoveryTotalDuration.
        /// </param>
        public void TriggerRecovery(float totalRecoveryDuration = -1f)
        {
            if (_state == SpeedState.Victory) return; // ignore collisions post-victory

            _recoverySpeedAtImpact = CurrentSpeed;
            _recoveryElapsed = 0f;
            _recoveryTotalDuration = totalRecoveryDuration > 0f ? totalRecoveryDuration : _defaultRecoveryTotalDuration;
            _state = SpeedState.Recovery;
        }

        // --- COMMAND OVERRIDE API (Chat fast/slow) ---

        /// <summary>
        /// Applies speed override from chat commands (fast/slow).
        /// </summary>
        public void TriggerCommandSpeed(float targetSpeed, float duration)
        {
            if (_state == SpeedState.ReverseKnockback || _state == SpeedState.Victory) return; // ignore during reverse knockback or victory
            if (_state == SpeedState.Recovery && _recoveryElapsed < _recoveryTotalDuration) return; // ignore during active collision knockdown

            _commandOverrideTargetSpeed = targetSpeed;
            _commandOverrideTimer = duration;
            _state = SpeedState.CommandOverride;
        }

        /// <summary>
        /// Cancels active command override early.
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
        /// Triggers reverse world scroll impulse on heavy collision.
        /// </summary>
        /// <param name="peakReverseSpeed">Peak reverse speed in m/s (negative value).</param>
        /// <param name="duration">Impulse duration in seconds.</param>
        public void TriggerReverseWorldKnockback(float peakReverseSpeed = -8.5f, float duration = 0.5f)
        {
            if (_state == SpeedState.Victory) return; // ignore collisions post-victory

            _reverseKnockbackElapsed = 0f;
            _reverseKnockbackDuration = Mathf.Max(0.1f, duration);
            _reverseKnockbackPeakSpeed = -Mathf.Abs(peakReverseSpeed);
            _state = SpeedState.ReverseKnockback;
        }

        /// <summary>
        /// Smoothly decelerates world speed to 0 when crossing the finish line.
        /// </summary>
        public void TriggerVictoryStop(float decelDuration = -1f)
        {
            _victorySpeedAtStart = CurrentSpeed;
            _victoryElapsed = 0f;
            if (decelDuration > 0f)
            {
                _victoryDecelDuration = decelDuration;
            }
            _state = SpeedState.Victory;
        }
    }
}
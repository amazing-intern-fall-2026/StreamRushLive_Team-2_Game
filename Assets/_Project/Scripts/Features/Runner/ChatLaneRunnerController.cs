namespace SteamRush.Features.Runner
{
    using System.Collections.Generic;
    using UnityEngine;
    using SteamRush.Track;
    using SteamRush.Features.StreamIntegration;
    using StreamRushLive.Features.VFX;

    /// <summary>
    /// Chat command parser and energy-gating coordinator for the Runner.
    /// Receives viewer chat commands (1, 2, 3, jump, slide, fast, etc.), checks Fan energy costs,
    /// and delegates physical execution to RunnerMovement.
    /// </summary>
    [RequireComponent(typeof(RunnerMovement))]
    [RequireComponent(typeof(RunnerCollisionHandler))]
    public class ChatLaneRunnerController : MonoBehaviour
    {
        [Header("Command Queue")]
        [Tooltip("Maximum commands accepted per comment.")]
        [SerializeField] private int _maxCommandsPerBatch = 3;
        [Tooltip("Minimum interval between consecutive fast/slow queue commands.")]
        [SerializeField] private float _nonLaneCommandDelay = 0.1f;

        [Header("Speed Commands (fast)")]
        [Tooltip("World scroll speed on fast command (default: 18.0 m/s).")]
        [SerializeField] private float _fastTargetSpeed = 18f;

        [Header("Energy & Faction References")]
        [Tooltip("FactionTugOfWarManager reference for Fan energy costs.")]
        [SerializeField] private FactionTugOfWarManager _factionManager;
        [Tooltip("RunnerEnergySystem reference for syncing lane change costs.")]
        [SerializeField] private StreamRushLive.Features.Spawning.RunnerEnergySystem _energySystem;

        [Header("Control Energy Costs (GDD v1.4.1 Section 2.2)")]
        [Tooltip("Fan energy cost per lane change (default: 10 points).")]
        [SerializeField] private int _laneChangeEnergyCost = 10;
        [Tooltip("Fan energy cost per jump (default: 20 points).")]
        [SerializeField] private int _jumpEnergyCost = 20;

        [Header("Control Lock On Hit")]
        [Tooltip("Automatically lock all runner controls when pushed back by obstacle or vehicle.")]
        [SerializeField] private bool _lockControlOnKnockback = true;
        [Tooltip("Extra stun / control lock duration after knockback finishes (seconds).")]
        [SerializeField] private float _extraControlLockDuration = 0.25f;

        private float _controlLockTimer = 0f;
        private bool _controlsLocked;

        public int LaneChangeEnergyCost
        {
            get => _laneChangeEnergyCost;
            set => _laneChangeEnergyCost = Mathf.Max(0, value);
        }

        public int JumpEnergyCost
        {
            get => _jumpEnergyCost;
            set => _jumpEnergyCost = Mathf.Max(0, value);
        }

        // Buff durations are managed exclusively via GiftManager
        private float _freeControlBuffDuration = 30f;
        private bool _isFreeControlActive;
        private Coroutine _freeControlCoroutine;

        public bool IsFreeControlActive => _isFreeControlActive;
        public float FreeControlBuffDuration => StreamRushLive.Features.Gifts.GiftManager.Instance != null 
            ? StreamRushLive.Features.Gifts.GiftManager.Instance.FreeControlDuration 
            : _freeControlBuffDuration;

        private float _sprintBuffDuration = 30f;
        private bool _isSprintBuffActive;
        private Coroutine _sprintBuffCoroutine;

        public bool IsSprintBuffActive => _isSprintBuffActive;
        public float SprintBuffDuration => StreamRushLive.Features.Gifts.GiftManager.Instance != null 
            ? StreamRushLive.Features.Gifts.GiftManager.Instance.SprintDuration 
            : _sprintBuffDuration;

        private RunnerMovement _runnerMovement;
        public RunnerMovement Movement => _runnerMovement;

        // Convenient forwarders to RunnerMovement
        public int CurrentLaneIndex => _runnerMovement != null ? _runnerMovement.CurrentLaneIndex : 1;
        public float[] LaneZPositions => _runnerMovement != null ? _runnerMovement.LaneZPositions : new float[] { 3f, 0f, -3f };
        public bool IsKnockingBack => _runnerMovement != null && _runnerMovement.IsKnockingBack;

        /// <summary>
        /// Indicates if runner controls are currently locked (knockdown, finish line, or reverse knockback).
        /// </summary>
        public bool IsControlLocked
        {
            get
            {
                if (_controlsLocked) return true;
                if (!_lockControlOnKnockback) return false;

                if (_controlLockTimer > 0f || IsKnockingBack) return true;
                if (_collisionHandler != null && _collisionHandler.IsHandlingHit) return true;
                if (_speedManager != null && (_speedManager.IsReverseKnockingBack || _speedManager.CurrentSpeed < 0f)) return true;

                return false;
            }
        }

        public bool IsControlsLocked => _controlsLocked;

        [Header("Chat Control Lock")]
        [Tooltip("When true, chat commands (1/2/3/jump) cannot move the runner. Used when manual keyboard control is enabled.")]
        [SerializeField] private bool _isChatControlLocked = false;
        public bool IsChatControlLocked
        {
            get => _isChatControlLocked;
            set
            {
                _isChatControlLocked = value;
                if (value)
                {
                    _commandQueue.Clear();
                }
            }
        }

        public void SetControlsLocked(bool locked)
        {
            _controlsLocked = locked;
            if (locked)
            {
                _commandQueue.Clear();
                StopFast();
            }
        }

        public void ApplyKnockback(float distance = -1f, float duration = -1f)
        {
            _runnerMovement?.ApplyKnockback(distance, duration);

            if (_lockControlOnKnockback)
            {
                float totalDur = duration > 0f ? duration : 0.45f;
                _controlLockTimer = totalDur + Mathf.Max(0f, _extraControlLockDuration);
                _commandQueue.Clear();
                if (!_isSprintBuffActive)
                {
                    StopFast();
                }
            }
        }

        private readonly Queue<string> _commandQueue = new Queue<string>();
        private float _commandCooldownTimer;

        private WorldSpeedManager _speedManager;
        private RunnerCollisionHandler _collisionHandler;
        private Animator _animator;
        private Camera _mainCamera;
        private float _baseFov = 60f;

        private bool _isFastRunning;
        private float _fastTimer;
        private SteamRush.Features.UI.HUDManager _hudManager;
        private float _lastEnergyWarningTime = -10f;

        private void Awake()
        {
            _runnerMovement = GetComponent<RunnerMovement>() ?? GetComponentInParent<RunnerMovement>();
            _speedManager = FindFirstObjectByType<WorldSpeedManager>() ?? WorldSpeedManager.Instance;
            _collisionHandler = GetComponent<RunnerCollisionHandler>();
            _animator = GetComponentInChildren<Animator>();
            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                _baseFov = _mainCamera.fieldOfView;
            }

            if (_factionManager == null)
            {
                _factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            }

            if (_energySystem == null)
            {
                _energySystem = FindFirstObjectByType<StreamRushLive.Features.Spawning.RunnerEnergySystem>();
            }

            if (_hudManager == null)
            {
                _hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            }

            if (GetComponent<RunnerInputHandler>() == null)
            {
                gameObject.AddComponent<RunnerInputHandler>();
            }
        }

        private void Update()
        {
            if (_controlLockTimer > 0f)
            {
                _controlLockTimer -= Time.deltaTime;
            }

            ProcessCommandQueue();
            UpdateFastEnergyDrain();
            UpdateSpeedVisualEffects();
        }

        private void UpdateSpeedVisualEffects()
        {
            if (_speedManager == null)
            {
                _speedManager = WorldSpeedManager.Instance ?? FindFirstObjectByType<WorldSpeedManager>();
                if (_speedManager == null) return;
            }

            float currentSpeed = _speedManager.CurrentSpeed;
            float baseSpeed = 8.0f;

            if (_animator != null)
            {
                if (_speedManager != null && _speedManager.IsVictoryStopped)
                {
                    _animator.speed = 1f;
                }
                else if (currentSpeed <= 0.2f)
                {
                    _animator.speed = 0f;
                }
                else
                {
                    _animator.speed = Mathf.Clamp(currentSpeed / baseSpeed, 1.0f, 2.3f);
                }
            }

            if (_mainCamera != null)
            {
                float targetFov = _baseFov;
                if (currentSpeed > baseSpeed + 2f)
                {
                    targetFov = _baseFov + 12f;
                }

                _mainCamera.fieldOfView = Mathf.Lerp(_mainCamera.fieldOfView, targetFov, Time.deltaTime * 7f);
            }
        }

        public void ExecuteCommands(List<string> commands)
        {
            if (commands == null || IsControlLocked || _isChatControlLocked) return;

            int count = Mathf.Min(commands.Count, _maxCommandsPerBatch);
            for (int i = 0; i < count; i++)
            {
                EnqueueCommand(commands[i]);
            }
        }

        public void ExecuteSingleCommand(string command)
        {
            if (IsControlLocked || _isChatControlLocked) return;
            EnqueueCommand(command);
        }

        private void EnqueueCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command) || IsControlLocked || _isChatControlLocked) return;

            string normalized = command.Trim().ToLowerInvariant();

            if (_commandQueue.Count >= _maxCommandsPerBatch)
            {
                Debug.LogWarning($"[ChatLaneRunner] Queue full, dropped command: {normalized}");
                return;
            }

            _commandQueue.Enqueue(normalized);
        }

        private void ProcessCommandQueue()
        {
            if (IsControlLocked) return;

            if (_commandCooldownTimer > 0f)
            {
                _commandCooldownTimer -= Time.deltaTime;
                return;
            }

            if (_commandQueue.Count == 0) return;

            string command = _commandQueue.Dequeue();
            RunCommand(command);
        }

        private void RunCommand(string command)
        {
            if (IsControlLocked) return;

            if (command == "j") command = "jump";
            if (command == "l") command = "left";
            if (command == "r") command = "right";

            float cooldown = _runnerMovement != null ? _runnerMovement.LaneChangeSmoothTime : 0.2f;

            switch (command)
            {
                case "jump":
                    TriggerJump();
                    _commandCooldownTimer = _nonLaneCommandDelay;
                    break;

                case "1":
                    SetLane(0);
                    _commandCooldownTimer = cooldown;
                    break;

                case "2":
                    SetLane(1);
                    _commandCooldownTimer = cooldown;
                    break;

                case "3":
                    SetLane(2);
                    _commandCooldownTimer = cooldown;
                    break;

                case "left":
                    ChangeLane(-1); // Shift toward lane index 0 (Left: +3.0)
                    _commandCooldownTimer = cooldown;
                    break;

                case "right":
                    ChangeLane(1); // Shift toward lane index 2 (Right: -3.0)
                    _commandCooldownTimer = cooldown;
                    break;

                case "switch":
                case "switch_lane":
                case "chuyenlan":
                    SwitchLaneAuto();
                    _commandCooldownTimer = cooldown;
                    break;

                case "slide":
                case "duck":
                    // Slide / Duck is deprecated; no-op
                    break;

                default:
                    Debug.LogWarning($"[ChatLaneRunner] Invalid command skipped: {command}");
                    break;
            }
        }

        private bool TryPayControlEnergy(int cost)
        {
            if (_isFreeControlActive || _isChatControlLocked)
            {
                return true;
            }

            if (_factionManager == null)
            {
                _factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            }

            if (_energySystem == null)
            {
                _energySystem = FindFirstObjectByType<StreamRushLive.Features.Spawning.RunnerEnergySystem>();
            }

            if (_factionManager == null && _energySystem == null)
            {
                return true;
            }

            bool canPay = true;

            if (_factionManager != null)
            {
                canPay = _factionManager.TrySpendFanEnergy(cost);
            }
            else if (_energySystem != null)
            {
                canPay = _energySystem.TryConsumeLaneChangeEnergy(cost);
            }

            if (canPay && _factionManager != null && _energySystem != null)
            {
                _energySystem.ConsumeLaneChangeEnergy(cost);
            }

            return canPay;
        }

        private void NotifyEnergyDepleted(string actionName)
        {
            if (Time.time - _lastEnergyWarningTime >= 1.2f)
            {
                _lastEnergyWarningTime = Time.time;
                Debug.LogWarning($"[ChatLaneRunner] Fan energy depleted - locked {actionName}.");
            }
        }

        public bool TriggerJump()
        {
            if (IsControlLocked || _runnerMovement == null) return false;

            if (!_runnerMovement.IsGrounded || _runnerMovement.IsDucking)
            {
                return false;
            }

            if (!TryPayControlEnergy(_jumpEnergyCost))
            {
                NotifyEnergyDepleted("Jump");
                return false;
            }

            _runnerMovement.PerformJump();
            return true;
        }

        public bool TriggerDuck(bool isDucking)
        {
            if (IsControlLocked || _runnerMovement == null) return false;

            _runnerMovement.SetDucking(isDucking);
            return true;
        }

        public bool SetLane(int targetIndex)
        {
            if (IsControlLocked || _runnerMovement == null) return false;

            if (targetIndex == _runnerMovement.CurrentLaneIndex)
            {
                return false;
            }

            if (!TryPayControlEnergy(_laneChangeEnergyCost))
            {
                NotifyEnergyDepleted("Lane Switch");
                return false;
            }

            return _runnerMovement.MoveToLane(targetIndex);
        }

        public bool ChangeLane(int direction)
        {
            if (IsControlLocked || _runnerMovement == null) return false;

            int newIndex = _runnerMovement.CurrentLaneIndex + direction;
            if (newIndex == _runnerMovement.CurrentLaneIndex)
            {
                return false;
            }

            if (!TryPayControlEnergy(_laneChangeEnergyCost))
            {
                NotifyEnergyDepleted("Lane Switch");
                return false;
            }

            return _runnerMovement.ShiftLane(direction);
        }

        /// <summary>
        /// Automatically switches to a different lane (0: Left, 1: Mid, 2: Right).
        /// </summary>
        public bool SwitchLaneAuto()
        {
            if (IsControlLocked || _runnerMovement == null) return false;
            int cur = _runnerMovement.CurrentLaneIndex;
            // Lane 0: Left, Lane 1: Mid, Lane 2: Right
            int target = cur switch
            {
                0 => 1,
                1 => Random.value > 0.5f ? 0 : 2,
                2 => 1,
                _ => 1
            };
            return SetLane(target);
        }

        private void TriggerFast()
        {
            if (IsControlLocked) return;

            if (_speedManager == null)
            {
                _speedManager = WorldSpeedManager.Instance ?? FindFirstObjectByType<WorldSpeedManager>();
            }

            _isFastRunning = true;
            _fastTimer = 0f;

            _speedManager?.TriggerCommandSpeed(_fastTargetSpeed, 9999f);
            AudioManager.Instance?.PlaySFX(SFXType.RunnerSpeedBoost, 0.75f);
        }

        private void UpdateFastEnergyDrain()
        {
            if (_isSprintBuffActive)
            {
                _isFastRunning = true;

                if (_speedManager != null &&
                    !_speedManager.IsReverseKnockingBack &&
                    !_speedManager.IsCommandOverrideActive)
                {
                    _speedManager.TriggerCommandSpeed(_fastTargetSpeed, 9999f);
                }
                return;
            }

            if (!_isFastRunning) return;

            if (_speedManager != null && (_speedManager.IsReverseKnockingBack || _speedManager.IsRecovering))
            {
                StopFast();
                return;
            }

            if (_collisionHandler != null && _collisionHandler.IsHandlingHit)
            {
                return;
            }

            if (_speedManager != null && !_speedManager.IsCommandOverrideActive &&
                !_speedManager.IsRecovering && !_speedManager.IsReverseKnockingBack)
            {
                StopFast();
                return;
            }

            _fastTimer += Time.deltaTime;
        }

        private void StopFast()
        {
            if (!_isFastRunning) return;
            _isFastRunning = false;
            _fastTimer = 0f;

            if (_speedManager != null)
            {
                _speedManager.CancelCommandSpeed();
            }
        }

        public void ActivateSprintBuff(float duration = 30f)
        {
            if (_sprintBuffCoroutine != null)
            {
                StopCoroutine(_sprintBuffCoroutine);
            }
            float dur = duration > 0f ? duration : SprintBuffDuration;
            _sprintBuffCoroutine = StartCoroutine(SprintBuffRoutine(dur));
            AudioManager.Instance?.PlaySFX(SFXType.RunnerSpeedBoost);
        }

        private System.Collections.IEnumerator SprintBuffRoutine(float duration)
        {
            _isSprintBuffActive = true;

            TriggerFast();
            SpeedLinesController.Instance?.Activate();

            var timer = SteamRush.Features.UI.Views.FanSprintTimerCircle.Instance;
            if (timer != null) timer.ActivateTimer(duration);

            yield return new WaitForSeconds(duration);

            _isSprintBuffActive = false;
            _sprintBuffCoroutine = null;

            StopFast();
            SpeedLinesController.Instance?.Deactivate();

            var timerEnd = SteamRush.Features.UI.Views.FanSprintTimerCircle.Instance;
            if (timerEnd != null) timerEnd.DeactivateTimer();
        }

        public void SetSprintBuffDebug(bool isActive)
        {
            if (_sprintBuffCoroutine != null)
            {
                StopCoroutine(_sprintBuffCoroutine);
                _sprintBuffCoroutine = null;
            }

            _isSprintBuffActive = isActive;
            var timer = SteamRush.Features.UI.Views.FanSprintTimerCircle.Instance;
            if (isActive)
            {
                TriggerFast();
                SpeedLinesController.Instance?.Activate();
                if (timer != null) timer.ActivateTimer(999f);
            }
            else
            {
                StopFast();
                SpeedLinesController.Instance?.Deactivate();
                if (timer != null) timer.DeactivateTimer();
            }
        }

        public void ActivateFreeControl(float duration = 30f)
        {
            if (_freeControlCoroutine != null)
            {
                StopCoroutine(_freeControlCoroutine);
            }
            float dur = duration > 0f ? duration : FreeControlBuffDuration;
            _freeControlCoroutine = StartCoroutine(FreeControlRoutine(dur));
        }

        private System.Collections.IEnumerator FreeControlRoutine(float duration)
        {
            _isFreeControlActive = true;
            var timer = SteamRush.Features.UI.Views.FreeControlTimerCircle.Instance;
            if (timer != null) timer.ActivateTimer(duration);

            yield return new WaitForSeconds(duration);

            _isFreeControlActive = false;
            _freeControlCoroutine = null;
            var timerEnd = SteamRush.Features.UI.Views.FreeControlTimerCircle.Instance;
            if (timerEnd != null) timerEnd.DeactivateTimer();
        }
    }
}
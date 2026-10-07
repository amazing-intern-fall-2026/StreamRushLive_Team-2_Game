namespace SteamRush.Features.Runner
{
    using System.Collections.Generic;
    using UnityEngine;
    using SteamRush.Track;
    using SteamRush.Features.StreamIntegration;

    /// <summary>
    /// Controls 3-lane runner movement and chat command execution.
    /// </summary>
    [RequireComponent(typeof(RunnerCollisionHandler))]
    [RequireComponent(typeof(Rigidbody))]
    public class ChatLaneRunnerController : MonoBehaviour
    {
        [Header("Lane Settings")]
        [Tooltip("Z positions of the 3 lanes: Left (+3.0) / Middle (0.0) / Right (-3.0) looking toward +X.")]
        [SerializeField] private float[] _laneZPositions = { 3f, 0f, -3f };
        [Tooltip("Lane change transition duration (default: 0.2s). Uses Mathf.SmoothDamp.")]
        [SerializeField] private float _laneChangeSmoothTime = 0.2f;

        [Header("Command Queue")]
        [Tooltip("Maximum commands accepted per comment.")]
        [SerializeField] private int _maxCommandsPerBatch = 3;
        [Tooltip("Minimum interval between consecutive fast/slow queue commands.")]
        [SerializeField] private float _nonLaneCommandDelay = 0.1f;

        [Header("Speed Commands (fast)")]
        [Tooltip("World scroll speed on fast command (default: 18.0 m/s).")]
        [SerializeField] private float _fastTargetSpeed = 18f;
        [Tooltip("FactionTugOfWarManager reference for Fan energy costs.")]
        [SerializeField] private FactionTugOfWarManager _factionManager;
        [Tooltip("EnergySystem reference for syncing lane change costs.")]
        [SerializeField] private StreamRushLive.Features.Spawning.EnergySystem _energySystem;

        [Header("Control Energy Costs (GDD v1.4.1 Section 2.2)")]
        [Tooltip("Fan energy cost per lane change (default: 10 points).")]
        [SerializeField] private int _laneChangeEnergyCost = 10;
        [Tooltip("Fan energy cost per jump (default: 20 points).")]
        [SerializeField] private int _jumpEnergyCost = 20;

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

        [Header("Knockback Settings (GDD v1.2)")]
        [Tooltip("Runner knockback push distance in meters on obstacle collision.")]
        [SerializeField] private float _knockbackDistance = 1.8f;
        [Tooltip("Recovery duration to return to base position after knockback.")]
        [SerializeField] private float _knockbackDuration = 0.45f;

        private float _knockbackOffsetX;
        private float _knockbackTimer = 999f;
        private float _knockbackTotalDuration = 0.45f;
        private float _knockbackStartOffset;

        [Header("Control Lock On Hit")]
        [Tooltip("Automatically lock all runner controls when pushed back by obstacle or vehicle.")]
        [SerializeField] private bool _lockControlOnKnockback = true;
        [Tooltip("Extra stun / control lock duration after knockback finishes (seconds).")]
        [SerializeField] private float _extraControlLockDuration = 0.25f;

        private float _controlLockTimer = 0f;

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

        public bool IsKnockingBack => _knockbackTimer < _knockbackTotalDuration;

        public void ApplyKnockback(float distance = -1f, float duration = -1f)
        {
            _currentSurgeX = 0f;
            float dist = distance >= 0f ? distance : _knockbackDistance;
            _knockbackTotalDuration = duration > 0f ? duration : _knockbackDuration;
            _knockbackTimer = 0f;
            _knockbackStartOffset = -dist;
            _knockbackOffsetX = _knockbackStartOffset;
            AudioManager.Instance?.PlaySFX(SFXType.RunnerKnockback);

            if (_lockControlOnKnockback)
            {
                _controlLockTimer = _knockbackTotalDuration + Mathf.Max(0f, _extraControlLockDuration);
                _commandQueue.Clear();
                if (!_isSprintBuffActive)
                {
                    StopFast();
                }
                _zVelocity = 0f;

                if (_rb != null)
                {
                    _currentLaneIndex = GetClosestLaneIndex(_rb.position.z);
                }
            }
        }

        private int GetClosestLaneIndex(float currentZ)
        {
            int bestIndex = _currentLaneIndex;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _laneZPositions.Length; i++)
            {
                float dist = Mathf.Abs(currentZ - _laneZPositions[i]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        private void UpdateKnockback(float dt)
        {
            if (_knockbackTimer < _knockbackTotalDuration)
            {
                _knockbackTimer += dt;
                float progress = Mathf.Clamp01(_knockbackTimer / _knockbackTotalDuration);
                // EaseOutQuad for snappy initial knockback followed by gradual recovery
                float ease = 1f - (1f - progress) * (1f - progress);
                _knockbackOffsetX = Mathf.Lerp(_knockbackStartOffset, 0f, ease);
            }
            else
            {
                _knockbackOffsetX = 0f;
            }
        }

        private int _currentLaneIndex = 1;
        private float _zVelocity;

        private readonly Queue<string> _commandQueue = new Queue<string>();
        private float _commandCooldownTimer;

        private WorldSpeedManager _speedManager;
        private RunnerCollisionHandler _collisionHandler;
        private RunnerController _runnerController;
        private Rigidbody _rb;
        private Animator _animator;
        private Camera _mainCamera;
        private float _baseFov = 60f;
        private float _baseX;
        private float _currentSurgeX;

        private bool _isFastRunning;
        private float _fastTimer;

        // Controls lock (GDD v1.4.1 - Victory Celebration): called when Runner crosses finish line archway,
        // ignoring any further chat commands (lane switch, jump, fast are blocked).
        private bool _controlsLocked;
        public bool IsControlsLocked => _controlsLocked;

        public void SetControlsLocked(bool locked)
        {
            _controlsLocked = locked;
            if (locked)
            {
                _commandQueue.Clear();
                StopFast();
            }
            Debug.Log($"[ChatLaneRunner] ControlsLocked = {locked}");
        }

        private SteamRush.Features.UI.HUDManager _hudManager;
        private float _lastEnergyWarningTime = -10f;

        private void Awake()
        {
            _speedManager = FindFirstObjectByType<WorldSpeedManager>() ?? WorldSpeedManager.Instance;
            _collisionHandler = GetComponent<RunnerCollisionHandler>();
            _runnerController = GetComponent<RunnerController>();
            _rb = GetComponent<Rigidbody>();
            _animator = GetComponentInChildren<Animator>();
            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                _baseFov = _mainCamera.fieldOfView;
            }
            _baseX = transform.position.x;
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = false;

            if (_factionManager == null)
            {
                _factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            }

            if (_energySystem == null)
            {
                _energySystem = FindFirstObjectByType<StreamRushLive.Features.Spawning.EnergySystem>();
            }

            if (_hudManager == null)
            {
                _hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
            }
        }

        private void Start()
        {
            _rb.constraints &= ~(RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezePositionX);

            Vector3 startPos = transform.position;
            startPos.z = _laneZPositions[_currentLaneIndex];
            _rb.position = startPos;
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
                // Victory (GDD v1.4.1): Animator.speed affects all layers.
                // Keep speed at 1f during victory stop so Dance animation continues looping even when World speed is 0.
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

            float targetSurgeX = 0f;
            bool isKnockedOrRecovering = (_speedManager != null && (_speedManager.IsReverseKnockingBack || _speedManager.IsRecovering)) ||
                                        (_collisionHandler != null && _collisionHandler.IsHandlingHit) ||
                                        IsKnockingBack;
            if (!isKnockedOrRecovering && currentSpeed > baseSpeed + 2f)
            {
                targetSurgeX = 1.6f;
            }

            _currentSurgeX = Mathf.Lerp(_currentSurgeX, targetSurgeX, Time.deltaTime * 7f);
        }

        private void FixedUpdate()
        {
            UpdateLaneMovement();
        }

        private void UpdateLaneMovement()
        {
            UpdateKnockback(Time.fixedDeltaTime);

            Vector3 pos = _rb.position;
            float newZ;

            if (IsControlLocked)
            {
                _zVelocity = 0f;
                newZ = _laneZPositions[_currentLaneIndex];
            }
            else
            {
                float targetZ = _laneZPositions[_currentLaneIndex];
                newZ = Mathf.SmoothDamp(pos.z, targetZ, ref _zVelocity, _laneChangeSmoothTime);
            }

            float newX = _baseX + _currentSurgeX + _knockbackOffsetX;
            _rb.MovePosition(new Vector3(newX, pos.y, newZ));
        }

        public void ExecuteCommands(List<string> commands)
        {
            if (commands == null || IsControlLocked) return;

            int count = Mathf.Min(commands.Count, _maxCommandsPerBatch);
            for (int i = 0; i < count; i++)
            {
                EnqueueCommand(commands[i]);
            }
        }

        public void ExecuteSingleCommand(string command)
        {
            if (IsControlLocked) return;
            EnqueueCommand(command);
        }

        private void EnqueueCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command) || IsControlLocked) return;

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
            if (IsControlLocked)
            {
                return;
            }

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

            if (command == "j")
            {
                command = "jump";
            }

            switch (command)
            {
                case "jump":
                    TriggerJump();
                    _commandCooldownTimer = _nonLaneCommandDelay;
                    break;

                case "1":
                    SetLane(0);
                    _commandCooldownTimer = _laneChangeSmoothTime;
                    break;

                case "2":
                    SetLane(1);
                    _commandCooldownTimer = _laneChangeSmoothTime;
                    break;

                case "3":
                    SetLane(2);
                    _commandCooldownTimer = _laneChangeSmoothTime;
                    break;

                default:
                    Debug.LogWarning($"[ChatLaneRunner] Invalid command skipped: {command}");
                    break;
            }
        }

        // Validates and deducts Fan control energy for lane switch or jump (GDD v1.4.1 Section 2.2).
        // During active Free-Control Buff, all actions are 100% free and bypass locks.
        private bool TryPayControlEnergy(int cost)
        {
            if (_isFreeControlActive)
            {
                return true;
            }

            if (_factionManager == null)
            {
                _factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            }

            if (_energySystem == null)
            {
                _energySystem = FindFirstObjectByType<StreamRushLive.Features.Spawning.EnergySystem>();
            }

            if (_factionManager == null && _energySystem == null)
            {
                // If FactionTugOfWarManager or EnergySystem is unassigned, permit actions for test scenes.
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

            // Sync deduction with EnergySystem if present
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
                Debug.LogWarning($"[ChatLaneRunner] Fan energy depleted — locked {actionName}.");
            }
        }

        public bool TriggerJump()
        {
            if (IsControlLocked) return false;

            if (_runnerController == null || !_runnerController.IsGrounded || _runnerController.IsDucking)
            {
                return false;
            }

            // Validate and deduct energy before jump: lock jump if Fan energy is depleted
            if (!TryPayControlEnergy(_jumpEnergyCost))
            {
                NotifyEnergyDepleted("Jump");
                return false;
            }

            _runnerController.PerformJump();
            AudioManager.Instance?.PlaySFX(SFXType.RunnerJump);
            // Jump trigger is handled by RunnerController.PerformJump()
            return true;
        }

        private void SetLane(int targetIndex)
        {
            if (IsControlLocked) return;

            int clampedIndex = Mathf.Clamp(targetIndex, 0, _laneZPositions.Length - 1);
            if (clampedIndex == _currentLaneIndex)
            {
                return;
            }

            // Deduct energy only when target lane differs from current lane
            if (!TryPayControlEnergy(_laneChangeEnergyCost))
            {
                NotifyEnergyDepleted("Lane Switch");
                return;
            }

            _currentLaneIndex = clampedIndex;
            AudioManager.Instance?.PlaySFX(SFXType.RunnerLaneSwitch);
        }

        private void ChangeLane(int direction)
        {
            if (IsControlLocked) return;

            int newIndex = Mathf.Clamp(_currentLaneIndex + direction, 0, _laneZPositions.Length - 1);
            if (newIndex == _currentLaneIndex)
            {
                return;
            }

            if (!TryPayControlEnergy(_laneChangeEnergyCost))
            {
                NotifyEnergyDepleted("Lane Switch");
                return;
            }

            _currentLaneIndex = newIndex;
            AudioManager.Instance?.PlaySFX(SFXType.RunnerLaneSwitch);
        }

        private void TriggerFast()
        {
            if (IsControlLocked) return;

            // GDD v1.4.1 Section 2.2: Fan energy costs only apply to Lane Switch and Jump.
            // Fast running is free (0% energy cost), so we don't check Fan energy here.
            if (_speedManager == null)
            {
                _speedManager = WorldSpeedManager.Instance ?? FindFirstObjectByType<WorldSpeedManager>();
            }

            _isFastRunning = true;
            _fastTimer = 0f;

            _speedManager?.TriggerCommandSpeed(_fastTargetSpeed, 9999f);
            AudioManager.Instance?.PlaySFX(SFXType.RunnerSpeedBoost, 0.75f);
        }

        // Time-based Fan energy drain removed (Fast running is free).
        // This method handles: (1) cancel fast on collision / knockback / recovery unless Sprint Buff is active;
        // (2) maintain fast speed while Sprint Buff is running.
        private void UpdateFastEnergyDrain()
        {
            if (_isSprintBuffActive)
            {
                _isFastRunning = true;

                // If in reverse knockback, defer command speed until recovery completes
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

            // Detect if CommandOverride was externally cancelled (e.g. collision -> Recovery -> Normal)
            // while _isFastRunning is still true. Sync controller state with speed manager.
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

            // Always attempt to cancel, even if speed manager already left CommandOverride
            // (e.g. collision interrupted it). This ensures a clean state reset.
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
            var timer = SteamRush.Features.UI.Views.FanSprintTimerCircle.Instance;
            if (timer != null) timer.ActivateTimer(duration);

            yield return new WaitForSeconds(duration);

            _isSprintBuffActive = false;
            _sprintBuffCoroutine = null;
            StopFast();
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
                if (timer != null) timer.ActivateTimer(999f);
            }
            else
            {
                StopFast();
                if (timer != null) timer.DeactivateTimer();
            }
        }

        /// <summary>
        /// Activates Free Control buff for Fan team (0% energy cost for jump and lane switches).
        /// Re-activating refreshes duration.
        /// </summary>
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
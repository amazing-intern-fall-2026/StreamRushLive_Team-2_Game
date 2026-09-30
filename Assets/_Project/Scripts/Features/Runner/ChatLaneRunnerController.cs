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
        [Tooltip("Vị trí trục Z của 3 làn: Trái (+3.0) / Giữa (0.0) / Phải (-3.0) theo hướng camera nhìn +X.")]
        [SerializeField] private float[] _laneZPositions = { 3f, 0f, -3f };
        [Tooltip("Thời gian lách làn mượt mà. GDD v1.3 = 0.2s, dùng Mathf.SmoothDamp.")]
        [SerializeField] private float _laneChangeSmoothTime = 0.2f;

        [Header("Command Queue")]
        [Tooltip("Số lệnh tối đa được nhận từ 1 lần gọi ExecuteCommands (1 comment chat).")]
        [SerializeField] private int _maxCommandsPerBatch = 3;
        [Tooltip("Thời gian chờ tối thiểu giữa 2 lệnh fast/slow liên tiếp trong queue (giây).")]
        [SerializeField] private float _nonLaneCommandDelay = 0.1f;

        [Header("Speed Commands (fast)")]
        [Tooltip("Tốc độ cuộn thế giới khi nhận lệnh fast (bứt tốc turbo). Mặc định = 18.0 m/s để tạo cảm giác bứt phá xé gió rõ rệt.")]
        [SerializeField] private float _fastTargetSpeed = 18f;
        [Tooltip("Tốc độ tiêu hao năng lượng Fan mỗi giây khi chạy fast (VD: 25/s thì 100 năng lượng chạy 4s, 50 năng lượng chạy 2s và giảm hết về 0).")]
        [SerializeField] private float _fastEnergyDrainPerSecond = 25f;
        [Tooltip("Tham chiếu FactionTugOfWarManager để kiểm tra và trừ năng lượng Fan.")]
        [SerializeField] private FactionTugOfWarManager _factionManager;

        [Header("Sprint Buff (Fan Gift - F2)")]
        [Tooltip("Thời gian hiệu lực mặc định của Bình Tăng Tốc (giây). GDD v1.4 = 30s.")]
        [SerializeField] private float _sprintBuffDuration = 30f;
        private bool _isSprintBuffActive;
        private Coroutine _sprintBuffCoroutine;

        public bool IsSprintBuffActive => _isSprintBuffActive;

        [Header("Knockback Settings (GDD v1.2)")]
        [Tooltip("Khoảng cách đẩy lùi Runner (mét) khi va chạm chướng ngại vật theo GDD v1.2.")]
        [SerializeField] private float _knockbackDistance = 1.8f;
        [Tooltip("Thời gian hồi phục lại vị trí gốc sau khi bị đẩy lùi (giây).")]
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
        private bool _controlsLocked;

        /// <summary>
        /// Khóa điều khiển (GDD v1.4.1 - Victory Celebration): Runner cán đích, không nhận lệnh di chuyển.
        /// </summary>
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

        /// <summary>
        /// Runner có đang bị khóa điều khiển (do va chạm bị đẩy lùi, choáng, về đích hoặc thế giới đang cuộn ngược) không.
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

            if (_lockControlOnKnockback)
            {
                _controlLockTimer = _knockbackTotalDuration + Mathf.Max(0f, _extraControlLockDuration);
                _commandQueue.Clear();
                StopFast();
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
                // Ease Out Quad cho cảm giác bật lùi dứt khoát rồi từ từ lấy lại đà chạy
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
        private float _fastEnergyAccumulator;

        // Khoa dieu khien (GDD v1.4.1 - Victory Celebration): goi khi Runner bang qua Cong Ve Dich,
        // khong con nhan lenh chat nao nua (doi lan/nhay/fast deu bi chan).
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

            if (_factionManager == null)
            {
                _factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
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
                // Victory (GDD v1.4.1): Animator.speed la toc do phat CHUNG cho moi layer, khong
                // rieng Base Layer - neu de "<=0.2f -> speed=0" ap dung luc World dung han sau khi
                // ve dich se dong bang luon ca layer Dance dang chay, gay hien tuong "nhay 1 lan roi
                // dung yen" thay vi loop. Ep speed=1 binh thuong de Dance van chay tiep du World=0.
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
                Debug.LogWarning($"[ChatLaneRunner] Queue đầy, huỷ lệnh dư: {normalized}");
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
            
            if (command == "j" || command == "nhay" || command == "up")
            {
                command = "jump";
            }

            switch (command)
            {
                case "jump":
                    TriggerJump();
                    _commandCooldownTimer = _nonLaneCommandDelay;
                    break;

                case "left":
                    ChangeLane(-1);
                    _commandCooldownTimer = _laneChangeSmoothTime;
                    break;

                case "right":
                    ChangeLane(1);
                    _commandCooldownTimer = _laneChangeSmoothTime;
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

                case "fast":
                    TriggerFast();
                    _commandCooldownTimer = _nonLaneCommandDelay;
                    break;

                default:
                    Debug.LogWarning($"[ChatLaneRunner] Lệnh không hợp lệ, bỏ qua: {command}");
                    break;
            }
        }

        private void TriggerJump()
        {
            if (IsControlLocked) return;

            if (_runnerController != null && _runnerController.IsGrounded && !_runnerController.IsDucking)
            {
                _runnerController.PerformJump();
                if (_animator != null)
                {
                    _animator.SetTrigger("Jump");
                }
            }
        }

        private void SetLane(int targetIndex)
        {
            if (IsControlLocked) return;

            int clampedIndex = Mathf.Clamp(targetIndex, 0, _laneZPositions.Length - 1);
            if (clampedIndex == _currentLaneIndex)
            {
                return;
            }

            _currentLaneIndex = clampedIndex;
        }

        private void ChangeLane(int direction)
        {
            if (IsControlLocked) return;

            int newIndex = Mathf.Clamp(_currentLaneIndex + direction, 0, _laneZPositions.Length - 1);
            if (newIndex == _currentLaneIndex)
            {
                return;
            }

            _currentLaneIndex = newIndex;
        }

        private void TriggerFast()
        {
            if (IsControlLocked) return;

            if (_factionManager == null)
            {
                _factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            }

            if (!_isSprintBuffActive && _factionManager != null && _factionManager.FanLikes <= 0)
            {
                Debug.LogWarning($"[ChatLaneRunner] Hết năng lượng Fan ({_factionManager.FanLikes}) để bứt tốc fast!");
                return;
            }

            if (_speedManager == null)
            {
                _speedManager = WorldSpeedManager.Instance ?? FindFirstObjectByType<WorldSpeedManager>();
            }

            _isFastRunning = true;
            _fastTimer = 0f;
            _fastEnergyAccumulator = 0f;

            _speedManager?.TriggerCommandSpeed(_fastTargetSpeed, 9999f);
        }

        private void UpdateFastEnergyDrain()
        {
            if (!_isFastRunning) return;

            if (_speedManager != null && (_speedManager.IsReverseKnockingBack || _speedManager.IsRecovering))
            {
                if (!_isSprintBuffActive)
                {
                    StopFast();
                }
                return;
            }

            if (_collisionHandler != null && _collisionHandler.IsHandlingHit)
            {
                return;
            }

            if (_isSprintBuffActive)
            {
                if (_speedManager != null && 
                    !_speedManager.IsRecovering && 
                    !_speedManager.IsReverseKnockingBack && 
                    _speedManager.CurrentSpeed < _fastTargetSpeed - 0.5f)
                {
                    _speedManager.TriggerCommandSpeed(_fastTargetSpeed, 9999f);
                }
                return;
            }

            if (_speedManager != null && (_speedManager.IsRecovering || _speedManager.IsReverseKnockingBack))
            {
                StopFast();
                return;
            }

            float dt = Time.deltaTime;
            _fastTimer += dt;

            _fastEnergyAccumulator += _fastEnergyDrainPerSecond * dt;

            if (_fastEnergyAccumulator >= 1f)
            {
                int intDrain = Mathf.FloorToInt(_fastEnergyAccumulator);
                _fastEnergyAccumulator -= intDrain;

                if (_factionManager != null)
                {
                    bool stillHasEnergy = _factionManager.TryConsumeFanEnergy(intDrain);
                    if (!stillHasEnergy || _factionManager.FanLikes <= 0)
                    {
                        StopFast();
                        return;
                    }
                }
            }

            if (_factionManager != null && _factionManager.FanLikes <= 0)
            {
                StopFast();
            }
        }

        private void StopFast()
        {
            if (!_isFastRunning) return;
            _isFastRunning = false;
            _fastTimer = 0f;
            _fastEnergyAccumulator = 0f;
            _speedManager?.CancelCommandSpeed();
        }

        public void ActivateSprintBuff(float duration = 30f)
        {
            if (_sprintBuffCoroutine != null)
            {
                StopCoroutine(_sprintBuffCoroutine);
            }
            float dur = duration > 0f ? duration : _sprintBuffDuration;
            _sprintBuffCoroutine = StartCoroutine(SprintBuffRoutine(dur));
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
    }
}
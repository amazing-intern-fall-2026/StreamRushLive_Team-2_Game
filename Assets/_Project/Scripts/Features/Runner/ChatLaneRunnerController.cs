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
        [Tooltip("Tham chiếu FactionTugOfWarManager để kiểm tra và trừ năng lượng Fan.")]
        [SerializeField] private FactionTugOfWarManager _factionManager;
        [Tooltip("Tham chiếu EnergySystem (nếu có trong scene) để đồng bộ trừ năng lượng khi đổi làn.")]
        [SerializeField] private StreamRushLive.Features.Spawning.EnergySystem _energySystem;

        [Header("Control Energy Costs (GDD v1.4.1 mục 2.2)")]
        [Tooltip("Chi phí năng lượng Fan khi đổi làn 1 lần. GDD v1.4.1 = -1% (-10 điểm trên thang 1000).")]
        [SerializeField] private int _laneChangeEnergyCost = 10;
        [Tooltip("Chi phí năng lượng Fan khi nhảy 1 lần. GDD v1.4.1 = -2% (-20 điểm trên thang 1000).")]
        [SerializeField] private int _jumpEnergyCost = 20;

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
                    Debug.LogWarning($"[ChatLaneRunner] Lệnh không hợp lệ, bỏ qua: {command}");
                    break;
            }
        }

        // GDD v1.4.1 muc 2.2: kiem tra + tru nang luong Fan cho 1 thao tac dieu khien (doi lan/
        // nhay). Trong luc Free-Control Buff dang bat, moi thao tac mien phi 100% va bo qua khoa
        // (duoc phep ngay ca khi Fan = 0%). Goi ham nay TRUOC khi thuc hien hanh dong - neu tra
        // ve false thi KHONG duoc thuc hien hanh dong (khoa doi lan / khoa nhay).
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
                // Chưa nối FactionTugOfWarManager hoặc EnergySystem trong scene (vd. scene test riêng) — không khoá,
                // cho phép thao tác như cũ để không chặn việc test các phần khác.
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

            // Nếu cả hai cùng có mặt trong scene, đồng bộ trừ luôn cả EnergySystem
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
            }
            Debug.LogWarning($"[ChatLaneRunner] Hết năng lượng Fan/Blue Team — khoá {actionName}.");
        }

        public bool TriggerJump()
        {
            if (IsControlLocked) return false;

            if (_runnerController == null || !_runnerController.IsGrounded || _runnerController.IsDucking)
            {
                return false;
            }

            // Kiểm tra + trừ năng lượng TRƯỚC khi nhảy thật sự xảy ra: không đủ (hoặc hết) năng
            // lượng Fan thì khoá nhảy, Runner buộc phải chịu va chạm nếu phía trước có chướng ngại.
            if (!TryPayControlEnergy(_jumpEnergyCost))
            {
                NotifyEnergyDepleted("nhảy");
                return false;
            }

            _runnerController.PerformJump();
            AudioManager.Instance?.PlaySFX(SFXType.RunnerJump);
            // Không tự SetTrigger("Jump") ở đây nữa — RunnerController.PerformJump() đã tự bắn
            // Trigger "Jump" cho Animator, gọi lại ở đây sẽ set trigger 2 lần thừa mỗi lần nhảy.
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

            // Chỉ trừ năng lượng khi lane đích THỰC SỰ khác lane hiện tại (đã check ở trên).
            if (!TryPayControlEnergy(_laneChangeEnergyCost))
            {
                NotifyEnergyDepleted("đổi làn");
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
                NotifyEnergyDepleted("đổi làn");
                return;
            }

            _currentLaneIndex = newIndex;
            AudioManager.Instance?.PlaySFX(SFXType.RunnerLaneSwitch);
        }

        private void TriggerFast()
        {
            if (IsControlLocked) return;

            // GDD v1.4.1 muc 2.2: bang tieu hao nang luong Fan chi con Doi Lan va Nhay. Chay Fast
            // la mien phi (0% nang luong), nen KHONG con kiem tra FanLikes <= 0 truoc khi cho fast
            // chay nua — chi con logic khoi dong toc do.
            if (_speedManager == null)
            {
                _speedManager = WorldSpeedManager.Instance ?? FindFirstObjectByType<WorldSpeedManager>();
            }

            _isFastRunning = true;
            _fastTimer = 0f;

            _speedManager?.TriggerCommandSpeed(_fastTargetSpeed, 9999f);
            AudioManager.Instance?.PlaySFX(SFXType.RunnerSpeedBoost, 0.75f);
        }

        // Da go bo phan tru nang luong Fan theo thoi gian (GDD v1.4.1: fast mien phi). Ham nay chi
        // con giu 2 viec: (1) huy fast ngay khi Runner dang bi va cham / knockback / recovering,
        // tru khi dang co Sprint Buff; (2) duy tri toc do fast khi dang co Sprint Buff dang chay.
        private void UpdateFastEnergyDrain()
        {
            if (_isSprintBuffActive)
            {
                _isFastRunning = true;

                // Nếu đang bị giật lùi (ReverseKnockback), tạm thời để WorldSpeedManager xử lý hiệu ứng giật lùi.
                // Ngay khi thoát giật lùi và pha dừng va chạm, lập tức tái kích hoạt CommandSpeed 18 m/s!
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
        /// Kích hoạt quà Bình Thao Tác Tự Do (Free-Control Buff) cho phe Fan trong duration giây
        /// (mặc định 30s, GDD v1.4.1 mục 3). Trong lúc hiệu lực: Đổi Làn và Nhảy tiêu tốn 0% năng
        /// lượng Fan, kể cả khi Fan đang ở mức 0% (bỏ qua khoá thao tác — xem TryPayControlEnergy).
        /// Bấm lại trong lúc buff đang chạy sẽ reset lại đủ duration giây (không cộng dồn).
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
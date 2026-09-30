namespace SteamRush.Features.Runner
{
    using System.Collections.Generic;
    using UnityEngine;
    using SteamRush.Track;
    using SteamRush.Features.StreamIntegration;

    /// <summary>
    /// Điều khiển Runner theo GDD v1.3 mục 1 (Lối chơi 3 làn) + mục 5 (Hệ thống điều khiển chat).
    /// Bản thử nghiệm SONG SONG với gameplay 1 làn cũ (GDD v1.2) — KHÔNG thay thế RunnerController
    /// hay RunnerInputHandler, chỉ cộng thêm khả năng đổi làn + nhận lệnh chat lên trên Runner có
    /// sẵn. Toàn bộ logic Nhảy/Trượt/Va chạm vẫn do RunnerController + RunnerCollisionHandler đảm
    /// nhiệm như cũ, script này không đụng vào.
    ///
    /// LƯU Ý QUAN TRỌNG VỀ VẬT LÝ: RunnerController khoá vĩnh viễn RigidbodyConstraints.
    /// FreezePositionZ (vì bản v1.2 gốc không cần đổi làn). Script này tự mở khoá RIÊNG bit Z đó
    /// ngay lúc Start() (không đụng gì tới RunnerController.cs), và dùng RB.MovePosition() trong
    /// FixedUpdate() thay vì ghi thẳng transform.position trong Update() — bắt buộc phải làm vậy
    /// vì Rigidbody không-Kinematic sẽ tự kéo transform về lại vị trí cũ mỗi bước mô phỏng vật lý
    /// nếu chỉ ghi transform.position suông, khiến nhân vật trông như không di chuyển gì cả.
    ///
    /// Luồng dữ liệu: Bộ lọc chat (chưa nối) sẽ gọi ExecuteCommands(List&lt;string&gt;) mỗi khi có
    /// comment mới từ Follower đang là Runner. Trong lúc chưa nối, dùng phím A/D để tự test.
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

        [Header("Control Energy Costs (GDD v1.4.1 mục 2.2)")]
        [Tooltip("Chi phí năng lượng Fan khi đổi làn 1 lần. GDD v1.4.1 = -1% (-10 điểm trên thang 1000).")]
        [SerializeField] private int _laneChangeEnergyCost = 10;
        [Tooltip("Chi phí năng lượng Fan khi nhảy 1 lần. GDD v1.4.1 = -2% (-20 điểm trên thang 1000).")]
        [SerializeField] private int _jumpEnergyCost = 20;

        [Header("Free-Control Buff (Fan Gift - Shift+F1)")]
        [Tooltip("Thời gian hiệu lực mặc định của Bình Thao Tác Tự Do (giây). GDD v1.4.1 = 30s.")]
        [SerializeField] private float _freeControlBuffDuration = 30f;
        private bool _isFreeControlActive;
        private Coroutine _freeControlCoroutine;

        public bool IsFreeControlActive => _isFreeControlActive;

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

        public bool IsKnockingBack => _knockbackTimer < _knockbackTotalDuration;

        public void ApplyKnockback(float distance = -1f, float duration = -1f)
        {
            _currentSurgeX = 0f; // Triệt tiêu ngay lập tức rướn người về phía trước
            float dist = distance >= 0f ? distance : _knockbackDistance;
            _knockbackTotalDuration = duration > 0f ? duration : _knockbackDuration;
            _knockbackTimer = 0f;
            _knockbackStartOffset = -dist; // Đẩy lùi về phía sau (-X) nếu dist > 0
            _knockbackOffsetX = _knockbackStartOffset;
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

        private int _currentLaneIndex = 1; // bắt đầu ở làn giữa
        private float _zVelocity; // bắt buộc phải có cho Mathf.SmoothDamp, lưu vận tốc giữa các frame

        private readonly Queue<string> _commandQueue = new Queue<string>();
        private float _commandCooldownTimer;

        private WorldSpeedManager _speedManager;
        private RunnerCollisionHandler _collisionHandler; // chỉ tham chiếu, KHÔNG sửa logic bên trong
        private RunnerController _runnerController; // Đã thêm RunnerController
        private Rigidbody _rb;
        private Animator _animator;
        private Camera _mainCamera;
        private float _baseFov = 60f;
        private float _baseX;
        private float _currentSurgeX;

        private bool _isFastRunning;
        private float _fastTimer;

        private void Awake()
        {
            _speedManager = FindFirstObjectByType<WorldSpeedManager>() ?? WorldSpeedManager.Instance;
            _collisionHandler = GetComponent<RunnerCollisionHandler>();
            _runnerController = GetComponent<RunnerController>(); // Lấy component RunnerController
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
            // Chạy ở Start() (không phải Awake()) để CHẮC CHẮN RunnerController.Awake() đã set
            // xong constraints trước — nếu làm ở Awake(), thứ tự Awake() giữa 2 script trên cùng
            // GameObject không được đảm bảo, có thể bị RunnerController ghi đè lại constraints
            // SAU khi mình vừa mở khoá, làm mất tác dụng.
            _rb.constraints &= ~(RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezePositionX);

            // Đặt vị trí Z ban đầu đúng làn giữa, tránh Runner spawn lệch làn nếu Scene đặt sai.
            Vector3 startPos = transform.position;
            startPos.z = _laneZPositions[_currentLaneIndex];
            _rb.position = startPos;
        }

        private void Update()
        {
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

            // 1. Đồng bộ nhịp chạy Animator với tốc độ cuộn thế giới:
            // 8m/s -> 1.0x (chạy đều), 18m/s -> 2.25x (bứt tốc cuồng nhiệt xé gió)
            if (_animator != null)
            {
                if (currentSpeed <= 0.2f)
                {
                    _animator.speed = 0f;
                }
                else
                {
                    _animator.speed = Mathf.Clamp(currentSpeed / baseSpeed, 1.0f, 2.3f);
                }
            }

            // 2. Hiệu ứng Camera FOV (Speed Warp Effect): mở rộng góc nhìn xé gió khi fast sprint
            if (_mainCamera != null)
            {
                float targetFov = _baseFov;
                if (currentSpeed > baseSpeed + 2f)
                {
                    targetFov = _baseFov + 12f; // Tăng lên 72 FOV tạo hiệu ứng bứt tốc rõ rệt
                }

                _mainCamera.fieldOfView = Mathf.Lerp(_mainCamera.fieldOfView, targetFov, Time.deltaTime * 7f);
            }

            // 3. Hiệu ứng vị trí Runner trên thảm chạy (rướn mạnh lên phía trước khi bứt tốc fast)
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

        /// <summary>
        /// Trượt mềm trục Z về đúng vị trí làn hiện tại — chạy trong FixedUpdate và dùng
        /// RB.MovePosition() (không ghi transform.position trực tiếp) để không bị Physics Engine
        /// kéo ngược lại, vì Rigidbody vẫn là non-kinematic (Y vẫn rơi/nhảy bình thường).
        /// </summary>
        private void UpdateLaneMovement()
        {
            UpdateKnockback(Time.fixedDeltaTime);

            float targetZ = _laneZPositions[_currentLaneIndex];
            Vector3 pos = _rb.position;
            float newZ = Mathf.SmoothDamp(pos.z, targetZ, ref _zVelocity, _laneChangeSmoothTime);
            float newX = _baseX + _currentSurgeX + _knockbackOffsetX;
            _rb.MovePosition(new Vector3(newX, pos.y, newZ));
        }

        // --- PUBLIC COMMAND API ---

        /// <summary>
        /// Nhận 1 chuỗi tối đa 3 lệnh từ 1 comment chat (GDD mục 5.2 "Command Queue FIFO").
        /// Nếu gửi quá 3 lệnh, chỉ 3 lệnh đầu tiên được nhận, phần dư bị huỷ bỏ.
        /// </summary>
        public void ExecuteCommands(List<string> commands)
        {
            if (commands == null) return;

            int count = Mathf.Min(commands.Count, _maxCommandsPerBatch);
            for (int i = 0; i < count; i++)
            {
                EnqueueCommand(commands[i]);
            }
        }

        /// <summary>Nhận đúng 1 lệnh — dùng cho test phím A/D hoặc chat chỉ gửi 1 lệnh duy nhất.</summary>
        public void ExecuteSingleCommand(string command)
        {
            EnqueueCommand(command);
        }

        private void EnqueueCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            string normalized = command.Trim().ToLowerInvariant();

            if (_commandQueue.Count >= _maxCommandsPerBatch)
            {
                Debug.LogWarning($"[ChatLaneRunner] Queue đầy, huỷ lệnh dư: {normalized}");
                return;
            }

            _commandQueue.Enqueue(normalized);
        }

        /// <summary>
        /// Xử lý 1 lệnh trong queue mỗi khi cooldown của lệnh trước đã hết — đảm bảo FIFO,
        /// lệnh sau không chồng lên lệnh trước khi đang lách làn/đang tăng-giảm tốc.
        /// </summary>
        private void ProcessCommandQueue()
        {
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
            
            // Lọc các từ khóa nhảy
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
                    SetLane(0); // Làn 1: Trái cùng (Z = +3.0)
                    _commandCooldownTimer = _laneChangeSmoothTime;
                    break;

                case "2":
                    SetLane(1); // Làn 2: Giữa (Z = 0.0)
                    _commandCooldownTimer = _laneChangeSmoothTime;
                    break;

                case "3":
                    SetLane(2); // Làn 3: Phải cùng (Z = -3.0)
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

            if (_factionManager == null)
            {
                // Chưa nối FactionTugOfWarManager trong scene (vd. scene test riêng) — không khoá,
                // cho phép thao tác như cũ để không chặn việc test các phần khác.
                return true;
            }

            return _factionManager.TrySpendFanEnergy(cost);
        }

        private void TriggerJump()
        {
            if (_runnerController == null || !_runnerController.IsGrounded || _runnerController.IsDucking)
            {
                return;
            }

            // Kiểm tra + trừ năng lượng TRƯỚC khi nhảy thật sự xảy ra: không đủ (hoặc hết) năng
            // lượng Fan thì khoá nhảy, Runner buộc phải chịu va chạm nếu phía trước có chướng ngại.
            if (!TryPayControlEnergy(_jumpEnergyCost))
            {
                Debug.LogWarning("[ChatLaneRunner] Hết năng lượng Fan — khoá nhảy.");
                return;
            }

            _runnerController.PerformJump();
            // Không tự SetTrigger("Jump") ở đây nữa — RunnerController.PerformJump() đã tự bắn
            // Trigger "Jump" cho Animator, gọi lại ở đây sẽ set trigger 2 lần thừa mỗi lần nhảy.
        }

        private void SetLane(int targetIndex)
        {
            int clampedIndex = Mathf.Clamp(targetIndex, 0, _laneZPositions.Length - 1);
            if (clampedIndex == _currentLaneIndex)
            {
                return;
            }

            // Chỉ trừ năng lượng khi lane đích THỰC SỰ khác lane hiện tại (đã check ở trên).
            if (!TryPayControlEnergy(_laneChangeEnergyCost))
            {
                Debug.LogWarning("[ChatLaneRunner] Hết năng lượng Fan — khoá đổi làn.");
                return;
            }

            _currentLaneIndex = clampedIndex;
        }

        private void ChangeLane(int direction)
        {
            int newIndex = Mathf.Clamp(_currentLaneIndex + direction, 0, _laneZPositions.Length - 1);
            if (newIndex == _currentLaneIndex)
            {
                return;
            }

            if (!TryPayControlEnergy(_laneChangeEnergyCost))
            {
                Debug.LogWarning("[ChatLaneRunner] Hết năng lượng Fan — khoá đổi làn.");
                return;
            }

            _currentLaneIndex = newIndex;
        }

        private void TriggerFast()
        {
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
        }

        // Da go bo phan tru nang luong Fan theo thoi gian (GDD v1.4.1: fast mien phi). Ham nay chi
        // con giu 2 viec: (1) huy fast ngay khi Runner dang bi va cham / knockback / recovering,
        // tru khi dang co Sprint Buff; (2) duy tri toc do fast khi dang co Sprint Buff dang chay.
        private void UpdateFastEnergyDrain()
        {
            if (!_isFastRunning) return;

            // Nếu đang va chạm (ReverseKnockback hoặc Recovery) hoặc Runner đang bị hit:
            // Tuyệt đối KHÔNG can thiệp đè tốc độ thế giới
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

            // Nếu đang có Sprint Buff (Bình Tăng Tốc), duy trì tốc độ fast liên tục
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

            // Nếu đang va chạm/hồi phục tốc độ thì hủy fast ngay
            if (_speedManager != null && (_speedManager.IsRecovering || _speedManager.IsReverseKnockingBack))
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
            _speedManager?.CancelCommandSpeed();
        }

        /// <summary>
        /// Kích hoạt quà Bình Tăng Tốc (Sprint Buff) cho phe Fan trong duration giây (mặc định 30s).
        /// Bứt tốc 18m/s liên tục mà KHÔNG trừ năng lượng Fan.
        /// </summary>
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

        /// <summary>
        /// [DEBUG] Bật/tắt tự do Sprint Buff không giới hạn thời gian (dành cho QA / test nhanh).
        /// </summary>
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
            float dur = duration > 0f ? duration : _freeControlBuffDuration;
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
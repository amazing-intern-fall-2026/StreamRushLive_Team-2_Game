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
        [Tooltip("Tốc độ tiêu hao năng lượng Fan mỗi giây khi chạy fast (VD: 25/s thì 100 năng lượng chạy 4s, 50 năng lượng chạy 2s và giảm hết về 0).")]
        [SerializeField] private float _fastEnergyDrainPerSecond = 25f;
        [Tooltip("Tham chiếu FactionTugOfWarManager để kiểm tra và trừ năng lượng Fan.")]
        [SerializeField] private FactionTugOfWarManager _factionManager;

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
            float dist = distance > 0f ? distance : _knockbackDistance;
            _knockbackTotalDuration = duration > 0f ? duration : _knockbackDuration;
            _knockbackTimer = 0f;
            _knockbackStartOffset = -dist; // Đẩy lùi về phía sau (-X)
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
        private Rigidbody _rb;
        private Animator _animator;
        private Camera _mainCamera;
        private float _baseFov = 60f;
        private float _baseX;
        private float _currentSurgeX;

        private bool _isFastRunning;
        private float _fastTimer;
        private float _fastEnergyAccumulator;

        private void Awake()
        {
            _speedManager = FindFirstObjectByType<WorldSpeedManager>() ?? WorldSpeedManager.Instance;
            _collisionHandler = GetComponent<RunnerCollisionHandler>();
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
            if (currentSpeed > baseSpeed + 2f)
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

        // ============================================================
        // PUBLIC API — gọi từ bộ lọc chat (chưa nối) hoặc HandleDebugKeys
        // ============================================================

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
            switch (command)
            {
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

        private void SetLane(int targetIndex)
        {
            int clampedIndex = Mathf.Clamp(targetIndex, 0, _laneZPositions.Length - 1);
            if (clampedIndex == _currentLaneIndex)
            {
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

            _currentLaneIndex = newIndex;
        }

        private void TriggerFast()
        {
            if (_factionManager == null)
            {
                _factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            }

            if (_factionManager != null && _factionManager.FanLikes <= 0)
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

            // Nếu đang va chạm/hồi phục tốc độ thì hủy fast ngay
            if (_speedManager != null && _speedManager.IsRecovering)
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
    }
}
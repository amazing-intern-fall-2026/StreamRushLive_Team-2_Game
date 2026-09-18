namespace SteamRush.Features.Runner
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using SteamRush.Track;

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
        [Tooltip("Vị trí trục Z của 3 làn: Trái / Giữa / Phải. GDD v1.3 = -3.0 / 0.0 / +3.0.")]
        [SerializeField] private float[] _laneZPositions = { -3f, 0f, 3f };
        [Tooltip("Thời gian lách làn mượt mà. GDD v1.3 = 0.2s, dùng Mathf.SmoothDamp.")]
        [SerializeField] private float _laneChangeSmoothTime = 0.2f;

        [Header("Command Queue")]
        [Tooltip("Số lệnh tối đa được nhận từ 1 lần gọi ExecuteCommands (1 comment chat).")]
        [SerializeField] private int _maxCommandsPerBatch = 3;
        [Tooltip("Thời gian chờ tối thiểu giữa 2 lệnh fast/slow liên tiếp trong queue (giây).")]
        [SerializeField] private float _nonLaneCommandDelay = 0.1f;

        [Header("Speed Commands (fast / slow)")]
        [Tooltip("Tốc độ cuộn thế giới khi nhận lệnh fast. GDD v1.3 = 10.0 m/s.")]
        [SerializeField] private float _fastTargetSpeed = 10f;
        [Tooltip("Thời gian duy trì hiệu ứng fast trước khi tự trở về bình thường. GDD v1.3 = 3.0s.")]
        [SerializeField] private float _fastDuration = 3f;
        [Tooltip("Tốc độ cuộn thế giới khi nhận lệnh slow (mức an toàn để né xe).")]
        [SerializeField] private float _slowTargetSpeed = 4f;
        [Tooltip("Thời gian duy trì hiệu ứng slow trước khi tự trở về bình thường.")]
        [SerializeField] private float _slowDuration = 3f;

        private int _currentLaneIndex = 1; // bắt đầu ở làn giữa
        private float _zVelocity; // bắt buộc phải có cho Mathf.SmoothDamp, lưu vận tốc giữa các frame

        private readonly Queue<string> _commandQueue = new Queue<string>();
        private float _commandCooldownTimer;

        private WorldSpeedManager _speedManager;
        private RunnerCollisionHandler _collisionHandler; // chỉ tham chiếu, KHÔNG sửa logic bên trong
        private Rigidbody _rb;

        private void Awake()
        {
            _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            _collisionHandler = GetComponent<RunnerCollisionHandler>();
            _rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            // Chạy ở Start() (không phải Awake()) để CHẮC CHẮN RunnerController.Awake() đã set
            // xong constraints trước — nếu làm ở Awake(), thứ tự Awake() giữa 2 script trên cùng
            // GameObject không được đảm bảo, có thể bị RunnerController ghi đè lại constraints
            // SAU khi mình vừa mở khoá, làm mất tác dụng.
            _rb.constraints &= ~RigidbodyConstraints.FreezePositionZ;

            // Đặt vị trí Z ban đầu đúng làn giữa, tránh Runner spawn lệch làn nếu Scene đặt sai.
            Vector3 startPos = transform.position;
            startPos.z = _laneZPositions[_currentLaneIndex];
            _rb.position = startPos;
        }

        private void Update()
        {
            HandleDebugKeys();
            ProcessCommandQueue();
        }

        private void FixedUpdate()
        {
            UpdateLaneMovement();
        }

        /// <summary>
        /// Bắt tạm phím A/D để tự test lách làn trên máy khi chưa kết nối bộ lọc chat thật.
        /// </summary>
        private void HandleDebugKeys()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.aKey.wasPressedThisFrame)
            {
                ExecuteSingleCommand("left");
            }

            if (Keyboard.current.dKey.wasPressedThisFrame)
            {
                ExecuteSingleCommand("right");
            }
        }

        /// <summary>
        /// Trượt mềm trục Z về đúng vị trí làn hiện tại — chạy trong FixedUpdate và dùng
        /// RB.MovePosition() (không ghi transform.position trực tiếp) để không bị Physics Engine
        /// kéo ngược lại, vì Rigidbody vẫn là non-kinematic (Y vẫn rơi/nhảy bình thường).
        /// </summary>
        private void UpdateLaneMovement()
        {
            float targetZ = _laneZPositions[_currentLaneIndex];
            Vector3 pos = _rb.position;
            float newZ = Mathf.SmoothDamp(pos.z, targetZ, ref _zVelocity, _laneChangeSmoothTime);
            _rb.MovePosition(new Vector3(pos.x, pos.y, newZ));
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

            // Đoạn 1 - Điểm nhận lệnh điều khiển
            Debug.Log($"[ChatLaneRunner] Nhận lệnh: {normalized}");

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

                case "fast":
                    TriggerFast();
                    _commandCooldownTimer = _nonLaneCommandDelay;
                    break;

                case "slow":
                    TriggerSlow();
                    _commandCooldownTimer = _nonLaneCommandDelay;
                    break;

                default:
                    Debug.LogWarning($"[ChatLaneRunner] Lệnh không hợp lệ, bỏ qua: {command}");
                    break;
            }
        }

        private void ChangeLane(int direction)
        {
            int newIndex = Mathf.Clamp(_currentLaneIndex + direction, 0, _laneZPositions.Length - 1);

            if (newIndex == _currentLaneIndex)
            {
                Debug.Log("[ChatLaneRunner] Đã ở làn ngoài cùng, khoá lại không đổi làn được.");
                return;
            }

            _currentLaneIndex = newIndex;
        }

        private void TriggerFast()
        {
            // Đoạn 2 - Điểm bứt tốc fast
            Debug.Log("[ChatLaneRunner] Tăng tốc (fast) — chưa nối thanh năng lượng thật.");
            _speedManager?.TriggerCommandSpeed(_fastTargetSpeed, _fastDuration);
        }

        private void TriggerSlow()
        {
            Debug.Log("[ChatLaneRunner] Giảm tốc (slow) về mức an toàn.");
            _speedManager?.TriggerCommandSpeed(_slowTargetSpeed, _slowDuration);
        }
    }
}
namespace SteamRush.Features.Runner
{
    using UnityEngine;
    using UnityEngine.InputSystem;
    using SteamRush.Track;

    /// Chịu trách nhiệm DUY NHẤT: đọc Input (New Input System) và gọi các hàm public tương ứng
    /// trên RunnerController / WorldSpeedManager. Nhân vật đứng yên (mô hình treadmill) nên KHÔNG
    /// còn đọc phím di chuyển ngang — chỉ còn Nhảy, Cúi/Slide và Sprint. Tách biệt khỏi
    /// RunnerController để dễ đổi input scheme (mobile, gamepad...) sau này mà không đụng logic
    /// vật lý hay logic tốc độ thế giới.
    ///
    /// Nhảy: RunnerController.PerformJump() giờ tự dùng đúng vận tốc GDD (+6.5 m/s) nội bộ,
    ///   không còn nhận tham số lực nhảy từ đây nữa .
    /// Ctrl / S / ↓: vừa Cúi (hitbox) vừa điều khiển World Speed qua WorldSpeedManager.
    ///   - Nhấp nhả (thả trước ngưỡng _slideHoldThreshold) -> Tap: BeginSlideTap() (world speed
    ///     giảm tức thì -50%, tự hồi sau 0.8s) VÀ hitbox hạ đúng _slideTapDuckDuration (0.8s) cố
    ///     định — không phụ thuộc thời gian giữ phím thực tế.
    ///   - Đè giữ quá ngưỡng -> Hold: HoldSlide() mỗi frame (hãm dần về 0) + hitbox hạ trong suốt
    ///     lúc giữ, nhả ra -> ReleaseSlide() (tăng mượt lại bình thường) + hitbox đứng thẳng ngay.
    /// Shift / E: giữ để Sprint (HoldSprint()), nhả để dừng (ReleaseSprint()). Không tự động.

    [RequireComponent(typeof(RunnerController))]
    public class RunnerInputHandler : MonoBehaviour
    {
        [Header("Slide Settings")]
        [Tooltip("Thời gian giữ phím (giây) để phân biệt Tap (nhấp nhả) và Hold (đè giữ).")]
        [SerializeField] private float _slideHoldThreshold = 0.15f;
        [Tooltip("Thời gian hitbox hạ CỐ ĐỊNH khi Tap (giây). GDD v1.2 = 0.8s — giữ đồng bộ với Slide Tap Duration bên WorldSpeedManager.")]
        [SerializeField] private float _slideTapDuckDuration = 0.8f;

        private RunnerController _controller;
        private WorldSpeedManager _speedManager;

        private float _slideKeyTimer;
        private bool _slideKeyHeldLastFrame;
        private bool _slideRegisteredAsHold;

        // Đếm ngược riêng cho hitbox khi Tap — độc lập với trạng thái phím thực tế,
        // để hitbox luôn hạ đủ _slideTapDuckDuration dù bấm-thả rất nhanh.
        private float _tapDuckTimer;

        private void Awake()
        {
            _controller = GetComponent<RunnerController>();
            _speedManager = FindFirstObjectByType<WorldSpeedManager>();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            HandleJump();
            HandleSlideAndDuck();
            HandleSprint();
        }

        private void HandleJump()
        {
            bool jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame
                || Keyboard.current.wKey.wasPressedThisFrame
                || Keyboard.current.upArrowKey.wasPressedThisFrame;

            if (jumpPressed)
            {
                _controller.PerformJump();
            }
        }

        private void HandleSlideAndDuck()
        {
            bool slideKeyHeldNow = Keyboard.current.sKey.isPressed
                || Keyboard.current.downArrowKey.isPressed
                || Keyboard.current.leftCtrlKey.isPressed
                || Keyboard.current.rightCtrlKey.isPressed;

            if (slideKeyHeldNow)
            {
                // Đang có 1 lần bấm thật sự (Hold đang diễn ra) -> huỷ hẹn giờ Tap cũ nếu còn sót lại
                _tapDuckTimer = 0f;

                if (!_slideKeyHeldLastFrame)
                {
                    // Vừa mới bấm xuống trong frame này -> reset bộ đếm phân biệt Tap/Hold
                    _slideKeyTimer = 0f;
                    _slideRegisteredAsHold = false;
                }

                _slideKeyTimer += Time.deltaTime;
                if (!_slideRegisteredAsHold && _slideKeyTimer >= _slideHoldThreshold)
                {
                    _slideRegisteredAsHold = true;
                }

                if (_slideRegisteredAsHold)
                {
                    _speedManager?.HoldSlide();
                }

                // Hitbox: cúi khi đứng đất, ép rơi thẳng khi đang trên không
                if (_controller.IsGrounded)
                {
                    _controller.SetDucking(true);
                }
                else
                {
                    _controller.PerformFastFall();
                }
            }
            else
            {
                if (_slideKeyHeldLastFrame)
                {
                    // Vừa nhả phím trong frame này
                    if (_slideRegisteredAsHold)
                    {
                        // Hold vừa kết thúc -> đứng thẳng lại ngay, tốc độ tăng mượt lại bình thường
                        _speedManager?.ReleaseSlide();
                        _controller.SetDucking(false);
                    }
                    else
                    {
                        // Đây là một cú Tap: bắt đầu đếm ngược 0.8s CỐ ĐỊNH cho hitbox, không phụ
                        // thuộc bạn giữ phím bao lâu (kể cả 1 frame cũng vẫn đủ 0.8s theo GDD).
                        _speedManager?.BeginSlideTap();
                        _tapDuckTimer = _slideTapDuckDuration;
                    }
                }

                // Xử lý đếm ngược hitbox Tap (nếu có) — chạy độc lập với trạng thái phím
                if (_tapDuckTimer > 0f)
                {
                    _tapDuckTimer -= Time.deltaTime;
                    _controller.SetDucking(true);
                }
                else
                {
                    _controller.SetDucking(false);
                }
            }

            _slideKeyHeldLastFrame = slideKeyHeldNow;
        }

        private void HandleSprint()
        {
            bool sprintHeldNow = Keyboard.current.leftShiftKey.isPressed
                || Keyboard.current.rightShiftKey.isPressed
                || Keyboard.current.eKey.isPressed;

            if (sprintHeldNow)
            {
                _speedManager?.HoldSprint();
            }
            else
            {
                _speedManager?.ReleaseSprint();
            }
        }
    }
}
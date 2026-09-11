namespace SteamRush.Features.Runner
{
    using UnityEngine;
    using UnityEngine.InputSystem;


    /// Chịu trách nhiệm DUY NHẤT: đọc Input (New Input System) và gọi các hàm public tương ứng
    /// trên RunnerController. Nhân vật đứng yên (mô hình treadmill) nên KHÔNG còn đọc phím
    /// di chuyển ngang — chỉ còn Nhảy và Cúi. Tách biệt khỏi RunnerController để dễ đổi input
    /// scheme (mobile, gamepad...) sau này mà không đụng vào logic vật lý.

    [RequireComponent(typeof(RunnerController))]
    public class RunnerInputHandler : MonoBehaviour
    {
        [Header("Nhảy")]
        [SerializeField] private float _jumpForce = 7f;

        private RunnerController _controller;

        private void Awake()
        {
            _controller = GetComponent<RunnerController>();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            bool jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame
                || Keyboard.current.wKey.wasPressedThisFrame
                || Keyboard.current.upArrowKey.wasPressedThisFrame;

            if (jumpPressed)
            {
                _controller.PerformJump(_jumpForce);
            }

            bool downHeld = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;

            if (_controller.IsGrounded)
            {
                _controller.SetDucking(downHeld);
            }
            else if (downHeld)
            {
                // Đang ở trên không (nhảy đơn hoặc nhảy đúp đều được, không phân biệt) —
                // bấm Xuống sẽ ép rơi thẳng xuống ngay, không phải Cúi.
                _controller.PerformFastFall();
            }
        }
    }
}
namespace SteamRush.Features.Runner
{
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.EventSystems;
    using SteamRush.Track;

    /// <summary>
    /// Single Responsibility: reads player input and delegates to RunnerController / WorldSpeedManager.
    /// Handles Jump, Slide/Duck (Tap vs Hold), and Sprint mechanics.
    /// </summary>

    [RequireComponent(typeof(RunnerMovement))]
    public class RunnerInputHandler : MonoBehaviour
    {
        [Header("Slide Settings")]
        [Tooltip("Hold threshold in seconds distinguishing tap from hold slide.")]
        [SerializeField] private float _slideHoldThreshold = 0.15f;
        [Tooltip("Fixed duck hitbox duration for tap slide (default: 0.8s).")]
        [SerializeField] private float _slideTapDuckDuration = 0.8f;

        private RunnerMovement _movement;
        private WorldSpeedManager _speedManager;
        private ChatLaneRunnerController _chatLaneRunner;

        private float _slideKeyTimer;
        private bool _slideKeyHeldLastFrame;
        private bool _slideRegisteredAsHold;

        // Independent countdown for tap slide hitbox
        private float _tapDuckTimer;

        private void Awake()
        {
            _movement = GetComponent<RunnerMovement>();
            _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            _chatLaneRunner = GetComponent<ChatLaneRunnerController>();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            // Ignore input when typing in an active UI InputField
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                return;
            }

            // Post finish line archway (GDD v1.4.1 - Victory Celebration): ignore manual QA inputs
            // by checking existing IsVictoryStopped state flag.
            if (_speedManager != null && _speedManager.IsVictoryStopped)
            {
                return;
            }

            HandleLaneChange();
            HandleJump();
            HandleSlideAndDuck();
            HandleSprint();
        }

        private void HandleLaneChange()
        {
            if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                if (_chatLaneRunner != null) _chatLaneRunner.ChangeLane(-1);
                else _movement?.ShiftLane(-1);
            }
            else if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                if (_chatLaneRunner != null) _chatLaneRunner.ChangeLane(1);
                else _movement?.ShiftLane(1);
            }
            else if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                if (_chatLaneRunner != null) _chatLaneRunner.SetLane(0);
                else _movement?.MoveToLane(0);
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                if (_chatLaneRunner != null) _chatLaneRunner.SetLane(1);
                else _movement?.MoveToLane(1);
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
            {
                if (_chatLaneRunner != null) _chatLaneRunner.SetLane(2);
                else _movement?.MoveToLane(2);
            }
        }

        private void HandleJump()
        {
            bool jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame
                || Keyboard.current.wKey.wasPressedThisFrame
                || Keyboard.current.upArrowKey.wasPressedThisFrame;

            if (jumpPressed)
            {
                if (_chatLaneRunner != null)
                {
                    _chatLaneRunner.TriggerJump();
                }
                else
                {
                    _movement.PerformJump();
                }
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
                // Active hold in progress: cancel previous tap countdown
                _tapDuckTimer = 0f;

                if (!_slideKeyHeldLastFrame)
                {
                    // Key pressed this frame: reset tap/hold differentiator
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

                // Hitbox: duck when grounded, fast fall when airborne
                if (_movement.IsGrounded)
                {
                    _movement.SetDucking(true);
                }
                else
                {
                    _movement.PerformFastFall();
                }
            }
            else
            {
                if (_slideKeyHeldLastFrame)
                {
                    // Key released this frame
                    if (_slideRegisteredAsHold)
                    {
                        // Hold ended: stand upright, resume normal speed
                        _speedManager?.ReleaseSlide();
                        _movement.SetDucking(false);
                    }
                    else
                    {
                        // Tap detected: start fixed duration hitbox ducking
                        _speedManager?.BeginSlideTap();
                        _tapDuckTimer = _slideTapDuckDuration;
                    }
                }

                // Process tap slide countdown independently of key state
                if (_tapDuckTimer > 0f)
                {
                    _tapDuckTimer -= Time.deltaTime;
                    _movement.SetDucking(true);
                }
                else
                {
                    _movement.SetDucking(false);
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
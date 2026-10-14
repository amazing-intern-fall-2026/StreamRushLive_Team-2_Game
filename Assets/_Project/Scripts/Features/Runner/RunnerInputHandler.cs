namespace SteamRush.Features.Runner
{
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.EventSystems;
    using SteamRush.Track;

    /// <summary>
    /// Single Responsibility: reads player input and delegates to RunnerController / WorldSpeedManager.
    /// Handles Lane Change, Jump, and Sprint mechanics.
    /// </summary>
    [RequireComponent(typeof(RunnerMovement))]
    public class RunnerInputHandler : MonoBehaviour
    {
        private RunnerMovement _movement;
        private WorldSpeedManager _speedManager;
        private ChatLaneRunnerController _chatLaneRunner;

        [Header("Manual Keyboard Controls")]
        [Tooltip("Allow player to manually control Runner via keyboard (A/D/Space). Configured in PreGameConfig Advanced Settings.")]
        [SerializeField] private bool _enableKeyboardControl = false;
        public bool EnableKeyboardControl
        {
            get => _enableKeyboardControl;
            set => _enableKeyboardControl = value;
        }

        private void Awake()
        {
            _movement = GetComponent<RunnerMovement>();
            _speedManager = FindFirstObjectByType<WorldSpeedManager>();
            _chatLaneRunner = GetComponent<ChatLaneRunnerController>();
        }

        private void OnDisable()
        {
            if (_movement != null && _movement.IsDucking)
            {
                _movement.SetDucking(false);
            }
        }

        private void Update()
        {
            if (!_enableKeyboardControl) return;
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
using UnityEngine;
using UnityEngine.InputSystem;

namespace SteamRush.Features.Runner
{
    [RequireComponent(typeof(PlayerCore))]
    public class PlayerInputFeature : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _jumpForce = 7f;

        private PlayerCore _core;

        private void Start()
        {
            _core = GetComponent<PlayerCore>();
        }

        private void Update()
        {
            if (Keyboard.current == null || _core == null)
            {
                return;
            }

            // Nhảy: W, Mũi tên lên, hoặc Phím cách (Space)
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.wKey.wasPressedThisFrame ||
                Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                _core.PerformJump(_jumpForce);
            }

            // Cúi / Trượt: S hoặc Mũi tên xuống
            if (Keyboard.current.sKey.wasPressedThisFrame ||
                Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                _core.PerformSlide();
            }
        }
    }
}
using System.Collections;
using UnityEngine;

namespace SteamRush.Features.Runner
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerCore : MonoBehaviour
    {
        public Rigidbody RB { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsSliding { get; private set; }

        [Header("Jump Settings")]
        [SerializeField] private int _maxJumps = 1;
        [SerializeField] private float _fallMultiplier = 2.5f;

        [Header("Slide Settings")]
        [SerializeField] private float _slideDuration = 0.8f;
        [SerializeField] private float _slideHeightMultiplier = 0.5f;

        private int _jumpCount = 0;
        private CapsuleCollider _capsuleCollider;
        private float _originalHeight;
        private Vector3 _originalCenter;
        private Coroutine _slideCoroutine;

        private void Awake()
        {
            RB = GetComponent<Rigidbody>();
            RB.constraints = RigidbodyConstraints.FreezePositionX |
                             RigidbodyConstraints.FreezePositionZ |
                             RigidbodyConstraints.FreezeRotationX |
                             RigidbodyConstraints.FreezeRotationY |
                             RigidbodyConstraints.FreezeRotationZ;

            _capsuleCollider = GetComponent<CapsuleCollider>();
            if (_capsuleCollider != null)
            {
                _originalHeight = _capsuleCollider.height;
                _originalCenter = _capsuleCollider.center;
            }
        }

        private void FixedUpdate()
        {
            // Tăng tốc rơi khi vận tốc trục Y âm
            if (RB.linearVelocity.y < 0)
            {
                RB.linearVelocity += Vector3.up * Physics.gravity.y * (_fallMultiplier - 1) * Time.fixedDeltaTime;
            }
        }

        public void PerformJump(float jumpForce)
        {
            if (IsSliding)
            {
                StopSlide();
            }

            if (_jumpCount < _maxJumps)
            {
                RB.linearVelocity = new Vector3(0f, 0f, 0f);
                RB.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                _jumpCount++;
                IsGrounded = false;
            }
        }

        public void PerformSlide()
        {
            if (!IsGrounded || IsSliding)
            {
                return;
            }

            if (_slideCoroutine != null)
            {
                StopCoroutine(_slideCoroutine);
            }

            _slideCoroutine = StartCoroutine(SlideRoutine());
        }

        private IEnumerator SlideRoutine()
        {
            IsSliding = true;

            if (_capsuleCollider != null)
            {
                _capsuleCollider.height = _originalHeight * _slideHeightMultiplier;
                _capsuleCollider.center = new Vector3(_originalCenter.x, _originalCenter.y * _slideHeightMultiplier, _originalCenter.z);
            }

            yield return new WaitForSeconds(_slideDuration);

            StopSlide();
        }

        private void StopSlide()
        {
            if (_slideCoroutine != null)
            {
                StopCoroutine(_slideCoroutine);
                _slideCoroutine = null;
            }

            if (_capsuleCollider != null)
            {
                _capsuleCollider.height = _originalHeight;
                _capsuleCollider.center = _originalCenter;
            }

            IsSliding = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Ground"))
            {
                IsGrounded = true;
                _jumpCount = 0;
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision.gameObject.CompareTag("Ground"))
            {
                IsGrounded = false;
            }
        }
    }
}
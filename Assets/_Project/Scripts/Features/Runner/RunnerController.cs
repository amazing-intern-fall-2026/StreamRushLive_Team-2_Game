namespace SteamRush.Features.Runner
{
    using UnityEngine;
    using StreamRushLive.Features.Spawning;

    /// <summary>
    /// Physics controller for Runner: handles jumping, ducking, fast fall, and ground check.
    /// Runner remains stationary on horizontal axes (treadmill paradigm).
    /// Features forgiving hitboxes scaled from 3D mesh bounds (GDD v1.2).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RunnerController : MonoBehaviour
    {
        [Header("Jump")]
        [Tooltip("Vertical jump velocity (default: +6.5 m/s).")]
        [SerializeField] private float _jumpVelocity = 6.5f;

        [Header("Gravity")]
        [Tooltip("Upward gravity rate in m/s² (positive value).")]
        [SerializeField] private float _risingGravity = 9.6f;
        [Tooltip("Downward gravity rate during fall in m/s² (positive value).")]
        [SerializeField] private float _fallingGravity = 18f;

        [Header("Crouch / Duck")]
        [Tooltip("Collider height ratio when ducking (e.g. 0.5 = half height).")]
        [SerializeField, Range(0.1f, 1f)] private float _duckHeightRatio = 0.5f;

        [Header("Fast Fall")]
        [Tooltip("Downward velocity applied when pressing down in air.")]
        [SerializeField] private float _fastFallSpeed = 20f;

        [Header("Ground Collision")]
        [SerializeField] private string _groundTag = "Ground";
        [Tooltip("Raycast check distance below collider bottom.")]
        [SerializeField] private float _groundCheckDistance = 0.25f;


        [Header("Forgiving Hitbox")]
        [Tooltip("Automatically shrink collider relative to 3D mesh on Awake.")]
        [SerializeField] private bool _autoApplyForgivingHitbox = true;
        [Tooltip("Collider scale ratio relative to mesh bounds (default: 0.85).")]
        [SerializeField, Range(0.5f, 1f)] private float _hitboxToleranceRatio = 0.85f;

        public Rigidbody RB { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsDucking { get; private set; }

        private BoxCollider _boxCollider;
        private CapsuleCollider _capsuleCollider;
        private Animator _animator;
        private Vector3 _standingBoxSize;
        private Vector3 _standingBoxCenter;
        private float _standingCapsuleHeight;
        private Vector3 _standingCapsuleCenter;
        private Transform _visualRoot;
        private Vector3 _standingVisualScale = Vector3.one;

        private bool _isCollidingWithGround;
        private float _jumpCooldownTimer;

        private void Awake()
        {
            RB = GetComponent<Rigidbody>();

            // Disable Unity gravity - manual asymmetric gravity applied in FixedUpdate regardless of physics settings.
            RB.useGravity = false;

            _animator = GetComponent<Animator>();

            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }

            _visualRoot = transform.Find("Root");
            if (_visualRoot == null && transform.childCount > 0)
            {
                _visualRoot = transform.GetChild(0);
            }
            if (_visualRoot != null)
            {
                _standingVisualScale = _visualRoot.localScale;
            }

            _boxCollider = GetComponent<BoxCollider>();
            _capsuleCollider = GetComponent<CapsuleCollider>();

            if (_autoApplyForgivingHitbox)
            {
                ApplyForgivingHitbox();
            }

            // Cache upright dimensions after forgiving hitbox calculation
            if (_boxCollider != null)
            {
                _standingBoxSize = _boxCollider.size;
                _standingBoxCenter = _boxCollider.center;
            }

            if (_capsuleCollider != null)
            {
                _standingCapsuleHeight = _capsuleCollider.height;
                _standingCapsuleCenter = _capsuleCollider.center;
            }

            // Constrain horizontal position: world scrolls around stationary runner
            RB.constraints = RigidbodyConstraints.FreezePositionX
                | RigidbodyConstraints.FreezePositionZ
                | RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationY
                | RigidbodyConstraints.FreezeRotationZ;

            if (_boxCollider != null) _boxCollider.isTrigger = false;
            if (_capsuleCollider != null) _capsuleCollider.isTrigger = false;
        }

        /// <summary>
        /// Calculates forgiving hitbox from child renderers, scaled by _hitboxToleranceRatio (default: 85%).
        /// </summary>
        private void ApplyForgivingHitbox()
        {
            Renderer[] meshRenderers = GetComponentsInChildren<Renderer>();
            if (meshRenderers == null || meshRenderers.Length == 0) return;

            Bounds worldBounds = meshRenderers[0].bounds;
            for (int i = 1; i < meshRenderers.Length; i++)
            {
                worldBounds.Encapsulate(meshRenderers[i].bounds);
            }

            // Convert world bounds to local space without rotation complications
            Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
            Vector3 meshSize = worldBounds.size;
            float meshBottomY = localCenter.y - (meshSize.y / 2f);

            Vector3 shrunkSize = meshSize * _hitboxToleranceRatio;

            if (_boxCollider != null)
            {
                _boxCollider.size = shrunkSize;
                _boxCollider.center = new Vector3(localCenter.x, meshBottomY + (shrunkSize.y / 2f), localCenter.z);
            }

            if (_capsuleCollider != null)
            {
                float shrunkHeight = meshSize.y * _hitboxToleranceRatio;
                float shrunkRadius = Mathf.Max(meshSize.x, meshSize.z) * 0.5f * _hitboxToleranceRatio;

                _capsuleCollider.height = shrunkHeight;
                _capsuleCollider.radius = shrunkRadius;
                _capsuleCollider.center = new Vector3(localCenter.x, meshBottomY + (shrunkHeight / 2f), localCenter.z);
            }
        }

        private void Update()
        {
            UpdateGroundCheck();

            if (_animator != null)
                _animator.SetBool("isGrounded", IsGrounded);

            if (_knockbackTimer < _knockbackDuration)
            {
                _knockbackTimer += Time.deltaTime;
            }
        }

        private void FixedUpdate()
        {
            // Asymmetric manual gravity: risingGravity when moving up, fallingGravity when falling
            float gravity = RB.linearVelocity.y > 0f ? _risingGravity : _fallingGravity;
            RB.linearVelocity += Vector3.down * gravity * Time.fixedDeltaTime;
        }

        /// <summary>Performs jump with fixed vertical velocity (+6.5 m/s).</summary>
        public void PerformJump()
        {
            if (!IsGrounded || IsDucking) return;

            RB.linearVelocity = new Vector3(RB.linearVelocity.x, _jumpVelocity, 0f);
            _isCollidingWithGround = false;
            IsGrounded = false;
            _jumpCooldownTimer = 0.15f;

            if (_animator != null)
            {
                _animator.SetBool("isGrounded", false);
                _animator.ResetTrigger("Jump");
                _animator.SetTrigger("Jump");
            }
        }

        /// <summary>
        /// Fast fall: cancels upward momentum and drives runner directly downward.
        /// </summary>
        public void PerformFastFall()
        {
            if (IsGrounded) return;

            RB.linearVelocity = new Vector3(RB.linearVelocity.x, -_fastFallSpeed, RB.linearVelocity.z);
        }

        /// <summary>Toggles duck state: true = ducking, false = upright.</summary>
        public void SetDucking(bool isDucking)
        {
            if (IsDucking == isDucking) return;

            // Disallow starting duck while airborne; always allow standing up
            if (isDucking && !IsGrounded) return;

            IsDucking = isDucking;

            // 1. Adjust BoxCollider keeping grounded foot position constant
            if (_boxCollider != null)
            {
                float bottomY = _standingBoxCenter.y - (_standingBoxSize.y / 2f);
                float newHeight = isDucking ? _standingBoxSize.y * _duckHeightRatio : _standingBoxSize.y;
                _boxCollider.size = new Vector3(_standingBoxSize.x, newHeight, _standingBoxSize.z);
                _boxCollider.center = new Vector3(_standingBoxCenter.x, bottomY + (newHeight / 2f), _standingBoxCenter.z);
            }

            // 2. Adjust CapsuleCollider keeping grounded foot position constant
            if (_capsuleCollider != null)
            {
                float bottomY = _standingCapsuleCenter.y - (_standingCapsuleHeight / 2f);
                float newHeight = isDucking ? _standingCapsuleHeight * _duckHeightRatio : _standingCapsuleHeight;
                _capsuleCollider.height = newHeight;
                _capsuleCollider.center = new Vector3(_standingCapsuleCenter.x, bottomY + (newHeight / 2f), _standingCapsuleCenter.z);
            }

            // 3. Visual feedback: compress model height while ducking
            if (_visualRoot != null)
            {
                _visualRoot.localScale = isDucking
                    ? new Vector3(_standingVisualScale.x, _standingVisualScale.y * _duckHeightRatio, _standingVisualScale.z)
                    : _standingVisualScale;
            }
        }

        [Header("Knockback Settings (GDD v1.2)")]
        [SerializeField] private float _knockbackDistance = 1.8f;
        [SerializeField] private float _knockbackDuration = 0.45f;
        private float _knockbackTimer = 999f;

        public bool IsKnockingBack => _knockbackTimer < _knockbackDuration;

        /// <summary>
        /// Invoked on collision: records knockback state (GDD v1.2).
        /// </summary>
        public void ApplyKnockback(float distance = -1f, float duration = -1f)
        {
            _knockbackTimer = 0f;
            if (duration > 0f) _knockbackDuration = duration;
            if (distance > 0f) _knockbackDistance = distance;
        }

        /// <summary>
        /// Switches player collider to trigger during i-frames to let obstacles pass through.
        /// </summary>
        public void SetTriggerMode(bool isTrigger)
        {
            if (_boxCollider == null) _boxCollider = GetComponent<BoxCollider>();
            if (_capsuleCollider == null) _capsuleCollider = GetComponent<CapsuleCollider>();
            if (RB == null) RB = GetComponent<Rigidbody>();

            if (_boxCollider != null) _boxCollider.isTrigger = isTrigger;
            if (_capsuleCollider != null) _capsuleCollider.isTrigger = isTrigger;

            if (RB != null)
            {
                if (isTrigger)
                {
                    RB.linearVelocity = new Vector3(RB.linearVelocity.x, 0f, RB.linearVelocity.z);
                    RB.constraints |= RigidbodyConstraints.FreezePositionY;
                }
                else
                {
                    RB.constraints &= ~RigidbodyConstraints.FreezePositionY;
                }
            }
        }

        /// <summary>
        /// Enables trigger mode while maintaining vertical physics for Hyper Dash.
        /// </summary>
        public void SetHyperDashTriggerMode(bool isTrigger)
        {
            if (_boxCollider != null)
            {
                _boxCollider.isTrigger = isTrigger;
            }

            if (_capsuleCollider != null)
            {
                _capsuleCollider.isTrigger = isTrigger;
            }

            if (isTrigger)
            {
                // Maintain gravity and jump physics during Hyper Dash
                // Runner can still jump and fall normally.
                RB.useGravity = false;
                RB.constraints &= ~RigidbodyConstraints.FreezePositionY;
            }
            else
            {
                RB.constraints &= ~RigidbodyConstraints.FreezePositionY;
                RB.useGravity = false;
            }
        }
        private void UpdateGroundCheck()
        {
            if (_jumpCooldownTimer > 0f)
            {
                _jumpCooldownTimer -= Time.deltaTime;
                IsGrounded = false;
                return;
            }

            bool rayGrounded = CheckGroundRaycast();
            IsGrounded = rayGrounded || _isCollidingWithGround;
        }

        private bool CheckGroundRaycast()
        {
            Vector3 origin;
            float checkDist;

            if (_boxCollider != null)
            {
                float bottomY = _boxCollider.center.y - (_boxCollider.size.y * 0.5f);
                origin = transform.TransformPoint(new Vector3(_boxCollider.center.x, bottomY + 0.2f, _boxCollider.center.z));
                checkDist = 0.2f + _groundCheckDistance;
            }
            else
            {
                origin = transform.position + Vector3.up * 0.2f;
                checkDist = 0.2f + _groundCheckDistance;
            }

            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, checkDist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                GameObject hitObj = hits[i].collider.gameObject;
                if (hitObj == gameObject || hitObj.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (IsValidGround(hitObj))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsValidGround(GameObject obj)
        {
            if (obj.CompareTag("Obstacle") || obj.CompareTag("Buff"))
            {
                return false;
            }

            string n = obj.name;
            if (n.Contains("Barrier") || n.Contains("Obstacle") || n.Contains("Coin") || n.Contains("Buff"))
            {
                return false;
            }

            if (obj.GetComponentInParent<ObstacleBase>() != null || obj.GetComponentInParent<ItemBase>() != null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(_groundTag) && obj.CompareTag(_groundTag))
            {
                return true;
            }

            return true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsValidGround(collision.gameObject))
            {
                _isCollidingWithGround = true;
                IsGrounded = true;
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            if (IsValidGround(collision.gameObject))
            {
                _isCollidingWithGround = true;
                IsGrounded = true;
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (IsValidGround(collision.gameObject))
            {
                _isCollidingWithGround = false;
            }
        }
    }
}
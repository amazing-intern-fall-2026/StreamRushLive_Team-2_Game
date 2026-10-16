namespace SteamRush.Features.Runner
{
    using UnityEngine;
    using SteamRush.Track;
    using StreamRushLive.Features.Spawning;

    /// <summary>
    /// Comprehensive movement controller for the Runner:
    /// - Lateral 3-lane switching on the Z-axis (SmoothDamp).
    /// - Vertical physics on the Y-axis (asymmetric manual gravity, jump, duck/slide, fast fall).
    /// - Longitudinal surge & knockback on the X-axis.
    /// - Forgiving hitboxes scaled from 3D mesh bounds.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp", "RunnerController")]
    public class RunnerMovement : MonoBehaviour
    {
        [Header("Lane Settings (Z-Axis)")]
        [Tooltip("Z positions of the 3 lanes: Left (+3.0) / Middle (0.0) / Right (-3.0) looking toward +X.")]
        [SerializeField] private float[] _laneZPositions = { 3f, 0f, -3f };
        [Tooltip("Lane change transition duration (default: 0.2s). Uses Mathf.SmoothDamp.")]
        [SerializeField] private float _laneChangeSmoothTime = 0.2f;

        [Header("Jump (Y-Axis)")]
        [Tooltip("Vertical jump velocity (default: +6.5 m/s).")]
        [SerializeField] private float _jumpVelocity = 6.5f;

        [Header("Gravity (Y-Axis)")]
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

        [Header("Knockback Settings (X-Axis)")]
        [Tooltip("Runner knockback push distance in meters on obstacle collision.")]
        [SerializeField] private float _knockbackDistance = 1.8f;
        [Tooltip("Recovery duration to return to base position after knockback.")]
        [SerializeField] private float _knockbackDuration = 0.45f;

        public Rigidbody RB { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsDucking { get; private set; }

        public int CurrentLaneIndex => _currentLaneIndex;
        public float[] LaneZPositions => _laneZPositions;
        public int LaneCount => _laneZPositions != null ? _laneZPositions.Length : 0;
        public float LaneChangeSmoothTime => _laneChangeSmoothTime;

        public bool IsKnockingBack => _knockbackTimer < _knockbackTotalDuration;

        private int _currentLaneIndex = 1; // Middle lane by default
        private float _zVelocity;

        private float _baseX;
        private float _currentSurgeX;
        private float _knockbackOffsetX;
        private float _knockbackTimer = 999f;
        private float _knockbackTotalDuration = 0.45f;
        private float _knockbackStartOffset;

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

        private WorldSpeedManager _speedManager;

        private void Awake()
        {
            RB = GetComponent<Rigidbody>();
            RB.useGravity = false;

            _speedManager = FindFirstObjectByType<WorldSpeedManager>() ?? WorldSpeedManager.Instance;

            _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
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

            if (_boxCollider != null)
            {
                _standingBoxSize = _boxCollider.size;
                _standingBoxCenter = _boxCollider.center;
                _boxCollider.isTrigger = false;
            }

            if (_capsuleCollider != null)
            {
                _standingCapsuleHeight = _capsuleCollider.height;
                _standingCapsuleCenter = _capsuleCollider.center;
                _capsuleCollider.isTrigger = false;
            }

            _baseX = transform.position.x;

            // Constrain rotations only; position X/Y/Z are controlled by physics and MovePosition
            RB.constraints = RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationY
                | RigidbodyConstraints.FreezeRotationZ;
        }

        private void Start()
        {
            if (_laneZPositions != null && _laneZPositions.Length > _currentLaneIndex)
            {
                Vector3 startPos = transform.position;
                startPos.z = _laneZPositions[_currentLaneIndex];
                RB.position = startPos;
            }
        }

        private void ApplyForgivingHitbox()
        {
            Renderer[] meshRenderers = GetComponentsInChildren<Renderer>();
            if (meshRenderers == null || meshRenderers.Length == 0) return;

            Bounds worldBounds = meshRenderers[0].bounds;
            for (int i = 1; i < meshRenderers.Length; i++)
            {
                worldBounds.Encapsulate(meshRenderers[i].bounds);
            }

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
            {
                _animator.SetBool("isGrounded", IsGrounded);
            }

            UpdateKnockback(Time.deltaTime);
            UpdateSpeedSurge();
        }

        private void FixedUpdate()
        {
            // 1. Asymmetric manual vertical gravity
            float gravity = RB.linearVelocity.y > 0f ? _risingGravity : _fallingGravity;
            RB.linearVelocity += Vector3.down * gravity * Time.fixedDeltaTime;

            // 2. Smooth lane movement (Z-axis) & longitudinal surge/knockback (X-axis)
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            Vector3 pos = RB.position;

            float targetZ = (_laneZPositions != null && _laneZPositions.Length > _currentLaneIndex)
                ? _laneZPositions[_currentLaneIndex]
                : pos.z;

            float newZ = Mathf.SmoothDamp(pos.z, targetZ, ref _zVelocity, _laneChangeSmoothTime);
            float newX = _baseX + _currentSurgeX + _knockbackOffsetX;

            RB.MovePosition(new Vector3(newX, pos.y, newZ));
        }

        private void UpdateSpeedSurge()
        {
            if (_speedManager == null)
            {
                _speedManager = WorldSpeedManager.Instance ?? FindFirstObjectByType<WorldSpeedManager>();
                if (_speedManager == null) return;
            }

            float currentSpeed = _speedManager.CurrentSpeed;
            float baseSpeed = 8.0f;

            float targetSurgeX = 0f;
            bool isKnockedOrRecovering = _speedManager.IsReverseKnockingBack || _speedManager.IsRecovering || IsKnockingBack;
            if (!isKnockedOrRecovering && currentSpeed > baseSpeed + 2f)
            {
                targetSurgeX = 1.6f;
            }

            _currentSurgeX = Mathf.Lerp(_currentSurgeX, targetSurgeX, Time.deltaTime * 7f);
        }

        #region Lane Switching (Z-Axis)

        /// <summary>
        /// Moves runner to an explicit lane index (0 = Left, 1 = Middle, 2 = Right).
        /// Returns true if lane index changed.
        /// </summary>
        public bool MoveToLane(int targetIndex)
        {
            if (_laneZPositions == null || _laneZPositions.Length == 0) return false;

            int clampedIndex = Mathf.Clamp(targetIndex, 0, _laneZPositions.Length - 1);
            if (clampedIndex == _currentLaneIndex)
            {
                return false;
            }

            _currentLaneIndex = clampedIndex;
            AudioManager.Instance?.PlaySFX(SFXType.RunnerLaneSwitch);
            return true;
        }

        /// <summary>
        /// Shifts runner lane by direction offset (-1 or +1).
        /// Returns true if lane index changed.
        /// </summary>
        public bool ShiftLane(int direction)
        {
            return MoveToLane(_currentLaneIndex + direction);
        }

        /// <summary>
        /// Identifies the closest lane index to a given Z world coordinate.
        /// </summary>
        public int GetClosestLaneIndex(float currentZ)
        {
            if (_laneZPositions == null || _laneZPositions.Length == 0) return 0;

            int bestIndex = _currentLaneIndex;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _laneZPositions.Length; i++)
            {
                float dist = Mathf.Abs(currentZ - _laneZPositions[i]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        /// <summary>
        /// Snaps current lane index to closest physical lane position.
        /// </summary>
        public void SnapToClosestLane()
        {
            _currentLaneIndex = GetClosestLaneIndex(RB.position.z);
            _zVelocity = 0f;
        }

        #endregion

        #region Vertical Physics (Y-Axis)

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

            AudioManager.Instance?.PlaySFX(SFXType.RunnerJump);
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
            if (isDucking && !IsGrounded) return;

            IsDucking = isDucking;

            if (_boxCollider != null)
            {
                float bottomY = _standingBoxCenter.y - (_standingBoxSize.y / 2f);
                float newHeight = isDucking ? _standingBoxSize.y * _duckHeightRatio : _standingBoxSize.y;
                _boxCollider.size = new Vector3(_standingBoxSize.x, newHeight, _standingBoxSize.z);
                _boxCollider.center = new Vector3(_standingBoxCenter.x, bottomY + (newHeight / 2f), _standingBoxCenter.z);
            }

            if (_capsuleCollider != null)
            {
                float bottomY = _standingCapsuleCenter.y - (_standingCapsuleHeight / 2f);
                float newHeight = isDucking ? _standingCapsuleHeight * _duckHeightRatio : _standingCapsuleHeight;
                _capsuleCollider.height = newHeight;
                _capsuleCollider.center = new Vector3(_standingCapsuleCenter.x, bottomY + (newHeight / 2f), _standingCapsuleCenter.z);
            }

            if (_visualRoot != null)
            {
                _visualRoot.localScale = isDucking
                    ? new Vector3(_standingVisualScale.x, _standingVisualScale.y * _duckHeightRatio, _standingVisualScale.z)
                    : _standingVisualScale;
            }
        }

        #endregion

        #region Knockback & Collisions (X-Axis)

        public void ApplyKnockback(float distance = -1f, float duration = -1f)
        {
            _currentSurgeX = 0f;
            float dist = distance >= 0f ? distance : _knockbackDistance;
            _knockbackTotalDuration = duration > 0f ? duration : _knockbackDuration;
            _knockbackTimer = 0f;
            _knockbackStartOffset = -dist;
            _knockbackOffsetX = _knockbackStartOffset;
            _zVelocity = 0f;

            SnapToClosestLane();
            AudioManager.Instance?.PlaySFX(SFXType.RunnerKnockback);
        }

        private void UpdateKnockback(float dt)
        {
            if (_knockbackTimer < _knockbackTotalDuration)
            {
                _knockbackTimer += dt;
                float progress = Mathf.Clamp01(_knockbackTimer / _knockbackTotalDuration);
                float ease = 1f - (1f - progress) * (1f - progress);
                _knockbackOffsetX = Mathf.Lerp(_knockbackStartOffset, 0f, ease);
            }
            else
            {
                _knockbackOffsetX = 0f;
            }
        }

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

        public void SetHyperDashTriggerMode(bool isTrigger)
        {
            if (_boxCollider != null) _boxCollider.isTrigger = isTrigger;
            if (_capsuleCollider != null) _capsuleCollider.isTrigger = isTrigger;

            if (isTrigger)
            {
                RB.useGravity = false;
                RB.constraints &= ~RigidbodyConstraints.FreezePositionY;
            }
            else
            {
                RB.constraints &= ~RigidbodyConstraints.FreezePositionY;
                RB.useGravity = false;
            }
        }

        #endregion

        #region Ground Check

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

        #endregion
    }

    /// <summary>
    /// Backwards compatibility alias for RunnerMovement.
    /// Ensures all existing scene serializations and legacy references continue to work seamlessly.
    /// </summary>
    [System.Obsolete("RunnerController is obsolete. Please use RunnerMovement instead.")]
    public class RunnerController : RunnerMovement
    {
    }
}
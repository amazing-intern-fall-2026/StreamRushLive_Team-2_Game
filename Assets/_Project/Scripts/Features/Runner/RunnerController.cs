namespace SteamRush.Features.Runner
{
    using UnityEngine;
    using StreamRushLive.Features.Spawning;

    /// <summary>
    /// "Bộ não" vật lý của Runner: xử lý nhảy, cúi, rơi nhanh, ground-check
    /// và đẩy lùi khi va chạm. Nhân vật ĐỨNG YÊN theo trục ngang (mô hình treadmill) — không có
    /// hàm di chuyển ngang. Không đọc Input trực tiếp — RunnerInputHandler gọi các hàm public ở
    /// đây (Single Responsibility: Controller chỉ lo vật lý, không lo phím bấm).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RunnerController : MonoBehaviour
    {

        [Header("Fall")]
        [Tooltip("Gravity multiplier when moving upwards after jump.")]
        [SerializeField] private float _risingMultiplier = 1.5f;
        [Tooltip("Gravity multiplier when falling downwards.")]
        [SerializeField] private float _fallMultiplier = 2.5f;

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

        [Header("Knockback")]
        [SerializeField] private float _knockbackDistance = 1.5f;
        [SerializeField] private float _knockbackDuration = 0.3f;
        [Tooltip("Knockback direction upon obstacle collision.")]
        [SerializeField] private Vector3 _knockbackDirection = Vector3.left;

        public Rigidbody RB { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsDucking { get; private set; }

        private BoxCollider _boxCollider;
        private CapsuleCollider _capsuleCollider;
        private Vector3 _standingBoxSize;
        private Vector3 _standingBoxCenter;
        private float _standingCapsuleHeight;
        private Vector3 _standingCapsuleCenter;
        private Transform _visualRoot;
        private Vector3 _standingVisualScale = Vector3.one;

        private float _startX;
        private readonly KnockbackHandler _knockbackHandler = new KnockbackHandler();

        private bool _isCollidingWithGround;
        private float _jumpCooldownTimer;

        private void Awake()
        {
            RB = GetComponent<Rigidbody>();
            _startX = transform.position.x;

            Animator animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
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
            if (_boxCollider != null)
            {
                _standingBoxSize = _boxCollider.size;
                _standingBoxCenter = _boxCollider.center;
            }

            _capsuleCollider = GetComponent<CapsuleCollider>();
            if (_capsuleCollider != null)
            {
                _standingCapsuleHeight = _capsuleCollider.height;
                _standingCapsuleCenter = _capsuleCollider.center;
            }

            // Khoá cả Position X lẫn Z: nhân vật đứng yên tại chỗ theo cả 2 trục ngang — thế
            // giới (Track/Background) mới là thứ di chuyển, mô hình "treadmill" của endless
            // runner. Chỉ còn trục Y (nhảy/rơi) là tự do. Trục X sẽ được MỞ TẠM lúc bị knockback.
            RB.constraints = RigidbodyConstraints.FreezePositionX
                | RigidbodyConstraints.FreezePositionZ
                | RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationY
                | RigidbodyConstraints.FreezeRotationZ;
        }

        private void Update()
        {
            UpdateGroundCheck();
        }

        private void FixedUpdate()
        {
            // Đang rơi (vận tốc Y < 0): cộng thêm trọng lực để rơi nhanh, dứt khoát.
            if (RB.linearVelocity.y < 0f)
            {
                RB.linearVelocity += Vector3.up * Physics.gravity.y * (_fallMultiplier - 1f) * Time.fixedDeltaTime;
            }
            // Đang lên (vận tốc Y > 0): cũng cộng thêm trọng lực (ít hơn lúc rơi) để quỹ đạo lên
            // gọn hơn — không làm vậy sẽ có cảm giác "bay bổng" lúc đi lên trước khi rơi nhanh.
            else if (RB.linearVelocity.y > 0f)
            {
                RB.linearVelocity += Vector3.up * Physics.gravity.y * (_risingMultiplier - 1f) * Time.fixedDeltaTime;
            }

            ApplyKnockbackMotion();
        }

        public void PerformJump(float jumpForce)
        {
            if (!IsGrounded || IsDucking) return;

            RB.linearVelocity = new Vector3(RB.linearVelocity.x, 0f, 0f);
            RB.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            _isCollidingWithGround = false;
            IsGrounded = false;
            _jumpCooldownTimer = 0.15f;
        }

        /// <summary>
        /// Gọi khi bấm Xuống lúc đang ở trên không: huỷ đà nhảy, ép rơi thẳng xuống ngay lập
        /// tức (kiểu Subway Surfers).
        /// </summary>
        public void PerformFastFall()
        {
            if (IsGrounded) return;

            RB.linearVelocity = new Vector3(RB.linearVelocity.x, -_fastFallSpeed, RB.linearVelocity.z);
        }

        /// <summary>Gọi liên tục khi giữ phím Cúi — true = đang cúi, false = đứng thẳng lại.</summary>
        public void SetDucking(bool isDucking)
        {
            if (IsDucking == isDucking) return;

            // Chỉ chặn không cho bắt đầu cúi nếu đang ở trên không.
            // Khi nhả phím cúi (isDucking == false) thì LUÔN LUÔN cho phép đứng thẳng lại.
            if (isDucking && !IsGrounded) return;

            IsDucking = isDucking;

            // 1. Điều chỉnh BoxCollider giữ cố định chân tiếp đất
            if (_boxCollider != null)
            {
                float bottomY = _standingBoxCenter.y - (_standingBoxSize.y / 2f);
                float newHeight = isDucking ? _standingBoxSize.y * _duckHeightRatio : _standingBoxSize.y;
                _boxCollider.size = new Vector3(_standingBoxSize.x, newHeight, _standingBoxSize.z);
                _boxCollider.center = new Vector3(_standingBoxCenter.x, bottomY + (newHeight / 2f), _standingBoxCenter.z);
            }

            // 2. Điều chỉnh CapsuleCollider giữ cố định chân tiếp đất
            if (_capsuleCollider != null)
            {
                float bottomY = _standingCapsuleCenter.y - (_standingCapsuleHeight / 2f);
                float newHeight = isDucking ? _standingCapsuleHeight * _duckHeightRatio : _standingCapsuleHeight;
                _capsuleCollider.height = newHeight;
                _capsuleCollider.center = new Vector3(_standingCapsuleCenter.x, bottomY + (newHeight / 2f), _standingCapsuleCenter.z);
            }

            // 3. Phản hồi thị giác: co tỉ lệ chiều cao model để người chơi thấy rõ nhân vật đang cúi rạp xuống
            if (_visualRoot != null)
            {
                _visualRoot.localScale = isDucking
                    ? new Vector3(_standingVisualScale.x, _standingVisualScale.y * _duckHeightRatio, _standingVisualScale.z)
                    : _standingVisualScale;
            }
        }

        /// <summary>Gọi khi va chạm vật cản: đẩy lùi nhân vật một đoạn ngắn, giảm dần theo easing.</summary>
        public void ApplyKnockback()
        {
            _knockbackHandler.BeginKnockback(_knockbackDistance, _knockbackDuration);
        }

        public bool IsKnockingBack => _knockbackHandler.IsKnockingBack;

        /// <summary>
        /// Chuyển đổi Collider của Player sang Trigger (dùng khi va chạm vật cản để vật thể trôi xuyên qua Player).
        /// Khi bật Trigger, tạm khoá trục Y và tắt gravity để Player đứng vững trên mặt sàn không bị rơi xuyên đất.
        /// </summary>
        public void SetTriggerMode(bool isTrigger)
        {
            if (_boxCollider != null) _boxCollider.isTrigger = isTrigger;
            if (_capsuleCollider != null) _capsuleCollider.isTrigger = isTrigger;

            if (isTrigger)
            {
                RB.useGravity = false;
                RB.linearVelocity = new Vector3(RB.linearVelocity.x, 0f, RB.linearVelocity.z);
                RB.constraints |= RigidbodyConstraints.FreezePositionY;
            }
            else
            {
                RB.constraints &= ~RigidbodyConstraints.FreezePositionY;
                RB.useGravity = true;
            }
        }

        private void ApplyKnockbackMotion()
        {
            if (!_knockbackHandler.IsKnockingBack)
            {
                // Sau khi knockback xong, từ từ tiến lại vị trí treadmill ban đầu
                if (Mathf.Abs(RB.position.x - _startX) > 0.02f)
                {
                    RB.constraints &= ~RigidbodyConstraints.FreezePositionX;
                    float newX = Mathf.MoveTowards(RB.position.x, _startX, 2.5f * Time.fixedDeltaTime);
                    RB.MovePosition(new Vector3(newX, RB.position.y, RB.position.z));
                }
                else
                {
                    RB.MovePosition(new Vector3(_startX, RB.position.y, RB.position.z));
                    RB.constraints |= RigidbodyConstraints.FreezePositionX;
                }
                return;
            }

            // Mở tạm khoá trục X trong lúc đẩy lùi
            RB.constraints &= ~RigidbodyConstraints.FreezePositionX;

            float backward = _knockbackHandler.GetBackwardDelta(Time.fixedDeltaTime);
            RB.MovePosition(RB.position + _knockbackDirection * backward);
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
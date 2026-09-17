namespace SteamRush.Features.Runner
{
    using UnityEngine;
    using StreamRushLive.Features.Spawning;

    /// <summary>
    /// "Bộ não" vật lý của Runner: xử lý nhảy, cúi, rơi nhanh, ground-check.
    /// Nhân vật ĐỨNG YÊN theo trục ngang (mô hình treadmill) — không có
    /// hàm di chuyển ngang. Không đọc Input trực tiếp — RunnerInputHandler gọi các hàm public ở
    /// đây (Single Responsibility: Controller chỉ lo vật lý, không lo phím bấm).
    ///
    /// Nhảy khớp đúng GDD v1.2 mục 1 (hàng "Nhảy"): vận tốc +6.5 m/s, trọng lực tách riêng lúc
    /// lên/rơi để đỉnh nhảy ra đúng 2.2m và rơi đúng -18.0 m/s².
    ///
    /// THÊM MỚI: Forgiving Hitbox (GDD v1.2 mục 4.1) — Collider tự động được tính lại nhỏ hơn
    /// bounds thật của 3D Mesh 15% ngay lúc Awake, dựa trên Renderer thật của model thay vì phải
    /// tự đo tay trong Inspector. Việc này giúp tránh va chạm oan khi rìa model chưa thực sự chạm
    /// vật cản mà mắt người xem đã tưởng là chạm.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RunnerController : MonoBehaviour
    {
        [Header("Jump")]
        [Tooltip("Vận tốc nhảy trục Y. GDD v1.2 = +6.5 m/s.")]
        [SerializeField] private float _jumpVelocity = 6.5f;

        [Header("Gravity")]
        [Tooltip("Trọng lực áp dụng lúc đang đi LÊN (m/s², giá trị dương). Tính ngược từ h = v²/(2g) để đỉnh nhảy = 2.2m với vận tốc nhảy 6.5 m/s.")]
        [SerializeField] private float _risingGravity = 9.6f;
        [Tooltip("Trọng lực áp dụng lúc đang RƠI (m/s², giá trị dương). GDD v1.2 = 18.0 m/s².")]
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
        [Tooltip("Bật để Collider tự động co nhỏ hơn Mesh 3D thật lúc Awake (GDD v1.2 mục 4.1). Tắt nếu muốn tự chỉnh tay Collider trong Inspector.")]
        [SerializeField] private bool _autoApplyForgivingHitbox = true;
        [Tooltip("Tỉ lệ kích thước Collider so với Mesh thật. GDD v1.2 = nhỏ hơn Mesh 15% -> 0.85.")]
        [SerializeField, Range(0.5f, 1f)] private float _hitboxToleranceRatio = 0.85f;

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

        private bool _isCollidingWithGround;
        private float _jumpCooldownTimer;

        private void Awake()
        {
            RB = GetComponent<Rigidbody>();

            // Tắt hẳn gravity mặc định của Unity — tự áp trọng lực thủ công (xem FixedUpdate)
            // để đúng chính xác 2 con số GDD (rơi -18.0 m/s², lên tính ngược ra 9.6 m/s²) bất kể
            // Project Settings > Physics > Gravity của máy đang mở là bao nhiêu.
            RB.useGravity = false;

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
            _capsuleCollider = GetComponent<CapsuleCollider>();

            if (_autoApplyForgivingHitbox)
            {
                ApplyForgivingHitbox();
            }

            // Chụp lại kích thước "đứng thẳng" SAU KHI đã co Forgiving Hitbox — mọi phép tính Duck
            // (co xuống _duckHeightRatio) đều dựa trên baseline đã đúng GDD này.
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

            // Khoá cả Position X lẫn Z: nhân vật đứng yên tại chỗ theo cả 2 trục ngang — thế
            // giới (Track/Background) mới là thứ di chuyển, mô hình "treadmill" của endless
            // runner. Chỉ còn trục Y (nhảy/rơi) là tự do. Hoàn toàn không có knockback.
            RB.constraints = RigidbodyConstraints.FreezePositionX
                | RigidbodyConstraints.FreezePositionZ
                | RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationY
                | RigidbodyConstraints.FreezeRotationZ;
        }

        /// <summary>
        /// GDD v1.2 mục 4.1 "Hitbox Dung Sai": tính bounds thật từ toàn bộ Renderer (Mesh/Skinned)
        /// trong con của Runner, rồi co Collider còn <see cref="_hitboxToleranceRatio"/> (mặc định
        /// 85%) kích thước đó — chân vẫn chạm đúng mặt đất, chỉ co từ tâm ra ngoài theo tỉ lệ.
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

            // Nhân vật không xoay (constraints khoá hết Rotation), nên quy đổi world -> local
            // ở đây chỉ cần dịch tâm, kích thước giữ nguyên theo trục thế giới là đủ chính xác.
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
        }

        private void FixedUpdate()
        {
            // Trọng lực thủ công: dùng _risingGravity khi đang đi lên (Y > 0), _fallingGravity
            // khi đang rơi (Y <= 0) — 2 giá trị tách biệt để khớp đúng GDD (đỉnh nhảy 2.2m,
            // rơi -18.0 m/s²) thay vì dùng chung 1 multiplier nhân với gravity mặc định.
            float gravity = RB.linearVelocity.y > 0f ? _risingGravity : _fallingGravity;
            RB.linearVelocity += Vector3.down * gravity * Time.fixedDeltaTime;
        }

        /// <summary>Nhảy với vận tốc cố định GDD (+6.5 m/s), không phụ thuộc Mass của Rigidbody. Áp dụng multiplier nếu có buff High Jump.</summary>
        public void PerformJump()
        {
            if (!IsGrounded || IsDucking) return;

            float jumpVel = _jumpVelocity;
            if (TryGetComponent<RunnerItemEffects>(out var itemEffects) && itemEffects.IsHighJumpActive)
            {
                jumpVel *= itemEffects.CurrentJumpForceMultiplier;
            }

            RB.linearVelocity = new Vector3(RB.linearVelocity.x, jumpVel, 0f);
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

        /// <summary>
        /// Chuyển đổi Collider của Player sang Trigger (dùng khi va chạm vật cản để vật thể trôi xuyên qua Player).
        /// Khi bật Trigger, tạm khoá trục Y để Player đứng vững tại chỗ, không rơi tiếp trong lúc bất tử.
        /// </summary>
        public void SetTriggerMode(bool isTrigger)
        {
            if (_boxCollider != null) _boxCollider.isTrigger = isTrigger;
            if (_capsuleCollider != null) _capsuleCollider.isTrigger = isTrigger;

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
namespace SteamRush.Features.Runner
{
    using UnityEngine;

    /// "Bộ não" vật lý của Runner:xử lý nhảy đúp, cúi, rơi nhanh, ground-check
    /// và đẩy lùi khi va chạm. Nhân vật ĐỨNG YÊN theo trục ngang (mô hình treadmill) — không có
    /// hàm di chuyển ngang. Không đọc Input trực tiếp — RunnerInputHandler gọi các hàm public ở
    /// đây (Single Responsibility: Controller chỉ lo vật lý, không lo phím bấm).

    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class RunnerController : MonoBehaviour
    {
        [Header("Nhảy")]
        [SerializeField] private int _maxJumps = 2;

        [Header("Rơi")]
        [Tooltip("Hệ số tăng lực khi đang lên (sau khi nhảy, vận tốc Y > 0). Giúp quỹ đạo nhảy gọn, dứt khoát, bớt cảm giác bay bổng.")]
        [SerializeField] private float _risingMultiplier = 1.5f;
        [Tooltip("Hệ số tăng lực rơi khi đang đi xuống, giúp cảm giác rơi dứt khoát, tránh lơ lửng.")]
        [SerializeField] private float _fallMultiplier = 2.5f;

        [Header("Cúi")]
        [Tooltip("Tỉ lệ chiều cao Collider khi cúi so với lúc đứng (0.5 = còn lại một nửa).")]
        [SerializeField, Range(0.1f, 1f)] private float _duckHeightRatio = 0.5f;

        [Header("Rơi nhanh (bấm Xuống khi đang nhảy)")]
        [Tooltip("Vận tốc rơi ép xuống ngay lập tức khi bấm Xuống lúc đang ở trên không, kiểu Subway Surfers.")]
        [SerializeField] private float _fastFallSpeed = 20f;

        [Header("Va chạm mặt đất")]
        [SerializeField] private string _groundTag = "Ground";

        [Header("Đẩy lùi khi va chạm")]
        [SerializeField] private float _knockbackDistance = 1.5f;
        [SerializeField] private float _knockbackDuration = 0.3f;

        public Rigidbody RB { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsDucking { get; private set; }

        private BoxCollider _collider;
        private Vector3 _standingColliderSize;
        private Vector3 _standingColliderCenter;
        private readonly KnockbackHandler _knockbackHandler = new KnockbackHandler();

        private int _jumpCount;

        private void Awake()
        {
            RB = GetComponent<Rigidbody>();
            _collider = GetComponent<BoxCollider>();
            _standingColliderSize = _collider.size;
            _standingColliderCenter = _collider.center;

            // Khoá cả Position X lẫn Z: nhân vật đứng yên tại chỗ theo cả 2 trục ngang — thế
            // giới (Track/Background) mới là thứ di chuyển, mô hình "treadmill" của endless
            // runner. Chỉ còn trục Y (nhảy/rơi) là tự do. Trục X sẽ được MỞ TẠM lúc bị knockback.
            RB.constraints = RigidbodyConstraints.FreezePositionX
                | RigidbodyConstraints.FreezePositionZ
                | RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationY
                | RigidbodyConstraints.FreezeRotationZ;
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
            if (_jumpCount >= _maxJumps || IsDucking) return;

            RB.linearVelocity = new Vector3(RB.linearVelocity.x, 0f, 0f);
            RB.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            _jumpCount++;
            IsGrounded = false;
        }

        /// <summary>
        /// Gọi khi bấm Xuống lúc đang ở trên không: huỷ đà nhảy, ép rơi thẳng xuống ngay lập
        /// tức (kiểu Subway Surfers) — hoạt động với cả nhảy đơn lẫn nhảy đúp, vì chỉ đơn giản
        /// ghi đè vận tốc Y hiện tại, không quan tâm đang ở lần nhảy thứ mấy.
        /// </summary>
        public void PerformFastFall()
        {
            if (IsGrounded) return;

            RB.linearVelocity = new Vector3(RB.linearVelocity.x, -_fastFallSpeed, RB.linearVelocity.z);
        }

        /// <summary>Gọi liên tục khi giữ phím Cúi — true = đang cúi, false = đứng thẳng lại.</summary>
        public void SetDucking(bool isDucking)
        {
            if (!IsGrounded || IsDucking == isDucking) return;

            IsDucking = isDucking;
            _collider.size = isDucking
                ? new Vector3(_standingColliderSize.x, _standingColliderSize.y * _duckHeightRatio, _standingColliderSize.z)
                : _standingColliderSize;
            _collider.center = isDucking
                ? new Vector3(_standingColliderCenter.x, _standingColliderCenter.y * _duckHeightRatio, _standingColliderCenter.z)
                : _standingColliderCenter;
        }

        /// <summary>Gọi khi va chạm vật cản: đẩy lùi nhân vật một đoạn ngắn, giảm dần theo easing.</summary>
        public void ApplyKnockback()
        {
            _knockbackHandler.BeginKnockback(_knockbackDistance, _knockbackDuration);
        }

        public bool IsKnockingBack => _knockbackHandler.IsKnockingBack;

        private void ApplyKnockbackMotion()
        {
            if (!_knockbackHandler.IsKnockingBack)
            {
                // Đảm bảo khoá lại trục X ngay khi vừa đẩy lùi xong, trở về đúng mô hình treadmill.
                RB.constraints |= RigidbodyConstraints.FreezePositionX;
                return;
            }

            // Mở tạm khoá trục X trong lúc đẩy lùi, vì Position X vốn bị khoá cứng cho mô hình
            // treadmill — không mở ra thì không thể di chuyển nhân vật theo trục này được.
            RB.constraints &= ~RigidbodyConstraints.FreezePositionX;

            float backward = _knockbackHandler.GetBackwardDelta(Time.fixedDeltaTime);
            RB.MovePosition(RB.position + Vector3.right * backward);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!collision.gameObject.CompareTag(_groundTag)) return;

            IsGrounded = true;
            _jumpCount = 0;
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision.gameObject.CompareTag(_groundTag))
            {
                IsGrounded = false;
            }
        }
    }
}
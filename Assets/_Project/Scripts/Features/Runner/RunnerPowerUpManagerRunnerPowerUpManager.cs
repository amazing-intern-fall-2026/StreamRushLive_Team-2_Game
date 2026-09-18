using UnityEngine;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Quản lý trạng thái các Power-up đang hoạt động trên Runner:
    /// - Shield: chặn 1 lần va chạm và tồn tại tối đa 20 giây.
    /// - High Jump: tăng JumpForce lên 140% và tồn tại 10 giây.
    /// - Hyper Dash: tăng tốc độ thế giới lên 18 m/s và bất tử trong 5 giây.
    ///
    /// Class này chỉ quản lý trạng thái và thời gian của Power-up.
    /// Logic va chạm, nhảy và di chuyển vẫn do các class chuyên trách xử lý.
    /// </summary>
    public class RunnerPowerUpManager : MonoBehaviour
    {
        [Header("Shield")]
        [Tooltip("Maximum duration of Shield in seconds.")]
        [SerializeField] private float _shieldDuration = 20f;

        [Header("High Jump")]
        [Tooltip("Duration of High Jump buff in seconds.")]
        [SerializeField] private float _highJumpDuration = 10f;

        [Tooltip("Jump force multiplier while High Jump is active.")]
        [SerializeField] private float _highJumpMultiplier = 1.4f;

        [Header("Hyper Dash")]
        [Tooltip("Duration of Hyper Dash in seconds.")]
        [SerializeField] private float _hyperDashDuration = 5f;

        [Tooltip("World speed while Hyper Dash is active.")]
        [SerializeField] private float _hyperDashSpeed = 18f;

        private bool _hasShield;
        private float _shieldTimer;

        private bool _isHighJumpActive;
        private float _highJumpTimer;

        private bool _isHyperDashActive;
        private float _hyperDashTimer;

        public bool HasShield => _hasShield;
        public bool IsHighJumpActive => _isHighJumpActive;
        public bool IsHyperDashActive => _isHyperDashActive;

        public float ShieldRemainingTime => _shieldTimer;
        public float HighJumpRemainingTime => _highJumpTimer;
        public float HyperDashRemainingTime => _hyperDashTimer;

        public float HyperDashSpeed => _hyperDashSpeed;

        private void Update()
        {
            UpdateShieldTimer();
            UpdateHighJumpTimer();
            UpdateHyperDashTimer();
        }

        // ==========================================
        // SHIELD
        // ==========================================

        /// <summary>
        /// Kích hoạt Shield.
        /// Shield tồn tại tối đa 20 giây hoặc cho đến khi chặn một lần va chạm.
        /// </summary>
        public void ActivateShield()
        {
            _hasShield = true;
            _shieldTimer = _shieldDuration;
        }

        /// <summary>
        /// Kiểm tra và tiêu thụ Shield khi Runner bị va chạm.
        /// Trả về true nếu Shield đã chặn được va chạm.
        /// </summary>
        public bool ConsumeShield()
        {
            if (!_hasShield)
            {
                return false;
            }

            _hasShield = false;
            _shieldTimer = 0f;

            return true;
        }

        private void UpdateShieldTimer()
        {
            if (!_hasShield) return;

            _shieldTimer -= Time.deltaTime;

            if (_shieldTimer <= 0f)
            {
                _shieldTimer = 0f;
                _hasShield = false;
            }
        }

        // ==========================================
        // HIGH JUMP
        // ==========================================

        /// <summary>
        /// Kích hoạt High Jump.
        /// JumpForce sẽ được nhân với High Jump multiplier trong thời gian buff.
        /// </summary>
        public void ActivateHighJump()
        {
            _isHighJumpActive = true;
            _highJumpTimer = _highJumpDuration;
        }

        /// <summary>
        /// Trả về JumpForce sau khi áp dụng High Jump nếu đang active.
        /// </summary>
        public float GetModifiedJumpForce(float baseJumpForce)
        {
            if (!_isHighJumpActive)
            {
                return baseJumpForce;
            }

            return baseJumpForce * _highJumpMultiplier;
        }

        private void UpdateHighJumpTimer()
        {
            if (!_isHighJumpActive) return;

            _highJumpTimer -= Time.deltaTime;

            if (_highJumpTimer <= 0f)
            {
                _highJumpTimer = 0f;
                _isHighJumpActive = false;
            }
        }

        // ==========================================
        // HYPER DASH
        // ==========================================

        /// <summary>
        /// Kích hoạt Hyper Dash.
        /// Runner sẽ được xử lý ở tốc độ 18 m/s và ở trạng thái bất tử
        /// trong thời gian Hyper Dash còn hiệu lực.
        /// </summary>
        public void ActivateHyperDash()
        {
            _isHyperDashActive = true;
            _hyperDashTimer = _hyperDashDuration;
        }

        private void UpdateHyperDashTimer()
        {
            if (!_isHyperDashActive) return;

            _hyperDashTimer -= Time.deltaTime;

            if (_hyperDashTimer <= 0f)
            {
                _hyperDashTimer = 0f;
                _isHyperDashActive = false;
            }
        }
    }
}
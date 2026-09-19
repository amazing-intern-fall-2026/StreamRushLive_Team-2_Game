using System;
using System.Collections;
using UnityEngine;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Quản lý máu của Runner.
    ///
    /// Gameplay hiện tại:
    /// - Runner có tối đa 3 tim.
    /// - TakeDamage(1): mất 1 tim.
    /// - Sau khi nhận damage, Runner được bất tử trong 2 giây.
    /// - Trong thời gian bất tử, Runner nhấp nháy để thể hiện trạng thái.
    /// - Heal(1): hồi 1 tim, không vượt quá 3 tim.
    /// - ResetHealth(): đưa máu về đầy 3 tim.
    /// - Khi máu về 0, phát sự kiện OnPlayerDeath.
    /// </summary>
    public class RunnerHealthSystem : MonoBehaviour
    {
        [Header("Health Settings")]
        [Tooltip("Số tim tối đa của Runner.")]
        [SerializeField] private int maxHealth = 3;

        [SerializeField] private int currentHealth;

        [Header("Invulnerability Settings")]
        [Tooltip("Thời gian bất tử sau khi nhận damage.")]
        [SerializeField] private float invulnerabilityDuration = 2f;

        [Tooltip("Khoảng thời gian giữa các lần nhấp nháy.")]
        [SerializeField] private float blinkInterval = 0.15f;

        private Renderer[] _renderers;
        private Coroutine _invulnerabilityCoroutine;
        private bool _isInvulnerable;

        /// <summary>
        /// Sự kiện được phát khi Runner hết máu.
        /// Checkpoint có thể đăng ký vào event này để xử lý respawn.
        /// </summary>
        public event Action OnPlayerDeath;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public bool IsInvulnerable => _isInvulnerable;

        private void Awake()
        {
            maxHealth = Mathf.Max(1, maxHealth);

            _renderers = GetComponentsInChildren<Renderer>();

            currentHealth = maxHealth;
        }

        /// <summary>
        /// Trừ máu của Runner.
        ///
        /// Ví dụ:
        /// TakeDamage(1) = mất 1 tim.
        ///
        /// Nếu Runner đang bất tử thì damage sẽ bị bỏ qua.
        /// Khi máu chạm 0, Debug.Log được gọi trước OnPlayerDeath.
        /// </summary>
        public void TakeDamage(int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            if (_isInvulnerable)
            {
                Debug.Log("[RunnerHealthSystem] Runner đang bất tử, damage bị bỏ qua.");
                return;
            }

            currentHealth -= damage;
            currentHealth = Mathf.Max(currentHealth, 0);

            Debug.Log(
                $"[RunnerHealthSystem] Runner nhận {damage} damage. " +
                $"Health: {currentHealth}/{maxHealth}"
            );

            if (currentHealth <= 0)
            {
                currentHealth = 0;

                // Debug phải nằm trước OnPlayerDeath
                // để Checkpoint có thể bắt được event sau đó.
                Debug.Log("[RunnerHealthSystem] Runner hết máu! OnPlayerDeath sẽ được kích hoạt.");

                OnPlayerDeath?.Invoke();

                return;
            }

            StartInvulnerability();
        }

        /// <summary>
        /// Hồi máu cho Runner.
        ///
        /// Ví dụ:
        /// Heal(1) = hồi 1 tim.
        ///
        /// Máu không thể vượt quá maxHealth.
        /// </summary>
        public void Heal(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            int oldHealth = currentHealth;

            currentHealth += amount;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

            Debug.Log(
                $"[RunnerHealthSystem] Runner hồi {currentHealth - oldHealth} tim. " +
                $"Health: {currentHealth}/{maxHealth}"
            );
        }

        /// <summary>
        /// Đưa Runner về trạng thái đầy máu.
        /// </summary>
        public void ResetHealth()
        {
            currentHealth = maxHealth;

            StopInvulnerability();

            Debug.Log(
                $"[RunnerHealthSystem] Health đã reset: {currentHealth}/{maxHealth}"
            );
        }

        /// <summary>
        /// Bắt đầu trạng thái bất tử sau khi nhận damage.
        /// </summary>
        private void StartInvulnerability()
        {
            if (_invulnerabilityCoroutine != null)
            {
                StopCoroutine(_invulnerabilityCoroutine);
            }

            _invulnerabilityCoroutine =
                StartCoroutine(InvulnerabilityRoutine());
        }

        /// <summary>
        /// Runner bất tử và nhấp nháy trong 2 giây.
        /// </summary>
        private IEnumerator InvulnerabilityRoutine()
        {
            _isInvulnerable = true;

            float elapsed = 0f;

            while (elapsed < invulnerabilityDuration)
            {
                elapsed += Time.deltaTime;

                bool visible =
                    Mathf.FloorToInt(elapsed / blinkInterval) % 2 == 0;

                SetRenderersVisible(visible);

                yield return null;
            }

            SetRenderersVisible(true);

            _isInvulnerable = false;
            _invulnerabilityCoroutine = null;

            Debug.Log("[RunnerHealthSystem] Trạng thái bất tử đã kết thúc.");
        }

        /// <summary>
        /// Dừng trạng thái bất tử ngay lập tức.
        /// Dùng khi ResetHealth().
        /// </summary>
        private void StopInvulnerability()
        {
            if (_invulnerabilityCoroutine != null)
            {
                StopCoroutine(_invulnerabilityCoroutine);
                _invulnerabilityCoroutine = null;
            }

            _isInvulnerable = false;
            SetRenderersVisible(true);
        }

        private void SetRenderersVisible(bool visible)
        {
            if (_renderers == null)
            {
                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = visible;
                }
            }
        }
    }
}
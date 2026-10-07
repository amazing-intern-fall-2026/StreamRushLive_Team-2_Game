using System;
using System.Collections;
using UnityEngine;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Manages runner health (legacy heart system).
    /// </summary>
    public class RunnerHealthSystem : MonoBehaviour
    {
        [Header("Health Settings")]
        [Tooltip("Maximum hearts for runner.")]
        [SerializeField] private int maxHealth = 3;

        [SerializeField] private int currentHealth;

        [Header("Invulnerability Settings")]
        [Tooltip("Invulnerability duration after receiving damage.")]
        [SerializeField] private float invulnerabilityDuration = 2f;

        [Tooltip("Blink interval during invulnerability.")]
        [SerializeField] private float blinkInterval = 0.15f;

        private Renderer[] _renderers;
        private Coroutine _invulnerabilityCoroutine;
        private bool _isInvulnerable;

        /// <summary>
        /// Event published when runner runs out of health.
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
        /// Deducts runner health.
        /// </summary>
        public void TakeDamage(int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            if (_isInvulnerable)
            {
                Debug.Log("[RunnerHealthSystem] Runner is invulnerable, damage ignored.");
                return;
            }

            currentHealth -= damage;
            currentHealth = Mathf.Max(currentHealth, 0);

            Debug.Log(
                $"[RunnerHealthSystem] Runner took {damage} damage. " +
                $"Health: {currentHealth}/{maxHealth}"
            );

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Debug.Log("[RunnerHealthSystem] Runner out of health! Invoking OnPlayerDeath.");

                OnPlayerDeath?.Invoke();

                return;
            }

            StartInvulnerability();
        }

        /// <summary>
        /// Restores runner health up to maxHealth.
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
                $"[RunnerHealthSystem] Runner healed {currentHealth - oldHealth} hearts. " +
                $"Health: {currentHealth}/{maxHealth}"
            );
        }

        /// <summary>
        /// Resets runner health to max.
        /// </summary>
        public void ResetHealth()
        {
            currentHealth = maxHealth;

            StopInvulnerability();

            Debug.Log(
                $"[RunnerHealthSystem] Health reset: {currentHealth}/{maxHealth}"
            );
        }

        /// <summary>
        /// Begins invulnerability state after taking damage.
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
        /// Runner invulnerability and blinking coroutine.
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

            Debug.Log("[RunnerHealthSystem] Invulnerability period ended.");
        }

        /// <summary>
        /// Stops invulnerability immediately.
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
using System.Collections;
using UnityEngine;
using SteamRush.Features.Runner;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Xử lý hiệu ứng hồi máu tức thì từ quà Donate.
    ///
    /// Gameplay mới:
    /// - Không phải vật phẩm vật lý trên đường chạy.
    /// - Không cần Player nhặt.
    /// - Khi được kích hoạt sẽ hồi ngay 1 tim.
    /// - Hiển thị hiệu ứng ánh sáng xanh lá trong thời gian ngắn.
    /// </summary>
    public class InstantHealItem : MonoBehaviour
    {
        [Header("Heal Settings")]
        [Tooltip("Số tim hồi mỗi lần nhận quà hồi máu.")]
        [SerializeField] private int healAmount = 1;

        [Tooltip("Thời gian hiển thị hiệu ứng ánh sáng xanh lá.")]
        [SerializeField] private float effectDuration = 0.5f;

        [Header("Green Heal Effect")]
        [Tooltip("GameObject chứa hiệu ứng ánh sáng xanh lá.")]
        [SerializeField] private GameObject healEffect;

        private RunnerHealthSystem _healthSystem;
        private Coroutine _effectCoroutine;

        private void Awake()
        {
            _healthSystem = GetComponent<RunnerHealthSystem>();

            if (_healthSystem == null)
            {
                _healthSystem = GetComponentInParent<RunnerHealthSystem>();
            }

            if (healEffect != null)
            {
                healEffect.SetActive(false);
            }
        }

        /// <summary>
        /// Kích hoạt hộp cứu thương.
        /// Hàm này được MockChatConsole gọi khi giả lập Donate Heal.
        /// </summary>
        public void ActivateHeal()
        {
            if (_healthSystem == null)
            {
                _healthSystem = GetComponent<RunnerHealthSystem>();

                if (_healthSystem == null)
                {
                    _healthSystem = GetComponentInParent<RunnerHealthSystem>();
                }
            }

            if (_healthSystem == null)
            {
                Debug.LogWarning(
                    "[InstantHealItem] Không tìm thấy RunnerHealthSystem trên Runner."
                );
                return;
            }

            // Hồi ngay lập tức 1 tim.
            _healthSystem.Heal(healAmount);

            Debug.Log(
                $"[InstantHealItem] Donate Heal → hồi {healAmount} tim."
            );

            // Hiển thị hiệu ứng ánh sáng xanh lá.
            PlayHealEffect();
        }

        /// <summary>
        /// Bật hiệu ứng hồi máu.
        /// Nếu hiệu ứng đang chạy thì reset thời gian hiển thị.
        /// </summary>
        private void PlayHealEffect()
        {
            if (healEffect == null)
            {
                return;
            }

            if (_effectCoroutine != null)
            {
                StopCoroutine(_effectCoroutine);
            }

            _effectCoroutine = StartCoroutine(HealEffectCoroutine());
        }

        private IEnumerator HealEffectCoroutine()
        {
            healEffect.SetActive(true);

            yield return new WaitForSeconds(effectDuration);

            healEffect.SetActive(false);
            _effectCoroutine = null;
        }
    }
}
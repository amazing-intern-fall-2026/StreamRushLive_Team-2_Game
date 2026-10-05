using System.Collections;
using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Quản lý các hiệu ứng Gift (quà tặng trực tiếp từ Viewer) đang tác động lên Runner.
    /// - Quản lý trạng thái Khiên bảo hộ (Shield).
    /// - Shield tồn tại tối đa theo thời gian được cấu hình (mặc định 15s).
    /// - Shield chỉ chặn được 1 lần va chạm.
    /// - Quản lý hiệu ứng High Jump / Buffs.
    /// </summary>
    public class RunnerGiftEffects : MonoBehaviour
    {
        [Header("Shield State")]
        [SerializeField] private bool shieldActive = false;

        [Header("High Jump State")]
        [SerializeField] private bool highJumpActive = false;

        [SerializeField] private float currentJumpForceMultiplier = 1f;

        private Coroutine shieldCoroutine;
        private Coroutine highJumpCoroutine;

        public bool IsShieldActive => shieldActive;
        public bool IsHighJumpActive => highJumpActive;
        public float CurrentJumpForceMultiplier => currentJumpForceMultiplier;

        private void Awake()
        {
            // Luôn đảm bảo trạng thái buff được reset sạch sẽ khi khởi động game
            shieldActive = false;
            highJumpActive = false;
            currentJumpForceMultiplier = 1f;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                shieldActive = false;
                highJumpActive = false;
                currentJumpForceMultiplier = 1f;
            }
        }

        private void OnValidate()
        {
            // Không bao giờ cho phép lưu shieldActive = true vào scene hoặc prefab khi không chạy Play
            if (!Application.isPlaying)
            {
                shieldActive = false;
                highJumpActive = false;
                currentJumpForceMultiplier = 1f;
            }
        }

        /// <summary>
        /// Kích hoạt Shield cho Runner (mặc định 15s theo yêu cầu GDD).
        /// Nếu Runner đang có Shield, thời gian sẽ được tính lại từ đầu.
        /// </summary>
        public void ActivateShield(float duration = 15f)
        {
            if (duration <= 0f)
            {
                duration = StreamRushLive.Features.Gifts.GiftManager.Instance != null 
                    ? StreamRushLive.Features.Gifts.GiftManager.Instance.ShieldDuration 
                    : 15f;
            }

            if (shieldCoroutine != null)
            {
                StopCoroutine(shieldCoroutine);
            }

            shieldCoroutine = StartCoroutine(ShieldTimer(duration));
            SteamRush.Features.UI.Views.ShieldTimerCircle.Instance?.ActivateTimer(duration);
            AudioManager.Instance?.PlaySFX(SFXType.ShieldActive);
        }

        /// <summary>
        /// Shield chặn một lần va chạm và bị tiêu hao ngay sau đó.
        /// </summary>
        /// <returns>True nếu Shield đã chặn được va chạm.</returns>
        public bool ConsumeShield()
        {
            if (!shieldActive)
            {
                return false;
            }

            shieldActive = false;

            if (shieldCoroutine != null)
            {
                StopCoroutine(shieldCoroutine);
                shieldCoroutine = null;
            }

            SteamRush.Features.UI.Views.ShieldTimerCircle.Instance?.DeactivateTimer();
            AudioManager.Instance?.PlaySFX(SFXType.ShieldBreak);
            Debug.Log("[RunnerItemEffects] Shield đã chặn 1 lần va chạm.");

            return true;
        }

        private IEnumerator ShieldTimer(float duration)
        {
            shieldActive = true;

            Debug.Log($"[RunnerItemEffects] Shield activated for {duration:F0}s.");

            yield return new WaitForSeconds(duration);

            shieldActive = false;
            shieldCoroutine = null;
            SteamRush.Features.UI.Views.ShieldTimerCircle.Instance?.DeactivateTimer();
            AudioManager.Instance?.PlaySFX(SFXType.ShieldBreak);

            Debug.Log("[RunnerItemEffects] Shield đã hết thời gian.");
        }

        /// <summary>
        /// Kích hoạt High Jump cho Runner.
        /// Nếu High Jump đang hoạt động, thời gian hiệu ứng sẽ được tính lại từ đầu.
        /// </summary>
        public void ActivateHighJump(float duration, float multiplier)
        {
            if (highJumpCoroutine != null)
            {
                StopCoroutine(highJumpCoroutine);
            }

            highJumpCoroutine = StartCoroutine(HighJumpTimer(duration, multiplier));
        }

        private IEnumerator HighJumpTimer(float duration, float multiplier)
        {
            highJumpActive = true;
            currentJumpForceMultiplier = multiplier;

            Debug.Log(
                $"[RunnerItemEffects] High Jump activated: " +
                $"JumpForce x{multiplier:F1} for {duration:F0}s."
            );

            yield return new WaitForSeconds(duration);

            highJumpActive = false;
            currentJumpForceMultiplier = 1f;
            highJumpCoroutine = null;

            Debug.Log("[RunnerGiftEffects] High Jump đã hết thời gian.");
        }
    }

    /// <summary>
    /// Alias tương thích ngược với các scenes, prefabs và mã nguồn cũ trước khi chuyển sang hệ thống Gift trực tiếp.
    /// </summary>
    public class RunnerItemEffects : RunnerGiftEffects
    {
    }
}
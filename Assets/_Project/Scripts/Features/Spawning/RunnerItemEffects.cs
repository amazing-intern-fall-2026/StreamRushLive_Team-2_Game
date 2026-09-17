using System.Collections;
using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Quản lý các hiệu ứng Item đang tác động lên Runner.
    /// - Quản lý trạng thái Shield.
    /// - Shield tồn tại tối đa theo thời gian được cấu hình.
    /// - Shield chỉ chặn được 1 lần va chạm.
    /// - Quản lý hiệu ứng High Jump.
    /// - High Jump tăng JumpForce lên 40% trong thời gian được cấu hình.
    /// </summary>
    public class RunnerItemEffects : MonoBehaviour
    {
        [Header("Shield State")]
        [SerializeField] private bool shieldActive = false;

        [Header("High Jump State")]
        [SerializeField] private bool highJumpActive = false;

        [SerializeField] private float currentJumpForceMultiplier = 1f;

        [Header("Hyper Dash State")]
        [SerializeField] private bool hyperDashActive = false;

        private Coroutine shieldCoroutine;
        private Coroutine highJumpCoroutine;
        private Coroutine hyperDashCoroutine;

        public bool IsShieldActive => shieldActive;
        public bool IsHighJumpActive => highJumpActive;
        public bool IsHyperDashActive => hyperDashActive;
        public float CurrentJumpForceMultiplier => currentJumpForceMultiplier;

        /// <summary>
        /// Kích hoạt Shield cho Runner.
        /// Nếu Runner đang có Shield, thời gian sẽ được tính lại từ đầu.
        /// </summary>
        public void ActivateShield(float duration)
        {
            if (shieldCoroutine != null)
            {
                StopCoroutine(shieldCoroutine);
            }

            shieldCoroutine = StartCoroutine(ShieldTimer(duration));
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

            Debug.Log("[RunnerItemEffects] High Jump đã hết thời gian.");
        }

        /// <summary>
        /// Kích hoạt Hyper Dash cho Runner.
        /// Tăng tốc độ tối đa (18 m/s) và cấp quyền đi xuyên/phá huỷ vật cản trong duration giây.
        /// </summary>
        public void ActivateHyperDash(float duration, float maxSpeed)
        {
            if (hyperDashCoroutine != null)
            {
                StopCoroutine(hyperDashCoroutine);
            }

            hyperDashCoroutine = StartCoroutine(HyperDashTimer(duration, maxSpeed));
        }

        private IEnumerator HyperDashTimer(float duration, float maxSpeed)
        {
            hyperDashActive = true;
            Debug.Log($"[RunnerItemEffects] Hyper Dash activated for {duration:F0}s at {maxSpeed:F0} m/s.");

            SteamRush.Features.Runner.RunnerCollisionHandler collisionHandler =
                GetComponent<SteamRush.Features.Runner.RunnerCollisionHandler>()
                ?? GetComponentInParent<SteamRush.Features.Runner.RunnerCollisionHandler>();

            if (collisionHandler != null)
            {
                collisionHandler.SetHyperDashState(true);
            }

            EnergySystem energySystem = FindFirstObjectByType<EnergySystem>();
            if (energySystem != null)
            {
                energySystem.SetSpeedOverride(maxSpeed);
            }

            yield return new WaitForSeconds(duration);

            hyperDashActive = false;

            if (collisionHandler != null)
            {
                collisionHandler.SetHyperDashState(false);
            }

            if (energySystem != null)
            {
                energySystem.ClearSpeedOverride();
            }

            hyperDashCoroutine = null;
            Debug.Log("[RunnerItemEffects] Hyper Dash đã hết thời gian.");
        }
    }
}
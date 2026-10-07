using System.Collections;
using UnityEngine;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Manages active gift buffs on the Runner (Shield, High Jump, etc.).
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
            // Reset buff states cleanly on Awake
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
            // Never allow shieldActive to persist in scene or prefab serialization outside Play Mode
            if (!Application.isPlaying)
            {
                shieldActive = false;
                highJumpActive = false;
                currentJumpForceMultiplier = 1f;
            }
        }

        /// <summary>
        /// Activates protective shield buff on runner.
        /// Resets duration if already active.
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
        /// Consumes shield to absorb a collision impact.
        /// </summary>
        /// <returns>True if shield absorbed the impact.</returns>
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
            Debug.Log("[RunnerItemEffects] Shield absorbed 1 collision impact.");

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

            Debug.Log("[RunnerItemEffects] Shield duration expired.");
        }

        /// <summary>
        /// Activates High Jump buff on runner.
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

            Debug.Log("[RunnerGiftEffects] High Jump duration expired.");
        }
    }

    /// <summary>
    /// Backward compatibility alias.
    /// </summary>
    public class RunnerItemEffects : RunnerGiftEffects
    {
    }
}
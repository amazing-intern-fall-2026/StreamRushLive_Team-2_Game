using System;
using UnityEngine;
using UnityEngine.Events;
using SteamRush.Track;
using SteamRush.Features.UI;
using SteamRush.Features.Runner;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Manages Runner energy.
    /// Energy does not drain over time and sprint is free.
    /// Lane switching consumes energy (default -10 points).
    /// Obstacle collisions inflict energy penalty (-25%).
    /// </summary>
    public class EnergySystem : MonoBehaviour
    {
        [Header("Energy Settings")]
        [SerializeField] private float maxEnergy = 100f;
        [Tooltip("Energy deducted per lane change (default = 10 points).")]
        [SerializeField] private float laneChangeEnergyCost = 10f;

        [Header("Speed Settings (Optional Override)")]
        [SerializeField] private float normalSpeed = 8f; // Standard 8 m/s

        [Header("References")]
        [SerializeField] private WorldSpeedManager worldSpeedManager;
        [SerializeField] private HUDManager hudManager;

        public UnityEvent<float> OnEnergyNormalizedChanged = new UnityEvent<float>();

        private float currentEnergy;
        private bool _hasSpeedOverride;
        private float _speedOverride;

        public float MaxEnergy => maxEnergy;
        public float CurrentEnergy => currentEnergy;
        public float EnergyNormalized => Mathf.Clamp01(currentEnergy / maxEnergy);
        public float LaneChangeEnergyCost => laneChangeEnergyCost;

        private void Start()
        {
            currentEnergy = maxEnergy;

            if (worldSpeedManager == null)
            {
                worldSpeedManager = FindFirstObjectByType<WorldSpeedManager>();
            }

            if (hudManager == null)
            {
                hudManager = FindFirstObjectByType<HUDManager>();
            }

            SyncHUD();
        }

        private void Update()
        {
            // No passive energy drain over time. Energy is deducted on lane change or collisions.
            SyncHUD();
        }

        /// <summary>
        /// Consumes energy when the runner changes lanes.
        /// </summary>
        public void ConsumeLaneChangeEnergy(float cost = -1f)
        {
            float actualCost = cost >= 0f ? cost : laneChangeEnergyCost;
            currentEnergy = Mathf.Max(0f, currentEnergy - actualCost);
            SyncHUD();
        }

        /// <summary>
        /// Checks and consumes lane change energy. Returns false if insufficient.
        /// </summary>
        public bool TryConsumeLaneChangeEnergy(float cost = -1f)
        {
            float actualCost = cost >= 0f ? cost : laneChangeEnergyCost;
            if (currentEnergy < actualCost)
            {
                return false;
            }

            currentEnergy -= actualCost;
            SyncHUD();
            return true;
        }

        private void SyncHUD()
        {
            float normalized = EnergyNormalized;
            OnEnergyNormalizedChanged?.Invoke(normalized);

            if (hudManager != null)
            {
                hudManager.UpdateEnergy(normalized);
            }
        }

        /// <summary>
        /// Returns the current runner speed if overridden by an item or buff.
        /// </summary>
        public float GetCurrentSpeed()
        {
            if (_hasSpeedOverride)
            {
                return _speedOverride;
            }

            return normalSpeed;
        }

        /// <summary>
        /// Overrides current speed with a buff speed value.
        /// </summary>
        public void SetSpeedOverride(float speed)
        {
            _hasSpeedOverride = true;
            _speedOverride = Mathf.Max(0f, speed);

            if (worldSpeedManager != null)
            {
                worldSpeedManager.CurrentSpeed = _speedOverride;
            }
        }

        /// <summary>
        /// Clears active speed override.
        /// </summary>
        public void ClearSpeedOverride()
        {
            _hasSpeedOverride = false;
            _speedOverride = 0f;

            if (worldSpeedManager != null)
            {
                worldSpeedManager.CurrentSpeed = normalSpeed;
            }
        }

        /// <summary>
        /// Adds or deducts energy (hearts, gifts, or collision penalty).
        /// </summary>
        public void AddEnergy(float amount)
        {
            currentEnergy += amount;
            currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);
            SyncHUD();
        }

        /// <summary>
        /// Restores energy and triggers HUD feedback on receiving likes/hearts.
        /// </summary>
        public void AddLike(float energyBonus = 20f, string sender = null)
        {
            AddEnergy(energyBonus);
        }

        public float GetCurrentEnergy()
        {
            return currentEnergy;
        }
    }
}

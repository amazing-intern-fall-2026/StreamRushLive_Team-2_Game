using System;
using UnityEngine;
using UnityEngine.Events;
using SteamRush.Track;
using SteamRush.Features.UI;
using SteamRush.Features.Runner;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Quản lý năng lượng của Runner.
    /// Energy tự giảm theo thời gian.
    /// Khi Energy > 0, Runner được tăng 50% tốc độ (Sprint).
    /// </summary>
    public class EnergySystem : MonoBehaviour
    {
        [Header("Energy Settings")]
        [SerializeField] private float maxEnergy = 100f;
        [SerializeField] private float energyDrainPerSecond = 10f;

        [Header("Speed Settings")]
        [SerializeField] private float normalSpeed = 8f; // Chuẩn 8 m/s
        [SerializeField] private float sprintMultiplier = 1.5f; // Sprint +50% = 12 m/s

        [Header("References")]
        [SerializeField] private WorldSpeedManager worldSpeedManager;
        [SerializeField] private HUDManager hudManager;

        public UnityEvent<float> OnEnergyNormalizedChanged = new UnityEvent<float>();

        private float currentEnergy;
        private PlayerCollisionFeature playerCollision;

        public float MaxEnergy => maxEnergy;
        public float CurrentEnergy => currentEnergy;
        public float EnergyNormalized => Mathf.Clamp01(currentEnergy / maxEnergy);

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

            playerCollision = FindFirstObjectByType<PlayerCollisionFeature>();
        }

        private void Update()
        {
            DrainEnergy();
            SyncWorldSpeed();
            SyncHUD();
        }

        private void DrainEnergy()
        {
            if (currentEnergy > 0f)
            {
                currentEnergy -= energyDrainPerSecond * Time.deltaTime;
                currentEnergy = Mathf.Max(currentEnergy, 0f);
            }
        }

        private void SyncWorldSpeed()
        {
            if (worldSpeedManager == null) return;

            // Nếu Player đang bị Stun thì để PlayerCollisionFeature tự quản lý tốc độ
            if (playerCollision != null && playerCollision.IsStunned)
            {
                return;
            }

            worldSpeedManager.CurrentSpeed = GetCurrentSpeed();
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
        /// Trả về tốc độ hiện tại của Runner.
        /// Energy > 0: tăng 50%.
        /// Energy = 0: tốc độ bình thường.
        /// </summary>
        public float GetCurrentSpeed()
        {
            if (currentEnergy > 0f)
            {
                return normalSpeed * sprintMultiplier;
            }

            return normalSpeed;
        }

        /// <summary>
        /// Thêm hoặc trừ năng lượng (dùng cho Thả tim, Nhặt item, hoặc Phạt vấp ngã).
        /// </summary>
        public void AddEnergy(float amount)
        {
            currentEnergy += amount;
            currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);
            SyncHUD();
        }

        public float GetCurrentEnergy()
        {
            return currentEnergy;
        }
    }
}

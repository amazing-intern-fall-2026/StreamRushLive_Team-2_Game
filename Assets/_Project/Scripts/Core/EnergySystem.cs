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
    /// Năng lượng không tự giảm theo thời gian và không trừ khi bứt tốc (Sprint là miễn phí).
    /// Thay vào đó, mỗi lần đổi làn sẽ bị trừ năng lượng (mặc định -10 điểm / 1%).
    /// Khi va chạm chướng ngại vật bị phạt trừ năng lượng (-25%).
    /// </summary>
    public class EnergySystem : MonoBehaviour
    {
        [Header("Energy Settings")]
        [SerializeField] private float maxEnergy = 100f;
        [Tooltip("Lượng năng lượng bị trừ mỗi lần Runner đổi làn (mặc định = 10 điểm).")]
        [SerializeField] private float laneChangeEnergyCost = 10f;

        [Header("Speed Settings (Optional Override)")]
        [SerializeField] private float normalSpeed = 8f; // Chuẩn 8 m/s

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
            // Không còn DrainEnergy() theo thời gian. Năng lượng chỉ bị trừ khi đổi làn hoặc va chạm.
            SyncHUD();
        }

        /// <summary>
        /// Trừ năng lượng khi Runner thực hiện thao tác đổi làn.
        /// </summary>
        public void ConsumeLaneChangeEnergy(float cost = -1f)
        {
            float actualCost = cost >= 0f ? cost : laneChangeEnergyCost;
            currentEnergy = Mathf.Max(0f, currentEnergy - actualCost);
            SyncHUD();
        }

        /// <summary>
        /// Kiểm tra và trừ năng lượng đổi làn. Trả về false nếu không đủ năng lượng.
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
        /// Trả về tốc độ hiện tại của Runner nếu có ghi đè từ Buff/Item.
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
        /// Ghi đè tốc độ hiện tại bằng tốc độ đặc biệt của Item (như Hyper Dash).
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
        /// Xóa tốc độ ghi đè.
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
        /// Thêm hoặc trừ năng lượng (dùng cho Thả tim, Nhặt item, hoặc Phạt vấp ngã).
        /// </summary>
        public void AddEnergy(float amount)
        {
            currentEnergy += amount;
            currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);
            SyncHUD();
        }

        /// <summary>
        /// Nhận tim / like từ viewer: hồi phục năng lượng và kích hoạt popup thông báo tim trên HUD.
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

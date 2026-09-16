using UnityEngine;
using UnityEngine.InputSystem;
using SteamRush.Relay;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Xử lý các phím test trong quá trình phát triển (hỗ trợ cả New Input System và Direct Keyboard).
    /// Sử dụng trực tiếp cấu hình vị trí spawn từ Spawner.
    ///
    /// 1 - Spawn Rào thấp (Low Barrier - Buộc Nhảy)
    /// 2 - Spawn Xà cao (High Barrier - Buộc Trượt)
    /// 3 - Spawn Buff Item (Hồi năng lượng)
    /// 4 - Thả tim (Add Energy +20)
    /// 5 - Thêm Follower vào hàng đợi tiếp sức
    /// </summary>
    public class MockInput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Spawner spawner;
        [SerializeField] private EnergySystem energySystem;
        [SerializeField] private RelayQueueManager relayQueue;

        [Header("Test Settings")]
        [SerializeField] private float energyAmount = 20f;

        private int followerCounter = 1;

        private void Start()
        {
            if (spawner == null) spawner = FindFirstObjectByType<Spawner>();
            if (energySystem == null) energySystem = FindFirstObjectByType<EnergySystem>();
            if (relayQueue == null) relayQueue = FindFirstObjectByType<RelayQueueManager>();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            // Phím 1: Rào thấp
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                SpawnLowBarrier();
            }

            // Phím 2: Xà cao
            if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                SpawnHighBarrier();
            }

            // Phím 3: Buff Item
            if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
            {
                SpawnBuffItem();
            }

            // Phím 4: Add Energy
            if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)
            {
                AddEnergy();
            }

            // Phím 5: Add Follower
            if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame)
            {
                AddMockFollower();
            }
        }

        public void SpawnLowBarrier()
        {
            if (spawner != null)
            {
                spawner.SpawnObstacle(ObstacleType.LowBarrier);
            }
        }

        public void SpawnHighBarrier()
        {
            if (spawner != null)
            {
                spawner.SpawnObstacle(ObstacleType.HighBarrier);
            }
        }

        public void SpawnBuffItem()
        {
            if (spawner != null)
            {
                spawner.SpawnItem(ItemType.EnergyBuff);
            }
        }

        public void AddEnergy()
        {
            if (energySystem != null)
            {
                energySystem.AddLike(energyAmount);
            }
        }

        public void AddMockFollower()
        {
            string newFollower = $"Follower_{followerCounter++}";
            if (relayQueue != null)
            {
                relayQueue.EnqueueFollower(newFollower);
            }
        }
    }
}
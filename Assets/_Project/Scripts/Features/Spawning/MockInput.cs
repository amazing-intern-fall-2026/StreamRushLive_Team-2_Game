using UnityEngine;
using UnityEngine.InputSystem;
using SteamRush.Relay;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Xử lý các phím test trong quá trình phát triển (hỗ trợ cả New Input System và Direct Keyboard).
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

        [Header("Spawn Settings 2.5D")]
        [SerializeField] private float spawnX = 30f;
        [SerializeField] private float lowBarrierY = 0.5f;
        [SerializeField] private float highBarrierY = 1.8f;
        [SerializeField] private float buffItemY = 0.8f;

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
            Debug.Log("[MockInput] Spawn Rào thấp (Cần Nhảy né)");
            if (spawner != null)
            {
                spawner.Spawn(SpawnType.LowBarrier, new Vector3(spawnX, lowBarrierY, 0f));
            }
        }

        public void SpawnHighBarrier()
        {
            Debug.Log("[MockInput] Spawn Xà cao (Cần Cúi/Trượt né)");
            if (spawner != null)
            {
                spawner.Spawn(SpawnType.HighBarrier, new Vector3(spawnX, highBarrierY, 0f));
            }
        }

        public void SpawnBuffItem()
        {
            Debug.Log("[MockInput] Spawn Buff Item");
            if (spawner != null)
            {
                spawner.Spawn(SpawnType.BuffItem, new Vector3(spawnX, buffItemY, 0f));
            }
        }

        public void AddEnergy()
        {
            Debug.Log($"[MockInput] Thả tim: Thêm +{energyAmount} năng lượng");
            if (energySystem != null)
            {
                energySystem.AddEnergy(energyAmount);
            }
        }

        public void AddMockFollower()
        {
            string newFollower = $"Follower_{followerCounter++}";
            Debug.Log($"[MockInput] Người xem mới: {newFollower} được thêm vào hàng đợi");
            if (relayQueue != null)
            {
                relayQueue.EnqueueFollower(newFollower);
            }
        }
    }
}
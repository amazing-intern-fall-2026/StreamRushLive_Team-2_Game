using UnityEngine;
using UnityEngine.InputSystem;
using SteamRush.Relay;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Xử lý các phím test trong quá trình phát triển (hỗ trợ cả New Input System và Direct Keyboard).
    /// Sử dụng trực tiếp cấu hình vị trí spawn từ Spawner.
    ///
    /// 1 - Spawn Obstacle ngẫu nhiên (Low Barrier / High Barrier)
    /// 2 - Spawn Item ngẫu nhiên (Energy Buff / Shield / High Jump / Hyper Dash)
    /// 3 - Thả tim (Add Energy +20)
    /// 4 - Thêm Follower vào hàng đợi tiếp sức
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

            // Phím 1: Spawn Obstacle ngẫu nhiên
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                SpawnRandomObstacle();
            }

            // Phím 2: Spawn Item ngẫu nhiên
            if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                SpawnRandomItem();
            }

            // Phím 3: Thả tim / Add Energy
            if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
            {
                AddEnergy();
            }

            // Phím 4: Add Follower
            if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)
            {
                AddMockFollower();
            }

            // Phím 6: Stop Sign (NguyenHuy)
            if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame)
            {
                SpawnStopSign();
            }

            // Phím 7: Traffic Light + Crossing Car (NguyenHuy)
            if (Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame)
            {
                SpawnTrafficLight();
            }

            // Phím 8: Falling Hazard (NguyenHuy)
            if (Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame)
            {
                SpawnFallingHazard();
            }

            // Phím 9: Bouncing Boulder (NguyenHuy)
            if (Keyboard.current.digit9Key.wasPressedThisFrame || Keyboard.current.numpad9Key.wasPressedThisFrame)
            {
                SpawnBouncingBoulder();
            }

            // Phím 0: Test Queue Safe Distance 15m (NguyenHuy)
            if (Keyboard.current.digit0Key.wasPressedThisFrame || Keyboard.current.numpad0Key.wasPressedThisFrame)
            {
                EnqueueSafeDistanceBatch();
            }
        }

        public void SpawnRandomObstacle()
        {
            if (spawner != null)
            {
                spawner.SpawnRandomObstacle();
            }
        }

        public void SpawnRandomItem()
        {
            if (spawner != null)
            {
                spawner.SpawnRandomItem();
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

        public void SpawnStopSign()
        {
            if (spawner != null)
            {
                spawner.SpawnObstacle(ObstacleType.StopSign);
            }
        }

        public void SpawnTrafficLight()
        {
            if (spawner != null)
            {
                spawner.SpawnObstacle(ObstacleType.TrafficLight);
            }
        }

        public void SpawnFallingHazard()
        {
            if (spawner != null)
            {
                spawner.SpawnObstacle(ObstacleType.FallingHazard);
            }
        }

        public void SpawnBouncingBoulder()
        {
            if (spawner != null)
            {
                spawner.SpawnObstacle(ObstacleType.BouncingBoulder);
            }
        }

        public void EnqueueSafeDistanceBatch()
        {
            if (spawner != null)
            {
                Debug.Log("[MockInput] Enqueuing batch of 4 obstacles to test safe distance (15m)...");
                spawner.EnqueueObstacle(ObstacleType.LowBarrier);
                spawner.EnqueueObstacle(ObstacleType.StopSign);
                spawner.EnqueueObstacle(ObstacleType.TrafficLight);
                spawner.EnqueueObstacle(ObstacleType.BouncingBoulder);
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
            Debug.Log("[MockInput] Phím 3 - Thả tim / Add Energy");
            if (energySystem != null)
            {
                energySystem.AddLike(energyAmount);
            }
        }

        public void AddMockFollower()
        {
            string newFollower = $"Follower_{followerCounter++}";
            Debug.Log($"[MockInput] Phím 4 - Add Follower: {newFollower}");

            if (relayQueue != null)
            {
                relayQueue.EnqueueFollower(newFollower);
            }
        }
    }
}
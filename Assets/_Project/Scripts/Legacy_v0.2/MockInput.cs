using UnityEngine;
using UnityEngine.InputSystem;
using SteamRush.Relay;

namespace StreamRushLive.Features.Spawning
{
    /// <summary>
    /// Handles test keys during development (supports New Input System and Direct Keyboard).
    /// Uses spawn position configurations from Spawner.
    ///
    /// 1 - Spawn random obstacle (Low Barrier / High Barrier)
    /// 2 - Spawn random item (Energy Buff / Shield / High Jump / Hyper Dash)
    /// 3 - Add Energy (+20)
    /// 4 - Add Follower to relay queue
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

            // When user is typing inside any InputField, ignore debug hotkeys
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
            {
                var selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
                if (selected.GetComponent<TMPro.TMP_InputField>() != null || selected.GetComponent<UnityEngine.UI.InputField>() != null)
                {
                    return;
                }
            }

            // Key 1: Spawn random obstacle
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                SpawnRandomObstacle();
            }

            // Key 2: Spawn random item
            if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                SpawnRandomItem();
            }

            // Key 3: Add Energy
            if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
            {
                AddEnergy();
            }

            // Key 4: Add Follower
            if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)
            {
                AddMockFollower();
            }

            // Key 6: Stop Sign
            if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame)
            {
                SpawnStopSign();
            }

            // Key 7: Traffic Light + Crossing Car
            if (Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame)
            {
                SpawnTrafficLight();
            }

            // Key 8: Falling Hazard
            if (Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame)
            {
                SpawnFallingHazard();
            }

            // Key 9: Bouncing Boulder
            if (Keyboard.current.digit9Key.wasPressedThisFrame || Keyboard.current.numpad9Key.wasPressedThisFrame)
            {
                SpawnBouncingBoulder();
            }

            // Key 0: Test Queue Safe Distance 15m
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
            Debug.Log("[MockInput] Key 3 - Add Energy");
            if (energySystem != null)
            {
                energySystem.AddLike(energyAmount);
            }
        }

        public void AddMockFollower()
        {
            string newFollower = $"Follower_{followerCounter++}";
            Debug.Log($"[MockInput] Key 4 - Add Follower: {newFollower}");

            if (relayQueue != null)
            {
                relayQueue.EnqueueFollower(newFollower);
            }
        }
    }
}
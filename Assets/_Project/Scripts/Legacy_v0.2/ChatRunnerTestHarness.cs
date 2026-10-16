#pragma warning disable CS0618
using UnityEngine;
using UnityEngine.UI;
using SteamRush.Core;
using SteamRush.Features.Runner;
using SteamRush.Features.StreamIntegration;

namespace SteamRush.MinhHuy.ChatRunnerTest
{
    // Personal test harness for ChatRunnerTest.unity.
    // Simulates Like/Chat/Follow/Death via Context Menu + buttons.
    public class ChatRunnerTestHarness : MonoBehaviour
    {
        [Header("Managers")]
        [SerializeField] private FactionTugOfWarManager _factionManager;
        [SerializeField] private ChatRunnerQueueManager _queueManager;

        [Header("Test Buttons")]
        [SerializeField] private Button _fanLikeButton;
        [SerializeField] private Button _antiLikeButton;

        [Header("Simulation Data")]
        [SerializeField] private string _testUserId = "viewer_01";
        [SerializeField] private string _testChatMessage = "left, right, fast";

        private readonly ChatCommandSanitizer _sanitizer = new ChatCommandSanitizer();

        private void Awake()
        {
            if (_fanLikeButton != null)
            {
                _fanLikeButton.onClick.AddListener(SimulateFanLike);
            }

            if (_antiLikeButton != null)
            {
                _antiLikeButton.onClick.AddListener(SimulateAntiLike);
            }
        }

        [ContextMenu("1. Test Sanitize Chat Message")]
        private void SimulateSanitize()
        {
            _sanitizer.SanitizeAndParse(_testChatMessage);
        }

        // Public so test buttons can invoke directly via Button.onClick.AddListener at runtime.
        [ContextMenu("2. Join FAN Faction + 1 Like")]
        public void SimulateFanLike()
        {
            _factionManager.OnChatCommand(_testUserId, "#fan");
            _factionManager.OnLikeReceived(_testUserId);
        }

        [ContextMenu("3. Join ANTI Faction + 1 Like")]
        public void SimulateAntiLike()
        {
            _factionManager.OnChatCommand(_testUserId, "#anti");
            _factionManager.OnLikeReceived(_testUserId);
        }

        [ContextMenu("4. Anti Like x500 (Trigger Car Spawn Threshold)")]
        private void SimulateAntiLikeBurst()
        {
            _factionManager.OnChatCommand(_testUserId, "#anti");
            for (int i = 0; i < 500; i++)
            {
                _factionManager.OnLikeReceived(_testUserId);
            }
        }

        [ContextMenu("5. New Follower Enqueues")]
        private void SimulateFollowerJoin()
        {
            _queueManager.TryEnqueueFollower(_testUserId);
        }

        [ContextMenu("6. Runner Dies (Respawn at Checkpoint)")]
        private void SimulatePlayerDeath()
        {
            EventBus.Publish(new PlayerDeathEvent());
        }
    }
}

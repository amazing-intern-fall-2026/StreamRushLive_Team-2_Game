using UnityEngine;
using SteamRush.Core;
using SteamRush.Features.Runner;
using SteamRush.Features.UI;

namespace SteamRush.Features.StreamIntegration
{
    /// <summary>
    /// Listens for TikTok chat events via EventBus and translates chat commands into in-game gameplay actions.
    /// Handles team selection (Blue / Red), runner movement controls, and anti obstacle placement.
    /// </summary>
    [DisallowMultipleComponent]
    public class TikTokChatAdapter : MonoBehaviour
    {
        [Header("Gameplay Subsystems")]
        [SerializeField] private FactionTugOfWarManager _factionManager;
        [SerializeField] private HUDManager _hudManager;
        [SerializeField] private ChatLaneRunnerController _runnerController;

        [Header("Follower Gate Configuration")]
        [Tooltip("If TRUE: Viewers must follow on TikTok Live to send commands. If FALSE: Anyone can play immediately.")]
        [SerializeField] private bool _requireFollowToPlay = false;

        [Header("Feedback & Diagnostics")]
        [SerializeField] private bool _showPopups = true;
        [SerializeField] private bool _logEvents = true;

        private readonly FollowerGate _followerGate = new FollowerGate();

        public bool RequireFollowToPlay
        {
            get => _requireFollowToPlay;
            set
            {
                _requireFollowToPlay = value;
                FollowerGate.StrictFollowerOnly = value;
            }
        }

        private void Awake()
        {
            EnsureReferences();
            FollowerGate.StrictFollowerOnly = _requireFollowToPlay;
        }

        private void OnEnable()
        {
            EnsureReferences();
            FollowerGate.StrictFollowerOnly = _requireFollowToPlay;
            EventBus.Subscribe<TikTokChatEvent>(OnChatReceived);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TikTokChatEvent>(OnChatReceived);
        }

        public void EnsureReferences()
        {
            if (_factionManager == null) _factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            if (_hudManager == null) _hudManager = FindFirstObjectByType<HUDManager>();
            if (_runnerController == null) _runnerController = FindFirstObjectByType<ChatLaneRunnerController>();
        }

        private void OnChatReceived(TikTokChatEvent evt)
        {
            if (_factionManager == null || _hudManager == null || _runnerController == null)
            {
                EnsureReferences();
            }

            if (string.IsNullOrEmpty(evt.Comment) || string.IsNullOrEmpty(evt.UserId))
            {
                return;
            }

            string displayName = !string.IsNullOrEmpty(evt.DisplayName) ? evt.DisplayName : evt.UserId;
            string trimmedCmd = evt.Comment.Trim().ToLowerInvariant();

            // Follower gate check
            if (_requireFollowToPlay && !_followerGate.CanSendCommand(evt.UserId))
            {
                if (_logEvents)
                {
                    Debug.Log($"[TikTokChatAdapter] {displayName} ({evt.UserId}) is not following - ignored chat command: '{evt.Comment}'.");
                }
                ShowPopup($"[{displayName}] Follow to play!", false);
                return;
            }

            if (_logEvents)
            {
                Debug.Log($"[TikTokChatAdapter] Chat: [{displayName}] '{evt.Comment}'");
            }

            // Team join commands
            if (trimmedCmd == "blue" || trimmedCmd == "red")
            {
                HandleTeamSelection(evt.UserId, displayName, trimmedCmd);
                return;
            }

            // Gameplay command execution based on player faction
            FactionType faction = _factionManager != null ? _factionManager.GetFaction(evt.UserId) : FactionType.Fan;

            if (faction == FactionType.Fan)
            {
                ExecuteFanCommand(displayName, trimmedCmd);
            }
            else
            {
                ExecuteAntiCommand(evt.UserId, displayName, trimmedCmd);
            }
        }

        private void HandleTeamSelection(string userId, string displayName, string command)
        {
            if (_factionManager == null) return;

            bool changed = _factionManager.OnChatCommand(userId, command);
            if (changed)
            {
                bool isFan = _factionManager.GetFaction(userId) == FactionType.Fan;
                string teamName = isFan ? "Blue Team (Runner Support)" : "Red Team (Obstacle Hazard)";
                ShowPopup($"[{displayName}] joined {teamName}!", isFan);
            }
        }

        private void ExecuteFanCommand(string displayName, string command)
        {
            if (command == "1" || command == "2" || command == "3" || command == "left" || command == "right")
            {
                _runnerController?.ExecuteSingleCommand(command);
                _hudManager?.ShowFanAction(displayName, $"Lane {command}");
            }
            else if (command == "jump" || command == "j")
            {
                _runnerController?.ExecuteSingleCommand("jump");
                _hudManager?.ShowFanAction(displayName, "Jump!");
            }
            else if (command == "fast" || command == "speed")
            {
                _runnerController?.ExecuteSingleCommand("fast");
                _hudManager?.ShowFanAction(displayName, "Turbo Boost!");
            }
        }

        private void ExecuteAntiCommand(string userId, string displayName, string command)
        {
            if (command == "1" || command == "2" || command == "3")
            {
                if (int.TryParse(command, out int laneIndex))
                {
                    bool spawned = _factionManager != null && _factionManager.TrySpawnAntiObstacleCar(userId, laneIndex);
                    if (spawned)
                    {
                        _hudManager?.ShowAntiAction(displayName, $"Car on Lane {laneIndex}!");
                    }
                }
            }
        }

        private void ShowPopup(string message, bool isFan)
        {
            if (!_showPopups) return;
            _hudManager?.ShowStatusPopup(message, isFan);
        }
    }
}

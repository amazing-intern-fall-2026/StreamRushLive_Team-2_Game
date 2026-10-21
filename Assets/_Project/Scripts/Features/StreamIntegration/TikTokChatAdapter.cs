using System.Collections.Generic;
using UnityEngine;
using SteamRush.Core;
using SteamRush.Features.Runner;
using SteamRush.Features.UI;
using SteamRush.Features.UI.PreGameConfig;

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

        private readonly ChatCommandSanitizer _sanitizer = new ChatCommandSanitizer();

        private void OnChatReceived(TikTokChatEvent evt)
        {
            // Do not execute chat commands while the streamer is actively configuring settings in the modal
            if (PreGameConfigManager.Instance != null && PreGameConfigManager.Instance.IsOpen)
            {
                return;
            }

            if (!PreGameConfigManager.IsLiveSessionActive && PreGameConfigManager.Instance != null && !PreGameConfigManager.Instance.IsTestModeActive)
            {
                return;
            }

            if (_factionManager == null || _hudManager == null || _runnerController == null)
            {
                EnsureReferences();
            }

            if (string.IsNullOrEmpty(evt.Comment) || string.IsNullOrEmpty(evt.UserId))
            {
                return;
            }

            string displayName = !string.IsNullOrEmpty(evt.DisplayName) ? evt.DisplayName : evt.UserId;

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

            List<string> commands = _sanitizer.SanitizeAndParse(evt.Comment);
            if (commands == null || commands.Count == 0) return;

            // Check if first command is team selection
            string primaryCmd = commands[0];
            if (primaryCmd == "blue" || primaryCmd == "red")
            {
                HandleTeamSelection(evt.UserId, displayName, primaryCmd);
                return;
            }

            // Gameplay command execution based on player faction
            FactionType faction = _factionManager != null ? _factionManager.GetFaction(evt.UserId) : FactionType.Blue;

            foreach (string cmd in commands)
            {
                if (faction == FactionType.Blue)
                {
                    ExecuteBlueCommand(displayName, cmd);
                }
                else
                {
                    ExecuteRedCommand(evt.UserId, displayName, cmd);
                }
            }
        }

        private void HandleTeamSelection(string userId, string displayName, string command)
        {
            if (_factionManager == null) return;

            bool isBlue = command == "blue";
            bool changed = _factionManager.OnChatCommand(userId, command);
            if (changed)
            {
                string teamName = isBlue ? "Blue Team (Runner Support)" : "Red Team (Obstacle Hazard)";
                ShowPopup($"[{displayName}] joined {teamName}!", isBlue);
                if (isBlue) _hudManager?.ShowFanAction(displayName, "Joined Blue Team");
                else _hudManager?.ShowAntiAction(displayName, "Joined Red Team");
            }
            else
            {
                if (isBlue) _hudManager?.ShowFanAction(displayName, "Team Blue!");
                else _hudManager?.ShowAntiAction(displayName, "Team Red!");
            }
        }

        private void ExecuteBlueCommand(string displayName, string command)
        {
            if (_runnerController != null && _runnerController.IsChatControlLocked)
            {
                if (_logEvents)
                {
                    Debug.LogWarning("[TikTokChatAdapter] Chat control locked: Runner is configured for manual keyboard control only.");
                }
                return;
            }

            if (command == "1" || command == "2" || command == "3" || command == "left" || command == "right")
            {
                _runnerController?.ExecuteSingleCommand(command);
                string laneDesc = command switch
                {
                    "1" or "left" => "Lane 1 (Left)",
                    "2" => "Lane 2 (Mid)",
                    "3" or "right" => "Lane 3 (Right)",
                    _ => $"Lane {command}"
                };
                _hudManager?.ShowSupportAction(displayName, laneDesc);
            }
            else if (command == "jump")
            {
                _runnerController?.ExecuteSingleCommand("jump");
                _hudManager?.ShowSupportAction(displayName, "Jump!");
            }
            else if (command == "fast")
            {
                _runnerController?.ExecuteSingleCommand("fast");
                _hudManager?.ShowSupportAction(displayName, "Turbo Boost!");
            }
        }

        private void ExecuteFanCommand(string displayName, string command) => ExecuteBlueCommand(displayName, command);

        private void ExecuteRedCommand(string userId, string displayName, string command)
        {
            int laneIndex = command switch
            {
                "1" or "left" => 1,
                "2" => 2,
                "3" or "right" => 3,
                _ => -1
            };

            if (laneIndex >= 1 && laneIndex <= 3)
            {
                bool spawned = _factionManager != null && _factionManager.TrySpawnObstacleVehicle(userId, laneIndex);
                if (spawned)
                {
                    string laneName = laneIndex == 1 ? "1 (Left)" : (laneIndex == 2 ? "2 (Mid)" : "3 (Right)");
                    _hudManager?.ShowHazardAction(displayName, $"Car on Lane {laneName}!");
                }
                else
                {
                    _hudManager?.ShowHazardAction(displayName, $"Need Energy ({_factionManager?.AntiLikes ?? 0})");
                }
            }
        }

        private void ExecuteAntiCommand(string userId, string displayName, string command) => ExecuteRedCommand(userId, displayName, command);

        private void ShowPopup(string message, bool isBlue)
        {
            if (!_showPopups) return;
            _hudManager?.ShowStatusPopup(message, isBlue);
        }
    }
}

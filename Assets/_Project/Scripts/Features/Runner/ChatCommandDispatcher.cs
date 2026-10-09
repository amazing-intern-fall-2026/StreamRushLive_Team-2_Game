using System.Collections.Generic;
using UnityEngine;
using StreamRushLive.Features.Spawning;
using SteamRush.Features.StreamIntegration;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Parses, sanitizes, and dispatches viewer chat commands to the runner and faction systems.
    /// Extracted from MockChatConsole to adhere to the Single Responsibility Principle (SRP).
    /// </summary>
    public class ChatCommandDispatcher : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private ChatLaneRunnerController chatLaneRunner;
        [SerializeField] private FactionTugOfWarManager factionManager;
        [SerializeField] private SteamRush.Features.UI.HUDManager hudManager;

        private readonly ChatCommandSanitizer _sanitizer = new ChatCommandSanitizer();
        private int _lastSubmittedFrame = -1;
        private int _mockFollowerIndex = 0;
        private string _lastActiveFollowerName = "";

        private static readonly string[] MockFollowerList = new string[]
        {
            "Viewer_Bao",
            "Viewer_Chi",
            "Top1_Dung",
            "Mod_Giang",
            "Gamer_Huy"
        };

        public void AutoWireReferences()
        {
            if (chatLaneRunner == null) chatLaneRunner = GetComponent<ChatLaneRunnerController>() ?? GetComponentInParent<ChatLaneRunnerController>() ?? FindFirstObjectByType<ChatLaneRunnerController>();
            if (factionManager == null) factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            if (hudManager == null) hudManager = FindFirstObjectByType<SteamRush.Features.UI.HUDManager>();
        }

        private string GetOrRotateFollowerName()
        {
            string name = MockFollowerList[_mockFollowerIndex % MockFollowerList.Length];
            _mockFollowerIndex++;
            return name;
        }

        public (string followerName, string command) ParseFollowerAndInput(string rawInput)
        {
            string trimmed = rawInput.Trim();

            int colonIdx = trimmed.IndexOf(':');
            if (colonIdx > 0 && colonIdx < trimmed.Length - 1)
            {
                string name = trimmed.Substring(0, colonIdx).Trim();
                string cmd = trimmed.Substring(colonIdx + 1).Trim();
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(cmd))
                {
                    _lastActiveFollowerName = name;
                    return (name, cmd);
                }
            }

            int firstSpace = trimmed.IndexOf(' ');
            if (firstSpace > 0)
            {
                string firstWord = trimmed.Substring(0, firstSpace).Trim();
                string rest = trimmed.Substring(firstSpace + 1).Trim();
                string firstLower = firstWord.ToLowerInvariant();

                bool isFirstWordCommand = firstLower == "#fan" || firstLower == "#anti" || firstLower == "anti" ||
                                          firstLower == "#blue" || firstLower == "#red" ||
                                          firstLower == "left" || firstLower == "right" || firstLower == "fast" ||
                                          firstLower == "1" || firstLower == "2" || firstLower == "3";

                if (!isFirstWordCommand && !string.IsNullOrEmpty(rest))
                {
                    _lastActiveFollowerName = firstWord;
                    return (firstWord, rest);
                }
            }

            string cmdLower = trimmed.ToLowerInvariant();
            if (cmdLower == "#fan" || cmdLower == "#blue" || cmdLower == "blue" || cmdLower == "fan" ||
                cmdLower == "#anti" || cmdLower == "#red" || cmdLower == "red" || cmdLower == "anti")
            {
                string newFollower = GetOrRotateFollowerName();
                _lastActiveFollowerName = newFollower;
                return (newFollower, trimmed);
            }

            if (string.IsNullOrEmpty(_lastActiveFollowerName))
            {
                _lastActiveFollowerName = GetOrRotateFollowerName();
            }

            return (_lastActiveFollowerName, trimmed);
        }

        public void SimulateViewerChat(string viewerName, string command)
        {
            _lastSubmittedFrame = -1;
            DispatchCommand($"{viewerName} {command}");
        }

        public void ExecuteChatCommand(string rawInput)
        {
            _lastSubmittedFrame = -1;
            DispatchCommand(rawInput);
        }

        public void DispatchCommand(string rawInput)
        {
            if (Time.frameCount == _lastSubmittedFrame)
            {
                return;
            }
            _lastSubmittedFrame = Time.frameCount;

            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return;
            }

            AutoWireReferences();

            var (followerName, command) = ParseFollowerAndInput(rawInput);
            string trimmedCmd = command.Trim().ToLowerInvariant();

            if (trimmedCmd == "blue" || trimmedCmd == "#blue")
            {
                bool isSwitching = factionManager != null && factionManager.HasFaction(followerName) && factionManager.GetFaction(followerName) == FactionType.Anti;
                factionManager?.SetFaction(followerName, FactionType.Fan);
                string actionText = isSwitching ? "Switched to Blue Team" : "Joined Blue Team";
                hudManager?.ShowFanAction(followerName, actionText);
                return;
            }
            else if (trimmedCmd == "red" || trimmedCmd == "#red")
            {
                bool isSwitching = factionManager != null && factionManager.HasFaction(followerName) && factionManager.GetFaction(followerName) == FactionType.Fan;
                factionManager?.SetFaction(followerName, FactionType.Anti);
                string actionText = isSwitching ? "Switched to Red Team" : "Joined Red Team";
                hudManager?.ShowAntiAction(followerName, actionText);
                return;
            }

            int antiLane = -1;
            if (factionManager != null && factionManager.GetFaction(followerName) == FactionType.Anti)
            {
                if (trimmedCmd == "1") antiLane = 1;
                else if (trimmedCmd == "2") antiLane = 2;
                else if (trimmedCmd == "3") antiLane = 3;
            }

            if (antiLane != -1)
            {
                var obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();
                if (obstacleSpawner != null)
                {
                    if (!obstacleSpawner.CanSpawnObstacle())
                    {
                        hudManager?.ShowAntiAction(followerName, "Max 2 Cars On Track!");
                        return;
                    }

                    if (!obstacleSpawner.CanSpawnObstacleOnLane(antiLane))
                    {
                        string occupiedLaneName = antiLane == 1 ? "1 (Left)" : (antiLane == 2 ? "2 (Mid)" : "3 (Right)");
                        hudManager?.ShowAntiAction(followerName, $"Lane {occupiedLaneName} Occupied!");
                        return;
                    }
                }

                if (factionManager != null)
                {
                    bool success = factionManager.TrySpawnAntiObstacleCar(followerName, antiLane);
                    if (success)
                    {
                        string laneName = antiLane == 1 ? "Left (1)" : (antiLane == 2 ? "Mid (2)" : "Right (3)");
                        hudManager?.ShowAntiAction(followerName, $"Spawned Car (Lane {laneName})");
                    }
                    else
                    {
                        hudManager?.ShowAntiAction(followerName, $"Need 100 Energy ({factionManager.AntiLikes})");
                        hudManager?.ShowStatusPopup("Out of Energy!", false);
                    }
                }
                return;
            }

            List<string> commands = _sanitizer.SanitizeAndParse(command);
            if (commands != null && commands.Count > 0)
            {
                if (chatLaneRunner != null)
                {
                    if (chatLaneRunner.IsControlLocked)
                    {
                        return;
                    }

                    chatLaneRunner.ExecuteCommands(commands);

                    string firstCmd = commands[0].ToLowerInvariant();
                    string actionDesc = firstCmd switch
                    {
                        "1" => "Lane 1 (Left)",
                        "2" => "Lane 2 (Mid)",
                        "3" => "Lane 3 (Right)",
                        "jump" or "j" => "Jump",
                        _ => $"Cmd: {firstCmd}"
                    };
                    hudManager?.ShowFanAction(followerName, actionDesc);
                }
                else
                {
                    Debug.LogWarning("[ChatCommandDispatcher] ChatLaneRunnerController not found to execute command!");
                }
            }
        }
    }
}

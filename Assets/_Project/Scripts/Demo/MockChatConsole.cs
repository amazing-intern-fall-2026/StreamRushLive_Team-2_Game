using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Central Manager and Facade for the Mock Chat & Live Simulation System.
    /// Coordinates modular sub-components according to the Single Responsibility Principle (SRP):
    /// - ChatCommandDispatcher: Sanitizes and routes viewer chat commands.
    /// - MockRunnerActionHandler: Executes simulated runner buffs, obstacles, and energy adjustments.
    /// - MockDebugUIPanel: Manages vertical debug button layouts and UI toggling.
    /// </summary>
    public class MockChatConsole : MonoBehaviour
    {
        public static MockChatConsole Instance { get; private set; }

        [Header("Modular Sub-Components (SRP)")]
        [SerializeField] private MockRunnerActionHandler actionHandler;
        [SerializeField] private ChatCommandDispatcher commandDispatcher;
        [SerializeField] private MockDebugUIPanel debugUIPanel;

        [Header("Input Field")]
        [SerializeField] private TMP_InputField chatInputField;

        private void Awake()
        {
            Instance = this;
            EnsureSubComponents();
            SetupListeners();
        }

        private void EnsureSubComponents()
        {
            if (actionHandler == null)
            {
                actionHandler = GetComponent<MockRunnerActionHandler>() ?? gameObject.AddComponent<MockRunnerActionHandler>();
            }
            actionHandler.AutoWireReferences();

            if (commandDispatcher == null)
            {
                commandDispatcher = GetComponent<ChatCommandDispatcher>() ?? gameObject.AddComponent<ChatCommandDispatcher>();
            }
            commandDispatcher.AutoWireReferences();

            if (debugUIPanel == null)
            {
                debugUIPanel = GetComponent<MockDebugUIPanel>() ?? gameObject.AddComponent<MockDebugUIPanel>();
            }
        }

        private void SetupListeners()
        {
            if (chatInputField != null)
            {
                chatInputField.onSubmit.RemoveAllListeners();
                chatInputField.onSubmit.AddListener(OnChatSubmitted);
            }
            else
            {
                Debug.LogWarning("[MockChatConsole] Chat Input Field not assigned!");
            }

            debugUIPanel?.Initialize(
                chatInputField,
                onToggleDebug: ToggleDebugUI,
                onBlueEnergyChanged: amt => actionHandler?.DebugIncreaseBlueEnergy(amt),
                onRedEnergyChanged: amt => actionHandler?.DebugIncreaseRedEnergy(amt),
                onFreeControl: () => MockActivateFreeControl(),
                onLiveDemo: () => ToggleLiveDemoSimulation(),
                onFinishApproach: () => MockTriggerFinishLineApproach(50f)
            );
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            // When user is typing inside any InputField, ignore all debug hotkeys
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected.GetComponent<TMP_InputField>() != null || selected.GetComponent<UnityEngine.UI.InputField>() != null)
                {
                    return;
                }
            }

            // Also ignore all debug hotkeys when PreGameConfig setup modal is open
            if (SteamRush.Features.UI.PreGameConfig.PreGameConfigManager.Instance != null && SteamRush.Features.UI.PreGameConfig.PreGameConfigManager.Instance.IsOpen)
            {
                return;
            }

            bool isShift = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;

            if (Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                ToggleDebugUI();
            }

            if (Keyboard.current.f12Key.wasPressedThisFrame)
            {
                MockTriggerFinishLineApproach(50f);
            }

            // F1: Shield buff. Shift+F1: Free-Control Buff (30s).
            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                if (isShift) MockActivateFreeControl();
                else MockDonateShield();
            }

            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                if (isShift) MockToggleSprintBuffDebug();
                else MockActivateSprintBuff();
            }

            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                MockSpawnSedanCar();
            }

            if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                if (isShift) MockActivatePickupTruckPhase();
                else MockSpawnPickupTruck();
            }

            if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                if (isShift) MockNewFollower();
                else
                {
                    if (Keyboard.current.ctrlKey.isPressed) MockActivateHeavyTruckPhase();
                    else MockSpawnHeavyTruck();
                }
            }

            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                if (isShift) actionHandler?.DebugDecreaseBlueEnergy(10);
                else actionHandler?.DebugIncreaseBlueEnergy(10);
            }

            if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
                MockActivateUnlimitedObstacles();
            }

            if (Keyboard.current.f8Key.wasPressedThisFrame)
            {
                MockGiftDance();
            }

            if (Keyboard.current.f9Key.wasPressedThisFrame)
            {
                MockWeatherHazard();
            }

            if (Keyboard.current.f10Key.wasPressedThisFrame)
            {
                MockBuyVipTicket();
            }

            if (Keyboard.current.f11Key.wasPressedThisFrame)
            {
                if (isShift) actionHandler?.DebugDecreaseRedEnergy(10);
                else actionHandler?.DebugIncreaseRedEnergy(10);
            }

            if (Keyboard.current.lKey.wasPressedThisFrame)
            {
                ToggleLiveDemoSimulation();
            }

            // Enter key to focus/submit chat
            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                if (chatInputField != null)
                {
                    if (chatInputField.isFocused)
                    {
                        string text = chatInputField.text;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            OnChatSubmitted(text);
                        }
                        else
                        {
                            ClearInputField();
                        }
                    }
                    else
                    {
                        if (debugUIPanel != null && !debugUIPanel.IsDebugUIVisible)
                        {
                            ToggleDebugUI();
                        }
                        chatInputField.ActivateInputField();
                    }
                }
            }
        }

        private void OnChatSubmitted(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                ClearInputField();
                return;
            }

            commandDispatcher?.DispatchCommand(rawInput);
            ClearInputField();
        }

        private void ClearInputField()
        {
            if (chatInputField == null) return;
            chatInputField.text = "";
            chatInputField.DeactivateInputField();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        #region Facade Delegation Methods

        public void ExecuteChatCommand(string rawInput) => commandDispatcher?.ExecuteChatCommand(rawInput);
        public void SimulateViewerChat(string viewerName, string command) => commandDispatcher?.SimulateViewerChat(viewerName, command);

        public void ToggleDebugUI() => debugUIPanel?.ToggleDebugUI();
        public void SetDebugUIVisible(bool visible) => debugUIPanel?.SetDebugUIVisible(visible);

        public void MockDonateShield(string sender = "Viewer") => actionHandler?.MockDonateShield(sender);
        public void MockActivateSprintBuff(string sender = "Blue Team") => actionHandler?.MockActivateSprintBuff(sender);
        [System.Obsolete("Use MockActivateSprintBuff instead.")]
        public void MockActivateFanSprintBuff(string sender = "Blue Team") => MockActivateSprintBuff(sender);
        public void MockActivateFreeControl(string sender = "Blue Team") => actionHandler?.MockActivateFreeControl(sender);
        public void MockToggleSprintBuffDebug() => actionHandler?.MockToggleSprintBuffDebug();

        public void MockSpawnSedanCar(string sender = "Red Team") => actionHandler?.MockSpawnSedanCar(sender);
        public void MockSpawnPickupTruck(string sender = "Red Team") => actionHandler?.MockSpawnPickupTruck(sender);
        public void MockActivatePickupTruckPhase(string sender = "Red Team") => actionHandler?.MockActivatePickupTruckPhase(sender);
        public void MockSpawnHeavyTruck(string sender = "Red Team") => actionHandler?.MockSpawnHeavyTruck(sender);
        public void MockActivateHeavyTruckPhase(string sender = "Red Team") => actionHandler?.MockActivateHeavyTruckPhase(sender);

        public void MockBlueEnergyBottle(string sender = "Viewer_Blue") => actionHandler?.MockBlueEnergyBottle(sender);
        [System.Obsolete("Use MockBlueEnergyBottle instead.")]
        public void MockFanEnergyBottle(string sender = "Viewer_Blue") => MockBlueEnergyBottle(sender);

        public void MockRedEnergyBottle(string sender = "Viewer_Red") => actionHandler?.MockRedEnergyBottle(sender);
        [System.Obsolete("Use MockRedEnergyBottle instead.")]
        public void MockAntiEnergyBottle(string sender = "Viewer_Red") => MockRedEnergyBottle(sender);

        public void MockNewFollower(string customUserId = null) => actionHandler?.MockNewFollower(customUserId);
        public void MockActivateUnlimitedObstacles(string sender = "Red Team") => actionHandler?.MockActivateUnlimitedObstacles(sender);
        [System.Obsolete("Use MockActivateUnlimitedObstacles instead.")]
        public void MockActivateAntiCarUnlimited(string sender = "Red Team") => MockActivateUnlimitedObstacles(sender);

        public void MockGiftDance(string sender = "Viewer") => actionHandler?.MockGiftDance(sender);
        public void MockWeatherHazard(string sender = "Viewer_Red") => actionHandler?.MockWeatherHazard(sender);
        public void MockBuyVipTicket(string customUserId = null) => actionHandler?.MockBuyVipTicket(customUserId);
        public void MockToggleUnlimitedModeDebug() => actionHandler?.MockToggleUnlimitedModeDebug();

        public void DebugIncreaseBlueEnergy(int amount = 10) => actionHandler?.DebugIncreaseBlueEnergy(amount);
        [System.Obsolete("Use DebugIncreaseBlueEnergy instead.")]
        public void DebugIncreaseFanEnergy(int amount = 10) => DebugIncreaseBlueEnergy(amount);

        public void DebugDecreaseBlueEnergy(int amount = 10) => actionHandler?.DebugDecreaseBlueEnergy(amount);
        [System.Obsolete("Use DebugDecreaseBlueEnergy instead.")]
        public void DebugDecreaseFanEnergy(int amount = 10) => DebugDecreaseBlueEnergy(amount);

        public void DebugIncreaseRedEnergy(int amount = 10) => actionHandler?.DebugIncreaseRedEnergy(amount);
        [System.Obsolete("Use DebugIncreaseRedEnergy instead.")]
        public void DebugIncreaseAntiEnergy(int amount = 10) => DebugIncreaseRedEnergy(amount);

        public void DebugDecreaseRedEnergy(int amount = 10) => actionHandler?.DebugDecreaseRedEnergy(amount);
        [System.Obsolete("Use DebugDecreaseRedEnergy instead.")]
        public void DebugDecreaseAntiEnergy(int amount = 10) => DebugDecreaseRedEnergy(amount);

        public void MockTriggerFinishLineApproach(float distanceAhead = 50f) => actionHandler?.MockTriggerFinishLineApproach(distanceAhead);
        public void ToggleLiveDemoSimulation() => actionHandler?.ToggleLiveDemoSimulation();

        #endregion
    }
}
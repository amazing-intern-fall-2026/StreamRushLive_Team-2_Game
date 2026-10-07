using UnityEngine;
using SteamRush.Features.Runner;
using SteamRush.Features.StreamIntegration;

namespace SteamRush.Features.UI
{
    // Event binder connecting logic managers (FactionTugOfWarManager, ChatRunnerQueueManager)
    // with view components (FactionTugOfWarUI, WaitingPanel) adhering to SRP and decoupling.
    public class ChatRunnerUIBinder : MonoBehaviour
    {
        [Header("Faction Tug-of-War")]
        [SerializeField] private FactionTugOfWarManager _factionManager;
        [SerializeField] private FactionTugOfWarUI _factionUI;

        [Header("Empty Queue Hold")]
        [SerializeField] private ChatRunnerQueueManager _queueManager;
        [SerializeField] private GameObject _waitingPanel;

        private void Awake()
        {
            if (_factionManager != null && _factionUI != null)
            {
                _factionManager.FactionValuesChanged.AddListener(_factionUI.SetFactionValues);
                _factionManager.FactionMemberCountsChanged.AddListener(_factionUI.SetMemberCounts);
            }

            if (_queueManager != null && _waitingPanel != null)
            {
                _queueManager.WaitingStateChanged.AddListener(_waitingPanel.SetActive);
            }
        }
    }
}

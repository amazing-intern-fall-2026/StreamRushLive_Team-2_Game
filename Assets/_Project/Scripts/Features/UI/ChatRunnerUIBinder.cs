using UnityEngine;
using SteamRush.Features.Runner;
using SteamRush.Features.StreamIntegration;

namespace SteamRush.Features.UI
{
    // "Day noi" bat buoc giua Manager (logic) va View (UI thuan) qua UnityEvent co san trong
    // Inspector - KHONG phai file test, phai co mat trong scene tich hop that. Khong co script
    // nay thi FactionTugOfWarUI/WaitingForFollowerUI se KHONG BAO GIO tu cap nhat du Manager
    // chay dung, vi ban than Manager/View khong tu tham chieu nhau (SRP, xem comment trong
    // FactionTugOfWarUI.cs va FactionTugOfWarManager.cs).
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
            }

            if (_queueManager != null && _waitingPanel != null)
            {
                _queueManager.WaitingStateChanged.AddListener(_waitingPanel.SetActive);
            }
        }
    }
}

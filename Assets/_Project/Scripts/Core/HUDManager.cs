using UnityEngine;
using SteamRush.Features.UI.Views;

namespace SteamRush.Features.UI
{
    // Facade pattern: Single entry point for external modules (RelayQueue, StreamIntegration...)
    // to update HUD elements. Coordinates views (ProgressBar, EnergyBar, RunnerNameplate) adhering to SRP.
    public class HUDManager : MonoBehaviour
    {
        [SerializeField] private ProgressBarController progressBar;
        [SerializeField] private EnergyBarController energyBar;
        [SerializeField] private RunnerNameplateController runnerNameplate;
        [SerializeField] private StatusPopupSpawner statusPopupSpawner;
        [SerializeField] private GiftToastQueue giftToastQueue;
        [SerializeField] private GiftToastQueue topBannerQueue;

        [Header("Dual-Wing Action Feeds")]
        [SerializeField] private GiftToastQueue fanFeedQueue;
        [SerializeField] private GiftToastQueue antiFeedQueue;

        [Header("Next Runner HUD / Preview")]
        [SerializeField] private TMPro.TMP_Text nextRunnerLabel;
        [SerializeField] private Color nextRunnerNormalColor = Color.white;
        [SerializeField] private Color nextRunnerVipColor = new Color(1f, 0.85f, 0.1f, 1f);

        // Synchronized accent color with Fan faction palette (Fan_Bg Outline / FactionTugOfWarUI)
        [SerializeField] private Color _buffAccentColor = new Color(0.35f, 0.75f, 1f, 1f);
        [SerializeField] private Color _debuffAccentColor = new Color(1f, 0.3f, 0.25f, 1f);

        private static readonly System.Text.RegularExpressions.Regex EmojiRegex = new System.Text.RegularExpressions.Regex(
            @"[\uD83C-\uDBFF\uDC00-\uDFFF\u2600-\u27BF\u2300-\u23FF\u2B50-\u2B55\uFE0F]",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        // Displays current leg distance progress (m / targetMeters) supplied by TrackProgressTracker
        public void UpdateLegProgress(float currentMeters, float targetMeters)
        {
            if (progressBar == null)
            {
                Debug.LogWarning("[HUDManager] ProgressBarController is not assigned in Inspector - skipping UpdateLegProgress.");
                return;
            }

            progressBar.SetLegProgress(currentMeters, targetMeters);
        }

        // currentEnergy: normalized 0..1 value provided by caller.
        public void UpdateEnergy(float currentEnergy)
        {
            if (energyBar == null)
            {
                Debug.LogWarning("[HUDManager] EnergyBarController is not assigned in Inspector - skipping UpdateEnergy.");
                return;
            }

            energyBar.SetEnergy(currentEnergy);
        }

        // Updates name + avatar + VIP status on the world-space nameplate of the active runner.
        public void UpdateRunnerInfo(string name, Sprite avatar, bool isVip = false)
        {
            if (runnerNameplate == null)
            {
                Debug.LogWarning("[HUDManager] RunnerNameplateController is not assigned in Inspector - skipping UpdateRunnerInfo.");
                return;
            }

            runnerNameplate.SetRunnerInfo(name, avatar, isVip);
        }

        public void UpdateRunnerInfo(string name, Sprite avatar)
        {
            UpdateRunnerInfo(name, avatar, false);
        }

        // Updates next runner preview on HUD
        public void UpdateNextRunnerPreview(string name, bool isVip)
        {
            if (nextRunnerLabel == null) return;

            if (string.IsNullOrEmpty(name))
            {
                nextRunnerLabel.text = "<color=#888888>(Empty)</color>";
                nextRunnerLabel.color = Color.gray;
            }
            else
            {
                nextRunnerLabel.text = name;
                nextRunnerLabel.color = isVip ? nextRunnerVipColor : nextRunnerNormalColor;
            }
        }

        // Assigns target transform for world-space nameplate tracking above runner character.
        public void UpdateRunnerTarget(Transform runner)
        {
            if (runnerNameplate == null)
            {
                Debug.LogWarning("[HUDManager] RunnerNameplateController is not assigned in Inspector - skipping UpdateRunnerTarget.");
                return;
            }

            runnerNameplate.SetTarget(runner);
        }

        // Displays notification on Top Banner / Status Popup with faction-colored styling.
        public void ShowStatusPopup(string message, bool isBuff, Sprite icon = null, Color? iconColor = null)
        {
            if (string.IsNullOrEmpty(message)) return;

            string lower = message.ToLowerInvariant();

            // Suppress redundant notifications on Top Banner
            if (lower.Contains("out of energy") || 
                lower.Contains("not enough energy") || 
                lower.Contains("ended - sedan") || 
                lower.Contains("distance!"))
            {
                return;
            }

            bool isBlueTeam = isBuff;
            if (lower.Contains("blue") || lower.Contains("fan") || lower.Contains("free control") || lower.Contains("shield") || lower.Contains("sprint"))
            {
                isBlueTeam = true;
            }
            else if (lower.Contains("red") || lower.Contains("anti") || lower.Contains("car") || lower.Contains("truck") || lower.Contains("sedan") || lower.Contains("pickup") || lower.Contains("beast") || lower.Contains("train"))
            {
                isBlueTeam = false;
            }

            // Strip [Red Team] and [Blue Team] prefixes for cleaner display
            message = System.Text.RegularExpressions.Regex.Replace(message, @"\[(Blue|Red)\s*Team\]\s*:?\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            message = EmojiRegex.Replace(message, "").Trim();

            if (statusPopupSpawner != null)
            {
                statusPopupSpawner.Spawn(message, isBlueTeam, icon, iconColor);
            }

            // Top Banner display for general game notifications.
            if (topBannerQueue == null)
            {
                Debug.LogWarning("[HUDManager] topBannerQueue is not assigned in Inspector - skipping Top Banner for ShowStatusPopup.");
                return;
            }

            Color accent = isBlueTeam ? _buffAccentColor : _debuffAccentColor;
            topBannerQueue.Show(string.Empty, message, icon, iconColor, accent);
        }

        private void Awake()
        {
            // Disable side comment notification containers
            if (fanFeedQueue != null) fanFeedQueue.gameObject.SetActive(false);
            if (antiFeedQueue != null) antiFeedQueue.gameObject.SetActive(false);
        }

        // Gift Toast notifications disabled per design
        public void ShowGiftToast(string viewerName, string itemName, Sprite giftIcon, Color? iconColor = null)
        {
            // Disabled
        }

        /// <summary>
        /// Deprecated: Fan team chat notifications disabled per design.
        /// </summary>
        public void ShowFanAction(string sender, string action, Sprite icon = null)
        {
            // Disabled: Fan team comment notification suppressed
        }

        /// <summary>
        /// Deprecated: Anti team chat notifications disabled per design.
        /// </summary>
        public void ShowAntiAction(string sender, string action, Sprite icon = null)
        {
            // Disabled: Anti team comment notification suppressed
        }
    }
}

using UnityEngine;
using SteamRush.Features.UI.Views;

namespace SteamRush.Features.UI
{
    // Facade: điểm gọi vào duy nhất cho các module khác (RelayQueue, StreamIntegration...) khi cần
    // cập nhật HUD. Bản thân HUDManager không chứa logic hiển thị, chỉ điều phối tới từng View
    // (ProgressBarController/EnergyBarController/RunnerNameplateController) - mỗi View chỉ lo
    // đúng 1 việc (SRP), không View nào gọi chéo sang View khác hay module khác.
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

        // Xanh duong dong bo voi mau Phe Fan (Fan_Bg Outline / FactionTugOfWarUI) thay vi xanh la.
        [SerializeField] private Color _buffAccentColor = new Color(0.35f, 0.75f, 1f, 1f);
        [SerializeField] private Color _debuffAccentColor = new Color(1f, 0.3f, 0.25f, 1f);

        // GDD v1.3.1 muc 7: hien thi cu ly theo met CUA CHANG hien tai (khong phai tong 100km).
        // currentMeters/targetMeters do TrackProgressTracker cung cap (CurrentLegDistanceMeters/RelayDistanceMeters).
        public void UpdateLegProgress(float currentMeters, float targetMeters)
        {
            if (progressBar == null)
            {
                Debug.LogWarning("[HUDManager] Chưa gán ProgressBarController trong Inspector - bỏ qua UpdateLegProgress.");
                return;
            }

            progressBar.SetLegProgress(currentMeters, targetMeters);
        }

        // currentEnergy: giá trị đã chuẩn hoá 0..1, phía gọi (module Like/Energy) tự tính trước khi truyền vào.
        public void UpdateEnergy(float currentEnergy)
        {
            if (energyBar == null)
            {
                Debug.LogWarning("[HUDManager] Chưa gán EnergyBarController trong Inspector - bỏ qua UpdateEnergy.");
                return;
            }

            energyBar.SetEnergy(currentEnergy);
        }

        // Cập nhật tên + avatar + trạng thái VIP hiển thị trên bảng tên world-space của runner đang chạy hiện tại.
        public void UpdateRunnerInfo(string name, Sprite avatar, bool isVip = false)
        {
            if (runnerNameplate == null)
            {
                Debug.LogWarning("[HUDManager] Chưa gán RunnerNameplateController trong Inspector - bỏ qua UpdateRunnerInfo.");
                return;
            }

            runnerNameplate.SetRunnerInfo(name, avatar, isVip);
        }

        public void UpdateRunnerInfo(string name, Sprite avatar)
        {
            UpdateRunnerInfo(name, avatar, false);
        }

        // Cập nhật khung xem trước Next Runner trên HUD (nếu có label liên kết)
        public void UpdateNextRunnerPreview(string name, bool isVip)
        {
            if (nextRunnerLabel == null) return;

            if (string.IsNullOrEmpty(name))
            {
                nextRunnerLabel.text = "<color=#888888>(Trống)</color>";
                nextRunnerLabel.color = Color.gray;
            }
            else
            {
                nextRunnerLabel.text = name;
                nextRunnerLabel.color = isVip ? nextRunnerVipColor : nextRunnerNormalColor;
            }
        }

        // Gán Transform runner hiện tại để bảng tên world-space biết vị trí cần bám theo phía trên đầu.
        // Cần thiết để yêu cầu "nameplate lơ lửng trên đầu nhân vật" hoạt động; module RelayQueue
        // (chưa có) sẽ là nơi gọi hàm này mỗi khi chuyển gậy sang runner mới.
        public void UpdateRunnerTarget(Transform runner)
        {
            if (runnerNameplate == null)
            {
                Debug.LogWarning("[HUDManager] Chưa gán RunnerNameplateController trong Inspector - bỏ qua UpdateRunnerTarget.");
                return;
            }

            runnerNameplate.SetTarget(runner);
        }

        // Hiện thông báo nổi lên trên Top Banner / Status Popup.
        // Tự động loại bỏ các tiền tố "[Red Team]", "[Blue Team]" và gán màu sắc xanh/đỏ tương ứng.
        public void ShowStatusPopup(string message, bool isBuff, Sprite icon = null, Color? iconColor = null)
        {
            if (string.IsNullOrEmpty(message)) return;

            string lower = message.ToLowerInvariant();

            // Loại bỏ hoàn toàn các thông báo rác / dư thừa không cần thiết lên Top Banner
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
            else if (lower.Contains("red") || lower.Contains("anti") || lower.Contains("car") || lower.Contains("truck") || lower.Contains("sedan") || lower.Contains("pickup"))
            {
                isBlueTeam = false;
            }

            // Loại bỏ hoàn toàn tiền tố [Red Team] và [Blue Team]
            message = System.Text.RegularExpressions.Regex.Replace(message, @"\[(Blue|Red)\s*Team\]\s*:?\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

            if (statusPopupSpawner != null)
            {
                statusPopupSpawner.Spawn(message, isBlueTeam, icon, iconColor);
            }

            // Top Banner (giữa trên cùng) - thay thế popup nổi trên đầu Runner (đã tắt qua _popupsEnabled)
            // làm điểm hiển thị chính cho sự kiện chung của game. Tái sử dụng GiftToastQueue/GiftToastController.
            if (topBannerQueue == null)
            {
                Debug.LogWarning("[HUDManager] Chưa gán topBannerQueue trong Inspector - bỏ qua Top Banner cho ShowStatusPopup.");
                return;
            }

            Color accent = isBlueTeam ? _buffAccentColor : _debuffAccentColor;
            topBannerQueue.Show(string.Empty, message, icon, iconColor, accent);
        }

        private void Awake()
        {
            // Vô hiệu hóa và ẩn hoàn toàn các container thông báo comment của 2 phe
            if (fanFeedQueue != null) fanFeedQueue.gameObject.SetActive(false);
            if (antiFeedQueue != null) antiFeedQueue.gameObject.SetActive(false);
        }

        // Da loai bo thong bao Gift Toast theo yeu cau cua nguoi dung
        public void ShowGiftToast(string viewerName, string itemName, Sprite giftIcon, Color? iconColor = null)
        {
            // Disabled: Khong hien thi Gift Toast
        }

        /// <summary>
        /// Đã loại bỏ hoàn toàn thông báo comment / hành động của Phe Fan theo yêu cầu.
        /// </summary>
        public void ShowFanAction(string sender, string action, Sprite icon = null)
        {
            // Disabled: Loại bỏ thông báo comment của Phe Fan
        }

        /// <summary>
        /// Đã loại bỏ hoàn toàn thông báo comment / hành động của Phe Anti theo yêu cầu.
        /// </summary>
        public void ShowAntiAction(string sender, string action, Sprite icon = null)
        {
            // Disabled: Loại bỏ thông báo comment của Phe Anti
        }
    }
}

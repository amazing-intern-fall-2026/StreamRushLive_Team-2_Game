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

        // Hiện thông báo buff/debuff nổi lên trên đầu runner rồi tự mờ dần.
        // isBuff quyết định màu viền/icon của Top Banner (xanh = buff, đỏ = debuff) - chữ tiêu đề luôn trắng
        // theo đúng mẫu "Cảnh báo xe cản địa" (GDD). icon: null = ẩn ô icon (vd. debuff chưa có icon riêng).
        public void ShowStatusPopup(string message, bool isBuff, Sprite icon = null, Color? iconColor = null)
        {
            if (statusPopupSpawner != null)
            {
                statusPopupSpawner.Spawn(message, isBuff, icon, iconColor);
            }

            // Top Banner (giữa trên cùng) - thay thế popup nổi trên đầu Runner (đã tắt qua _popupsEnabled)
            // làm điểm hiển thị chính cho sự kiện chung của game. Tái sử dụng GiftToastQueue/GiftToastController.
            if (topBannerQueue == null)
            {
                Debug.LogWarning("[HUDManager] Chưa gán topBannerQueue trong Inspector - bỏ qua Top Banner cho ShowStatusPopup.");
                return;
            }

            topBannerQueue.Show(string.Empty, message, icon, iconColor, isBuff ? _buffAccentColor : _debuffAccentColor);
        }

        // Hien toast ngay tren thanh Anti khi viewer tang qua: chi Icon + ten viewer (GDD moi -
        // bo hien thi ten vat pham, xem itemName trong tham so chi con giu de tuong thich chu ky goi).
        // iconColor: tint cho icon quà (icon nguồn là hình trắng/nền trong suốt) - null = giữ màu mặc định.
        public void ShowGiftToast(string viewerName, string itemName, Sprite giftIcon, Color? iconColor = null)
        {
            if (giftToastQueue == null)
            {
                Debug.LogWarning("[HUDManager] Chưa gán GiftToastQueue trong Inspector - bỏ qua ShowGiftToast.");
                return;
            }

            giftToastQueue.Show(viewerName, itemName, giftIcon, iconColor, showItemName: false);
        }
    }
}

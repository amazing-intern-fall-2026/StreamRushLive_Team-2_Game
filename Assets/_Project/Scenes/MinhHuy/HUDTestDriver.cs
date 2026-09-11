using SteamRush.Features.UI;
using UnityEngine;

namespace SteamRush.MinhHuy
{
    public class HUDTestDriver : MonoBehaviour
    {
        [SerializeField] private HUDManager hud;
        [SerializeField] private Sprite dummyAvatar;
        [SerializeField] private Transform dummyRunner;
        [SerializeField] private float statusPopupInterval = 3f;
        [SerializeField] private float giftToastInterval = 4f;

        // Nhãn chung chung, không ghi số cụ thể (số liệu game có thể đổi mà không cần sửa UI).
        private static readonly string[] BuffMessages = { "Năng lượng +", "Quãng đường +", "Khiên +" };
        private static readonly string[] DebuffMessages = { "Năng lượng -", "Vấp ngã!" };

        // Icon cho buff, tái dùng đúng màu đã dùng ở Progress/Energy bar và Gift Toast cho nhất quán.
        [SerializeField] private Sprite[] buffIcons = new Sprite[3];
        [SerializeField]
        private Color[] buffIconColors =
        {
            new Color(0.996f, 0.553f, 0.102f, 1f), // Năng lượng + : cam, giống icon Energy bar
            new Color(0.149f, 0.451f, 0.949f, 1f), // Quãng đường + : xanh dương, giống icon Progress bar
            new Color(0.35f, 0.70f, 1.00f, 1f),    // Khiên + : xanh dương thép, giống icon Gift Toast Khiên
        };

        // Icon cho debuff theo đúng thứ tự DebuffMessages.
        [SerializeField] private Sprite[] debuffIcons = new Sprite[2];
        [SerializeField]
        private Color[] debuffIconColors =
        {
            new Color(0.95f, 0.35f, 0.3f, 1f),  // Năng lượng - : đỏ, dùng lại icon battery-pack (chưa có icon riêng)
            new Color(1.00f, 0.85f, 0.30f, 1f), // Vấp ngã! : vàng, icon knockout (sao choáng)
        };

        // Viewer + quà tặng -> vật thể trong game tương ứng (theo GDD-ver1). Icon quà thật sẽ bổ sung sau.
        private static readonly string[] ViewerNames = { "MeoU_88", "Khoa Ngu Gat", "Lan.tv", "AnhTrangTV" };
        private static readonly string[] GiftItemNames = { "Rào thấp", "Xà cao", "Đá lăn", "Năng lượng +20%", "Khiên chắn", "+25m tức thì" };

        // Icon + màu tint theo đúng thứ tự GiftItemNames (icon nguồn trắng/nền trong suốt từ game-icons.net, cần tint để có màu phù hợp).
        [SerializeField] private Sprite[] giftIcons = new Sprite[6];
        [SerializeField]
        private Color[] giftIconColors =
        {
            new Color(0.93f, 0.31f, 0.42f, 1f), // Rào thấp - hoa hồng: đỏ hồng
            new Color(1.00f, 0.62f, 0.75f, 1f), // Xà cao - donut: hồng kem
            new Color(0.85f, 0.65f, 0.30f, 1f), // Đá lăn - sư tử/cá voi: vàng nâu
            new Color(1.00f, 0.30f, 0.40f, 1f), // Năng lượng +20% - trái tim: đỏ
            new Color(0.35f, 0.70f, 1.00f, 1f), // Khiên chắn - khiên: xanh dương thép
            new Color(1.00f, 0.80f, 0.20f, 1f), // +25m tức thì - ngôi sao: vàng gold
        };

        private float fakeKm;
        private float statusPopupTimer;
        private float giftToastTimer;

        private void Start()
        {
            // Dùng field SerializeField hud kéo từ Inspector, KHÔNG dùng GetComponent vì script và HUDManager nằm trên 2 GameObject khác nhau.
            if (hud == null)
            {
                Debug.LogError("[MinhHuy] HUDTestDriver: chưa kéo HUD_Canvas vào slot Hud!");
            }

            hud?.UpdateRunnerInfo("MinhHuy", dummyAvatar);
            hud?.UpdateRunnerTarget(dummyRunner);
            Debug.Log("[MinhHuy] dữ liệu giả chờ module Relay/Like");
        }

        private void Update()
        {
            // Tăng tiến độ giả để kiểm tra HUD khi các module gameplay chưa sẵn sàng.
            fakeKm += Time.deltaTime;
            hud?.UpdateProgress(fakeKm);
            hud?.UpdateEnergy(Mathf.PingPong(Time.time, 1f));

            // Giả lập buff/debuff ngẫu nhiên mỗi statusPopupInterval giây để test popup khi module GiftSystem chưa sẵn sàng.
            statusPopupTimer += Time.deltaTime;
            if (statusPopupTimer >= statusPopupInterval)
            {
                statusPopupTimer = 0f;
                bool isBuff = Random.value > 0.5f;
                string[] pool = isBuff ? BuffMessages : DebuffMessages;
                int index = Random.Range(0, pool.Length);
                Sprite[] iconPool = isBuff ? buffIcons : debuffIcons;
                Color[] colorPool = isBuff ? buffIconColors : debuffIconColors;
                Sprite icon = index < iconPool.Length ? iconPool[index] : null;
                Color? iconColor = index < colorPool.Length ? colorPool[index] : (Color?)null;
                hud?.ShowStatusPopup(pool[index], isBuff, icon, iconColor);
            }

            // Giả lập viewer tặng quà ngẫu nhiên mỗi giftToastInterval giây để test toast khi module StreamIntegration chưa sẵn sàng.
            giftToastTimer += Time.deltaTime;
            if (giftToastTimer >= giftToastInterval)
            {
                giftToastTimer = 0f;
                string viewerName = ViewerNames[Random.Range(0, ViewerNames.Length)];
                int giftIndex = Random.Range(0, GiftItemNames.Length);
                string itemName = GiftItemNames[giftIndex];
                Sprite icon = giftIndex < giftIcons.Length ? giftIcons[giftIndex] : null;
                Color? iconColor = giftIndex < giftIconColors.Length ? giftIconColors[giftIndex] : (Color?)null;
                hud?.ShowGiftToast(viewerName, itemName, icon, iconColor);
            }
        }
    }
}

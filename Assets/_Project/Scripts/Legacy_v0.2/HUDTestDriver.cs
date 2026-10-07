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

        // Generic labels without hardcoded stats so values can change without breaking UI.
        private static readonly string[] BuffMessages = { "Energy +", "Distance +", "Shield +" };
        private static readonly string[] DebuffMessages = { "Energy -", "Stumble!" };

        // Buff icons matching colors used in Progress/Energy bars and Gift Toasts for consistency.
        [SerializeField] private Sprite[] buffIcons = new Sprite[3];
        [SerializeField]
        private Color[] buffIconColors =
        {
            new Color(0.996f, 0.553f, 0.102f, 1f), // Energy +
            new Color(0.149f, 0.451f, 0.949f, 1f), // Distance +
            new Color(0.35f, 0.70f, 1.00f, 1f),    // Shield +
        };

        // Debuff icons in the exact order of DebuffMessages.
        [SerializeField] private Sprite[] debuffIcons = new Sprite[2];
        [SerializeField]
        private Color[] debuffIconColors =
        {
            new Color(0.95f, 0.35f, 0.3f, 1f),  // Energy -
            new Color(1.00f, 0.85f, 0.30f, 1f), // Stumble!
        };

        private static readonly string[] ViewerNames = { "Alex_88", "Viewer_Pro", "Runner_Fan", "StarGamer" };
        private static readonly string[] GiftItemNames = { "Low Hurdle", "High Bar", "Rolling Rock", "Energy +20%", "Shield", "+25m Boost" };

        // Icon + tint color matching GiftItemNames.
        [SerializeField] private Sprite[] giftIcons = new Sprite[6];
        [SerializeField]
        private Color[] giftIconColors =
        {
            new Color(0.93f, 0.31f, 0.42f, 1f), // Low Hurdle - Rose: Red/Pink
            new Color(1.00f, 0.62f, 0.75f, 1f), // High Bar - Donut: Pink Cream
            new Color(0.85f, 0.65f, 0.30f, 1f), // Rolling Rock - Lion: Golden Brown
            new Color(1.00f, 0.30f, 0.40f, 1f), // Energy +20% - Heart: Red
            new Color(0.35f, 0.70f, 1.00f, 1f), // Shield - Steel Blue
            new Color(1.00f, 0.80f, 0.20f, 1f), // Instant Boost - Star: Gold
        };

        private float fakeKm;
        private float statusPopupTimer;
        private float giftToastTimer;

        private void Start()
        {
            if (hud == null)
            {
                Debug.LogWarning("[HUDTestDriver] HUD_Canvas not assigned in HUD slot!");
            }

            hud?.UpdateRunnerInfo("DemoRunner", dummyAvatar);
            hud?.UpdateRunnerTarget(dummyRunner);
        }

        [SerializeField] private bool testFakeProgressAndEnergy = true;
        [SerializeField] private bool testRandomStatusPopups = false;

        private void Update()
        {
            // Simulate progress to verify HUD when gameplay modules are offline.
            if (testFakeProgressAndEnergy)
            {
                fakeKm += Time.deltaTime;
                hud?.UpdateLegProgress(fakeKm % 100f, 100f);
                hud?.UpdateEnergy(Mathf.PingPong(Time.time, 1f));
            }

            // Simulate random buff/debuff popups when enabled.
            if (testRandomStatusPopups)
            {
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
            }

            // Simulate random gift toast every interval to test toasts without live stream connection.
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

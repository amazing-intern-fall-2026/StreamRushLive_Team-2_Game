using TMPro;
using UnityEngine;

namespace SteamRush.Features.UI
{
    // Theme dung chung cho toan bo HUD (bo Hyper Casual UI Pack - Smashy Tech): moi sprite/mau cua
    // HUD lay tu day thay vi hardcode trong tung script. Asset nam o Resources/HudTheme.asset de cac
    // view tu dung UI luc runtime (timer circle, gift card...) cung lay duoc ma khong can serialize ref.
    [CreateAssetMenu(fileName = "HudTheme", menuName = "SteamRush/UI/Hud Theme")]
    public class HudTheme : ScriptableObject
    {
        private const string ResourcePath = "HudTheme";
        private static HudTheme _current;

        public static HudTheme Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Resources.Load<HudTheme>(ResourcePath);
                    // Khong co asset (vd scene test thieu Resources) -> dung theme rong, view tu fallback ve mau mac dinh.
                    if (_current == null) _current = CreateInstance<HudTheme>();
                }
                return _current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _current = null;

        [Header("Pills (9-slice, cao 50px)")]
        public Sprite pillDark;
        public Sprite pillBlue;
        public Sprite pillRed;
        public Sprite pillGold;
        public Sprite pillTeal;
        public Sprite pillGrey;     // pill xam toi trung tinh (track progress bar, stopwatch)
        public Sprite pillWhite;    // pill trang bac - nhuom FactionDark lam lon thanh Fan/Anti, badge so nguoi; vien o gift loai Like

        [Header("Panels (9-slice)")]
        public Sprite panelFrame;   // khung vang, than teal - popup lon (Victory)
        public Sprite panelTabbed;  // khung vang co tab vang o giua tren (How To Play) - chu tieu de dat trong tab
        public Sprite panelDark;    // teal dam phang - the nho (gift card, banner)
        public Sprite panelInset;   // khung toi co vien - vung chua (gift panel, quick help)
        public Sprite panelFlat;    // o phang mau sang (kit) - nhuom dam theo loai gift bang CategoryFill

        [Header("Circles (timer ring, avatar ring, handle)")]
        public Sprite circleBlue;
        public Sprite circleRed;
        public Sprite circleGold;
        public Sprite circleGreen;
        public Sprite circleTeal;

        [Header("Icons")]
        public Sprite iconTimer;
        public Sprite iconUser;
        public Sprite iconCommand;
        public Sprite iconFlag;
        public Sprite iconCrown;

        // MOT bang mau duy nhat cho moi phe, moi UI (thanh, badge, o How To Play, o gift, banner/feed donate, chu noi) deu lay tu day:
        //   xxx      = vien / diem nhan sang    xxxDark  = nen dam    xxxLight = chu tren nen dam
        [Header("Faction colors (Blue = Fan, Red = Anti)")]
        public Color blue = new Color32(0x40, 0xAD, 0xFF, 0xFF);
        public Color blueDark = new Color32(0x24, 0x45, 0x8C, 0xFF);
        public Color blueLight = new Color32(0x8C, 0xCB, 0xFF, 0xFF);
        public Color red = new Color32(0xFF, 0x4D, 0x57, 0xFF);
        public Color redDark = new Color32(0x85, 0x24, 0x2E, 0xFF);
        public Color redLight = new Color32(0xFF, 0x9A, 0x9F, 0xFF);

        [Header("Neutral palette")]
        public Color gold = new Color32(0xFC, 0xDA, 0x21, 0xFF);
        public Color green = new Color32(0x76, 0xD1, 0x2C, 0xFF);
        public Color tealDark = new Color32(0x20, 0x61, 0x72, 0xFF);
        public Color textPrimary = Color.white;
        public Color textSecondary = new Color32(0xBF, 0xE3, 0xE8, 0xFF);

        [Header("Fonts")]
        public TMP_FontAsset fontBold;
        public TMP_FontAsset fontHeavy;

        [Header("Timer circle (disc nen + disc trong, tint tu sprite teal)")]
        public Color timerBgTint = new Color(0.64f, 0.47f, 0.64f, 1f);
        public Color timerInnerTint = new Color(0.50f, 0.36f, 0.50f, 1f);

        public Color FactionColor(bool isBlue) => isBlue ? blue : red;
        public Color FactionLight(bool isBlue) => isBlue ? blueLight : redLight;
        public Color FactionDark(bool isBlue) => isBlue ? blueDark : redDark;
        public Color FactionBorder(bool isBlue) => isBlue ? blue : red;
        public Sprite FactionPill(bool isBlue) => isBlue ? pillBlue : pillRed;
        public Sprite FactionCircle(bool isBlue) => isBlue ? circleBlue : circleRed;

        // 4 loai the gift: vien = pill mau loai, lon = pillGrey nhuom mau loai, chu = mau sang cung loai.
        public Sprite CategoryFrame(HudCategory c)
        {
            switch (c)
            {
                case HudCategory.Blue: return pillBlue;
                case HudCategory.Red: return pillRed;
                case HudCategory.Special: return pillGold;
                default: return pillWhite != null ? pillWhite : pillGrey;
            }
        }

        // Mau nen o theo loai (nhuom len panelFlat): Blue/Red lay tu bang mau phe; Like = teal dam; Special = nau vang.
        public Color CategoryFill(HudCategory c)
        {
            switch (c)
            {
                case HudCategory.Blue: return blueDark;
                case HudCategory.Red: return redDark;
                case HudCategory.Special: return new Color(0.52f, 0.37f, 0.10f, 1f);
                default: return new Color(0.14f, 0.40f, 0.41f, 1f);
            }
        }

        // Mau vien sang cua o theo loai - Blue/Red cung 1 mau voi moi vien/diem nhan khac cua phe.
        public Color CategoryBorder(HudCategory c)
        {
            switch (c)
            {
                case HudCategory.Blue: return blue;
                case HudCategory.Red: return red;
                case HudCategory.Special: return new Color(1f, 0.86f, 0.15f, 1f);
                default: return new Color(0.85f, 1f, 1f, 1f);
            }
        }

        // Mau chu theo loai tren nen dam: giong tieu de Team Blue/Red cua How To Play.
        public Color CategoryText(HudCategory c)
        {
            switch (c)
            {
                case HudCategory.Blue: return blueLight;
                case HudCategory.Red: return redLight;
                case HudCategory.Special: return gold;
                default: return textPrimary;
            }
        }

        // Moi loai timer co 1 vong sprite (mau co san trong kit) + mau nhan - cac TimerCircle chi goi ham nay
        // thay vi tu ve sprite tron / hardcode mau trong BuildUI.
        public TimerCircleSkin GetTimerSkin(TimerCircleKind kind)
        {
            Sprite ring;
            Color label;
            switch (kind)
            {
                case TimerCircleKind.FanSprint:     ring = circleBlue;  label = blueLight; break;
                case TimerCircleKind.Shield:        ring = circleTeal;  label = textSecondary; break;
                case TimerCircleKind.FreeControl:   ring = circleGreen; label = green; break;
                case TimerCircleKind.AntiUnlimited: ring = circleRed;   label = redLight; break;
                case TimerCircleKind.VehiclePickup: ring = circleGold;  label = gold; break;
                default:                            ring = circleRed;   label = redLight; break;
            }
            return new TimerCircleSkin
            {
                ring = ring,
                disc = circleTeal,
                bgTint = timerBgTint,
                innerTint = timerInnerTint,
                label = label,
                font = fontHeavy != null ? fontHeavy : fontBold
            };
        }
    }

    public enum HudCategory { Neutral, Blue, Red, Special }

    public enum TimerCircleKind {FanSprint, Shield, FreeControl, AntiUnlimited, VehiclePickup, VehicleHeavy }

    public struct TimerCircleSkin
    {
        public Sprite ring;
        public Sprite disc;
        public Color bgTint;
        public Color innerTint;
        public Color label;
        public TMP_FontAsset font;
    }
}

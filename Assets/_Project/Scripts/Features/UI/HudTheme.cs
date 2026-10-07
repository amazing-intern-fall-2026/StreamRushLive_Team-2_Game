using TMPro;
using UnityEngine;

namespace SteamRush.Features.UI
{
    // Shared HUD Theme (Hyper Casual UI Pack - Smashy Tech): All HUD sprites and colors are retrieved from here
    // instead of being hardcoded in individual scripts. Asset is located at Resources/HudTheme.asset.
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
                    // Fallback to empty instance if asset missing in test scenes
                    if (_current == null) _current = CreateInstance<HudTheme>();
                }
                return _current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _current = null;

        [Header("Pills (9-slice, 50px height)")]
        public Sprite pillDark;
        public Sprite pillBlue;
        public Sprite pillRed;
        public Sprite pillGold;
        public Sprite pillTeal;
        public Sprite pillGrey;     // Neutral dark grey pill (track progress bar, stopwatch)
        public Sprite pillWhite;    // White silver pill - tinted FactionDark for Fan/Anti badges

        [Header("Panels (9-slice)")]
        public Sprite panelFrame;   // Gold frame, teal body - large popup (Victory)
        public Sprite panelTabbed;  // Gold frame with center top tab (How To Play)
        public Sprite panelDark;    // Flat dark teal - cards, banners
        public Sprite panelInset;   // Inset bordered frame - container regions
        public Sprite panelFlat;    // Flat light panel - tinted via CategoryFill

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

        // Shared faction color palette:
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

        [Header("Timer circle (disc background + inner disc, tinted from teal sprite)")]
        public Color timerBgTint = new Color(0.64f, 0.47f, 0.64f, 1f);
        public Color timerInnerTint = new Color(0.50f, 0.36f, 0.50f, 1f);

        public Color FactionColor(bool isBlue) => isBlue ? blue : red;
        public Color FactionLight(bool isBlue) => isBlue ? blueLight : redLight;
        public Color FactionDark(bool isBlue) => isBlue ? blueDark : redDark;
        public Color FactionBorder(bool isBlue) => isBlue ? blue : red;
        public Sprite FactionPill(bool isBlue) => isBlue ? pillBlue : pillRed;
        public Sprite FactionCircle(bool isBlue) => isBlue ? circleBlue : circleRed;

        // Gift category frame mapping
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

        // Category fill color mapping
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

        // Category border color mapping
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

        // Category text color on dark background
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

        // Timer circle visual skin configuration
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

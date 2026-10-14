using System;
using System.Collections.Generic;
using UnityEngine;
using SteamRush.Features.StreamIntegration;

namespace SteamRush.Core.Localization
{
    public enum AppLanguage
    {
        English = 0,
        Vietnamese = 1
    }

    /// <summary>
    /// Central manager for game-wide localization (English & Vietnamese).
    /// Dispatches OnLanguageChanged event when active language is toggled.
    public class LocalizationManager : MonoBehaviour
    {
        private static LocalizationManager _instance;
        public static LocalizationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<LocalizationManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("LocalizationManager");
                        _instance = go.AddComponent<LocalizationManager>();
                        if (Application.isPlaying) DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        public const string PREFS_KEY = "app_language";

        public static event Action<AppLanguage> OnLanguageChanged;

        [SerializeField] private AppLanguage _currentLanguage = AppLanguage.English;

        public AppLanguage CurrentLanguage
        {
            get => _currentLanguage;
            set => SetLanguage(value);
        }

        public string CurrentLanguageCode => _currentLanguage == AppLanguage.Vietnamese ? "vi" : "en";

        private static readonly Dictionary<string, string> _dictVi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // === MODAL HEADER & TABS ===
            { "HEADER_TITLE", "CẤU HÌNH TRÒ CHƠI" },
            { "HEADER_SUBTITLE", "Thiết Lập Stream Rush Live & Điều Khiển Trò Chơi" },
            { "TAB_BASIC", "CẤU HÌNH CƠ BẢN" },
            { "TAB_ADVANCED", "HỆ THỐNG NÂNG CAO" },

            // === BASIC PANEL BANNERS & FIELDS ===
            { "BANNER_HOST_CHANNEL", "KÊNH TIKTOK LIVE HOST" },
            { "PLACEHOLDER_USERNAME", "Nhập @username TikTok Live..." },
            { "BTN_CHECK_LIVE", "KIỂM TRA LIVE" },
            { "STATUS_NOT_VERIFIED", "CHƯA XÁC MINH" },
            { "STATUS_VERIFIED", "ĐÃ KẾT NỐI" },
            { "STATUS_CHECKING", "ĐANG KIỂM TRA..." },
            { "BANNER_TARGET_DISTANCE", "MỤC TIÊU CỰ LY & TIẾP SỨC" },
            { "PLACEHOLDER_DISTANCE", "Nhập cự ly mục tiêu (km)... ví dụ 1 hoặc 2.5" },
            { "LABEL_INFINITE_RUN", "Chạy Vô Tận (Không giới hạn):" },
            { "BANNER_STREAM_GIFTS", "QUÀ TẶNG LIVE & TƯƠNG TÁC" },

            // === TABLE HEADERS ===
            { "TH_ACTIVE", "BẬT" },
            { "TH_FEATURE", "HÀNH ĐỘNG TÍNH NĂNG" },
            { "TH_GIFT", "QUÀ TIKTOK" },
            { "TH_STAT", "CHỈ SỐ" },
            { "TH_DESC", "MÔ TẢ THẺ" },
            { "TH_ORDER", "THỨ TỰ" },

            // === BOTTOM BAR BUTTONS ===
            { "BTN_RESET_DEFAULTS", "KHÔI PHỤC MẶC ĐỊNH" },
            { "BTN_SAVE_CONFIG", "LƯU CÀI ĐẶT" },
            { "FEEDBACK_SAVED", "ĐÃ LƯU!" },
            { "BTN_TEST_MODE", "CHẠY THỬ (OFFLINE)" },
            { "BTN_START_STREAM", "BẮT ĐẦU LIVE STREAM" },

            // === ADVANCED SETTINGS BANNERS ===
            { "BANNER_FAN_ENERGY", "ĐỘI XANH & THỂ LỰC RUNNER" },
            { "BANNER_ANTI_HAZARDS", "ĐỘI ĐỎ & VẬT CẢN ĐƯỜNG" },
            { "BANNER_PENALTIES", "HÌNH PHẠT VA CHẠM" },
            { "BANNER_SIMULATION", "MÔ PHỎNG LIVE STREAM OFFLINE" },
            { "BANNER_HUD_DISPLAY", "HIỂN THỊ HUD & BẢNG THÔNG TIN" },
            { "BANNER_BACKEND", "TIKTOK LIVE BACKEND & KẾT NỐI" },

            // === ADVANCED SETTINGS LABELS ===
            { "LBL_LANE_COST", "Thể lực đổi làn (Mặc định: 10):" },
            { "LBL_JUMP_COST", "Thể lực nhảy (Mặc định: 20):" },
            { "LBL_FREE_CONTROL_DUR", "Thời lượng Bùa Tự Do (giây, mặc định: 30s):" },
            { "LBL_SPRINT_DUR", "Thời lượng Bứt Tốc Turbo (giây, mặc định: 30s):" },
            { "LBL_KEYBOARD_CTRL", "Tự điều khiển bằng phím (A/D/Phím Cách):" },
            { "LBL_AUTO_SPAWN", "Tự động thả xe khi đầy 100% năng lượng Đỏ:" },
            { "LBL_MAX_CARS", "Số xe cản đường tối đa cùng lúc (1 - 3):" },
            { "LBL_DISTANCE_PENALTY", "Cự ly bị trừ khi va chạm (mét, mặc định: 100m):" },
            { "LBL_SEDAN_PENALTY", "Thể lực mất khi đâm Xe Ô Tô (%):" },
            { "LBL_PICKUP_PENALTY", "Thể lực mất khi va Thú Săn (%):" },
            { "LBL_HEAVY_PENALTY", "Thể lực mất khi đâm Tàu Hỏa (%):" },
            { "LBL_SIM_MASTER", "Tự động mô phỏng trong chế độ chạy thử:" },
            { "LBL_SIM_CHATS", "Mô phỏng comment chat chuyển làn:" },
            { "LBL_SIM_GIFTS", "Mô phỏng khán giả tặng quà:" },
            { "LBL_SIM_LIKES", "Mô phỏng thả tim liên tục (Nạp thể lực):" },
            { "LBL_SIM_FOLLOWS", "Mô phỏng Follow trao gậy tiếp sức:" },
            { "LBL_SIM_DELAY", "Mô phỏng độ trễ phát sóng live (1.5s - 3.0s):" },
            { "LBL_HUD_HOWTOPLAY", "Hiển thị bảng hướng dẫn Câu Lệnh Chơi:" },
            { "LBL_HUD_GIFTINFO", "Hiển thị bảng danh mục Quà Tặng:" },
            { "LBL_HUD_TIMER", "Hiển thị đồng hồ bấm giờ trận đấu:" },
            { "LBL_HUD_CIRCLES", "Hiển thị vòng tròn đếm ngược bùa & buff:" },
            { "LBL_HUD_DEBUGUI", "Hiển thị bảng Debug UI trong game (Phím '~'):" },
            { "LBL_BACKEND_HTTP", "Cổng HTTP Backend (Mặc định: 9091):" },
            { "LBL_BACKEND_SOCKET", "Cổng Socket.IO Client (Mặc định: 3001):" },
            { "LBL_EULER_KEY", "Khóa Euler API (EULER_API_KEY):" },
            { "LBL_BACKEND_DIR", "Thư mục mã nguồn Backend:" },
            { "BTN_GET_KEY", "LẤY KEY" },
            { "BTN_CHECK_PORTS", "KIỂM TRA CỔNG" },
            { "BTN_START_BACKEND", "BẬT BACKEND" },
            { "BTN_SETUP_BACKEND", "TỰ ĐỘNG CÀI ĐẶT (GIT)" },
            { "BTN_STOP_BACKEND", "DỪNG BACKEND" },
            { "BTN_RESET", "ĐẶT LẠI" },

            // === HOW TO PLAY HUD ===
            { "HTP_TITLE", "Câu Lệnh Chơi" },
            { "HTP_FAN_TITLE", "Đội Xanh (điều khiển)" },
            { "HTP_FAN_JOIN", "\"blue\" → Vào phe Xanh" },
            { "HTP_FAN_MOVE", "\"1/2/3\" → Đổi làn" },
            { "HTP_FAN_JUMP", "\"jump/j\" → Nhảy" },
            { "HTP_ANTI_TITLE", "Đội Đỏ (thả xe)" },
            { "HTP_ANTI_JOIN", "\"red\" → Vào phe Đỏ" },
            { "HTP_ANTI_SPAWN", "\"1/2/3\" → Thả vật cản" },

            // === VICTORY POPUP ===
            { "VIC_TITLE", "<b>CHIẾN THẮNG!</b>" },
            { "VIC_SUBTITLE", "HOÀN THÀNH CHẶNG ĐUA CÙNG KHÁN GIẢ LIVE!" },
            { "VIC_TOTAL_DIST", "TỔNG CỰ LY" },
            { "VIC_CLEAR_TIME", "THỜI GIAN" },
            { "VIC_FOOTER", "STREAM RUSH LIVE - HOÀN THÀNH XUẤT SẮC" },
            { "VIC_PLAY_AGAIN", "CHƠI LẠI" },

            // === SETUP & STREAM GUIDE MODAL ===
            { "GUIDE_TITLE", "HƯỚNG DẪN SETUP & LIVE TIKTOK" },
            { "GUIDE_SUBTITLE", "3 Bước kết nối phòng Live và bắt đầu game tương tác" },
            { "GUIDE_STEP1_TAG", "BƯỚC 1" },
            { "GUIDE_STEP1_TITLE", "Khởi Động Máy Chủ Backend" },
            { "GUIDE_STEP1_DESC", "Vào tab <b>HỆ THỐNG NÂNG CAO</b> -> Bấm <b>CÀI ĐẶT (INSTALL)</b> để chuẩn bị môi trường (lần đầu) -> Bấm <b>KHỞI ĐỘNG (START)</b>. Cổng HTTP 9091 & Socket 3001 báo xanh là hoàn tất." },
            { "GUIDE_STEP2_TAG", "BƯỚC 2" },
            { "GUIDE_STEP2_TITLE", "Kiểm Tra & Kết Nối Kênh Live" },
            { "GUIDE_STEP2_DESC", "Tại tab <b>CẤU HÌNH CƠ BẢN</b>, nhập chính xác <b>@username</b> kênh TikTok đang phát Live -> Bấm nút <b>KIỂM TRA LIVE</b> -> Chờ trạng thái chuyển sang <b>ĐÃ KẾT NỐI</b> màu xanh lá." },
            { "GUIDE_STEP3_TAG", "BƯỚC 3" },
            { "GUIDE_STEP3_TITLE", "Bắt Đầu Live Stream & Tương Tác" },
            { "GUIDE_STEP3_DESC", "Tùy chỉnh cự ly và quà tặng nếu muốn -> Bấm <b>LƯU CÀI ĐẶT</b> -> Bấm <b>BẮT ĐẦU LIVE STREAM</b>. Khán giả gõ <b>1, 2, 3</b> để điều khiển Runner và tặng quà kích hoạt kỹ năng thời gian thực!" },
            { "GUIDE_BTN_GOTIT", "ĐÃ HIỂU" },

            // === FEATURE NAMES ===
            { "FEAT_SHIELD", "Khiên Bảo Vệ" },
            { "FEAT_SPEEDBOOST", "Bứt Tốc Turbo" },
            { "FEAT_FREECONTROL", "Bùa Tự Do" },
            { "FEAT_BLUEENERGY", "Bình Năng Lượng Xanh" },
            { "FEAT_PICKUP", "Thả Thú Săn" },
            { "FEAT_HEAVYTRUCK", "Thả Tàu Hỏa" },
            { "FEAT_UNLIMITEDCARS", "Bão Xe Không Giới Hạn" },
            { "FEAT_REDENERGY", "Bình Năng Lượng Đỏ" },
            { "FEAT_DANCE", "Vũ Đạo Ăn Mừng" },
            { "FEAT_RAINHAZARD", "Bão Mưa Trơn Trượt" },
            { "FEAT_NEXTRUNNER", "Chạy Tiếp Sức" },
            { "FEAT_TEAMENERGY", "Năng Lượng Đội (Thả Tim)" },

            // === DEFAULT GIFT DESCRIPTIONS ===
            { "DESC_SHIELD", "Khiên bảo vệ" },
            { "DESC_SPEEDBOOST", "Tăng tốc cực nhanh" },
            { "DESC_FREECONTROL", "Miễn phí đổi làn" },
            { "DESC_BLUEENERGY", "Bơm năng lượng Xanh" },
            { "DESC_PICKUP", "Bầy thú săn" },
            { "DESC_HEAVYTRUCK", "Đoàn tàu hỏa" },
            { "DESC_UNLIMITEDCARS", "Bão xe 60s" },
            { "DESC_REDENERGY", "Bơm năng lượng Đỏ" },
            { "DESC_DANCE", "Vũ đạo ăn mừng" },
            { "DESC_RAINHAZARD", "Mưa trơn trượt" },
            { "DESC_NEXTRUNNER", "Lên làm runner" },
            { "DESC_TEAMENERGY", "x20: 20 Năng Lượng Đội" }
        };

        private static readonly Dictionary<string, string> _dictEn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // === MODAL HEADER & TABS ===
            { "HEADER_TITLE", "GAME CONFIGURATION" },
            { "HEADER_SUBTITLE", "Stream Rush Live Setup & Gameplay Controls" },
            { "TAB_BASIC", "BASIC SETUP" },
            { "TAB_ADVANCED", "ADVANCED SYSTEM" },

            // === BASIC PANEL BANNERS & FIELDS ===
            { "BANNER_HOST_CHANNEL", "TIKTOK LIVE HOST CHANNEL" },
            { "PLACEHOLDER_USERNAME", "Enter TikTok live @username..." },
            { "BTN_CHECK_LIVE", "CHECK LIVE" },
            { "STATUS_NOT_VERIFIED", "NOT VERIFIED" },
            { "STATUS_VERIFIED", "VERIFIED" },
            { "STATUS_CHECKING", "CHECKING..." },
            { "BANNER_TARGET_DISTANCE", "TARGET DISTANCE & BATON RELAY" },
            { "PLACEHOLDER_DISTANCE", "Enter target distance (km)... e.g., 1 or 2.5" },
            { "LABEL_INFINITE_RUN", "Infinite Run:" },
            { "BANNER_STREAM_GIFTS", "STREAM GIFTS & INTERACTION TRIGGERS" },

            // === TABLE HEADERS ===
            { "TH_ACTIVE", "ACTIVE" },
            { "TH_FEATURE", "FEATURE ACTION" },
            { "TH_GIFT", "TIKTOK GIFT" },
            { "TH_STAT", "STAT" },
            { "TH_DESC", "CARD DESCRIPTION" },
            { "TH_ORDER", "ORDER" },

            // === BOTTOM BAR BUTTONS ===
            { "BTN_RESET_DEFAULTS", "RESET DEFAULTS" },
            { "BTN_SAVE_CONFIG", "SAVE SETTINGS" },
            { "FEEDBACK_SAVED", "SAVED!" },
            { "BTN_TEST_MODE", "TEST RUN (OFFLINE)" },
            { "BTN_START_STREAM", "START LIVE STREAM" },

            // === ADVANCED SETTINGS BANNERS ===
            { "BANNER_FAN_ENERGY", "FAN FACTION & RUNNER ENERGY" },
            { "BANNER_ANTI_HAZARDS", "ANTI FACTION & TRAFFIC HAZARDS" },
            { "BANNER_PENALTIES", "COLLISION PENALTIES" },
            { "BANNER_SIMULATION", "OFFLINE LIVE STREAM SIMULATION" },
            { "BANNER_HUD_DISPLAY", "HUD & OVERLAY DISPLAY" },
            { "BANNER_BACKEND", "TIKTOK LIVE BACKEND & CONNECTION" },

            // === ADVANCED SETTINGS LABELS ===
            { "LBL_LANE_COST", "Lane change energy cost (default: 10):" },
            { "LBL_JUMP_COST", "Jump energy cost (default: 20):" },
            { "LBL_FREE_CONTROL_DUR", "Freedom Charm duration (sec, default: 30s):" },
            { "LBL_SPRINT_DUR", "Sprint Boost duration (sec, default: 30s):" },
            { "LBL_KEYBOARD_CTRL", "Enable manual keyboard controls (A/D/Space):" },
            { "LBL_AUTO_SPAWN", "Auto-spawn car at 100% energy:" },
            { "LBL_MAX_CARS", "Max concurrent cars on 3 lanes (1 - 3):" },
            { "LBL_DISTANCE_PENALTY", "Distance penalty on crash (meters, default: 100m):" },
            { "LBL_SEDAN_PENALTY", "Energy lost on Sedan hit (%):" },
            { "LBL_PICKUP_PENALTY", "Energy lost on Hunting Beast hit (%):" },
            { "LBL_HEAVY_PENALTY", "Energy lost on Train hit (%):" },
            { "LBL_SIM_MASTER", "Auto live simulation in test mode:" },
            { "LBL_SIM_CHATS", "Simulate chat comments:" },
            { "LBL_SIM_GIFTS", "Simulate viewer gifts:" },
            { "LBL_SIM_LIKES", "Simulate continuous hearts (Likes):" },
            { "LBL_SIM_FOLLOWS", "Simulate baton handover (Follow):" },
            { "LBL_SIM_DELAY", "Simulate stream broadcast delay (1.5s - 3.0s):" },
            { "LBL_HUD_HOWTOPLAY", "Show How-To-Play Guide banner:" },
            { "LBL_HUD_GIFTINFO", "Show Gift Info Panel banner:" },
            { "LBL_HUD_TIMER", "Show Match Timer (Stopwatch):" },
            { "LBL_HUD_CIRCLES", "Show Skill & Buff Timer Circles:" },
            { "LBL_HUD_DEBUGUI", "Show Debug UI in game (Toggle '~', default: OFF):" },
            { "LBL_BACKEND_HTTP", "Backend HTTP Port (Default: 9091):" },
            { "LBL_BACKEND_SOCKET", "Socket.IO Port for Game Client (Default: 3001):" },
            { "LBL_EULER_KEY", "Euler API Key (EULER_API_KEY):" },
            { "LBL_BACKEND_DIR", "Backend Folder Path:" },
            { "BTN_GET_KEY", "GET KEY" },
            { "BTN_CHECK_PORTS", "CHECK PORTS" },
            { "BTN_START_BACKEND", "START BACKEND" },
            { "BTN_SETUP_BACKEND", "AUTO SETUP (GIT)" },
            { "BTN_STOP_BACKEND", "STOP BACKEND" },
            { "BTN_RESET", "RESET" },

            // === HOW TO PLAY HUD ===
            { "HTP_TITLE", "Command To Play" },
            { "HTP_FAN_TITLE", "Team Blue (control)" },
            { "HTP_FAN_JOIN", "\"blue\" → Join Blue" },
            { "HTP_FAN_MOVE", "\"1/2/3\" → Move" },
            { "HTP_FAN_JUMP", "\"jump/j\" → Jump" },
            { "HTP_ANTI_TITLE", "Team Red (spawn car)" },
            { "HTP_ANTI_JOIN", "\"red\" → Join Red" },
            { "HTP_ANTI_SPAWN", "\"1/2/3\" → Spawn Obstacles" },

            // === VICTORY POPUP ===
            { "VIC_TITLE", "<b>VICTORY!</b>" },
            { "VIC_SUBTITLE", "STAGE CLEARED WITH LIVE AUDIENCE!" },
            { "VIC_TOTAL_DIST", "TOTAL DISTANCE" },
            { "VIC_CLEAR_TIME", "CLEAR TIME" },
            { "VIC_FOOTER", "STREAM RUSH LIVE - RUN COMPLETED" },
            { "VIC_PLAY_AGAIN", "PLAY AGAIN" },

            // === SETUP & STREAM GUIDE MODAL ===
            { "GUIDE_TITLE", "TIKTOK LIVE SETUP & STREAM GUIDE" },
            { "GUIDE_SUBTITLE", "3 Steps to connect your Live room & start interactive gameplay" },
            { "GUIDE_STEP1_TAG", "STEP 1" },
            { "GUIDE_STEP1_TITLE", "Start Backend Server" },
            { "GUIDE_STEP1_DESC", "Open <b>ADVANCED SYSTEM</b> tab -> Click <b>AUTO SETUP (GIT)</b> to prepare environment (first time) -> Click <b>START BACKEND</b>. HTTP 9091 & Socket 3001 green indicate ready." },
            { "GUIDE_STEP2_TAG", "STEP 2" },
            { "GUIDE_STEP2_TITLE", "Check & Verify Live Channel" },
            { "GUIDE_STEP2_DESC", "In <b>BASIC SETUP</b> tab, enter the exact <b>@username</b> broadcasting live -> Click <b>CHECK LIVE</b> -> Wait for green <b>CONNECTED</b> status confirmation." },
            { "GUIDE_STEP3_TAG", "STEP 3" },
            { "GUIDE_STEP3_TITLE", "Start Live Stream & Interactivity" },
            { "GUIDE_STEP3_DESC", "Configure distance & gifts if desired -> Click <b>SAVE SETTINGS</b> -> Click <b>START LIVE STREAM</b>. Viewers comment <b>1, 2, 3</b> to control Runner and send gifts to trigger live skills!" },
            { "GUIDE_BTN_GOTIT", "GOT IT" },

            // === FEATURE NAMES ===
            { "FEAT_SHIELD", "Protection Shield" },
            { "FEAT_SPEEDBOOST", "Speed Boost (Turbo)" },
            { "FEAT_FREECONTROL", "Freedom Charm" },
            { "FEAT_BLUEENERGY", "Blue Energy" },
            { "FEAT_PICKUP", "Spawn Hunting Beasts" },
            { "FEAT_HEAVYTRUCK", "Spawn Train" },
            { "FEAT_UNLIMITEDCARS", "Car Storm (Unlimited)" },
            { "FEAT_REDENERGY", "Red Energy" },
            { "FEAT_DANCE", "Dance" },
            { "FEAT_RAINHAZARD", "Rain Storm Hazard" },
            { "FEAT_NEXTRUNNER", "Next Runner" },
            { "FEAT_TEAMENERGY", "Team Energy (Likes)" },

            // === DEFAULT GIFT DESCRIPTIONS ===
            { "DESC_SHIELD", "Protection Shield" },
            { "DESC_SPEEDBOOST", "Turbo Speed" },
            { "DESC_FREECONTROL", "Freedom Charm" },
            { "DESC_BLUEENERGY", "Blue Energy" },
            { "DESC_PICKUP", "Hunting Beasts" },
            { "DESC_HEAVYTRUCK", "Train" },
            { "DESC_UNLIMITEDCARS", "Car Storm" },
            { "DESC_REDENERGY", "Red Energy" },
            { "DESC_DANCE", "Dance" },
            { "DESC_RAINHAZARD", "Rain Storm" },
            { "DESC_NEXTRUNNER", "Next Runner" },
            { "DESC_TEAMENERGY", "x20: 20 Team Energy" }
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);

            LoadSavedLanguage();
        }

        public void LoadSavedLanguage()
        {
            string code = PlayerPrefs.GetString(PREFS_KEY, "en");
            _currentLanguage = string.Equals(code, "vi", StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.Vietnamese
                : AppLanguage.English;
        }

        public void SetLanguage(AppLanguage language)
        {
            _currentLanguage = language;
            PlayerPrefs.SetString(PREFS_KEY, CurrentLanguageCode);
            PlayerPrefs.Save();

            OnLanguageChanged?.Invoke(_currentLanguage);
        }

        public void ToggleLanguage()
        {
            SetLanguage(_currentLanguage == AppLanguage.English ? AppLanguage.Vietnamese : AppLanguage.English);
        }

        public string Get(string key, string fallback = null)
        {
            if (string.IsNullOrEmpty(key)) return fallback ?? "";

            var dict = _currentLanguage == AppLanguage.Vietnamese ? _dictVi : _dictEn;
            if (dict.TryGetValue(key, out string val))
            {
                return val;
            }

            // Fallback to English dictionary if not found in Vietnamese
            if (_currentLanguage == AppLanguage.Vietnamese && _dictEn.TryGetValue(key, out string enVal))
            {
                return enVal;
            }

            return fallback ?? key;
        }

        public string GetFeatureName(GiftActionType action)
        {
            switch (action)
            {
                case GiftActionType.Blue_Shield: return Get("FEAT_SHIELD");
                case GiftActionType.Blue_SpeedBoost: return Get("FEAT_SPEEDBOOST");
                case GiftActionType.Blue_FreeControl: return Get("FEAT_FREECONTROL");
                case GiftActionType.Blue_EnergyBottle: return Get("FEAT_BLUEENERGY");
                case GiftActionType.Red_SpawnPickup: return Get("FEAT_PICKUP");
                case GiftActionType.Red_SpawnHeavyTruck: return Get("FEAT_HEAVYTRUCK");
                case GiftActionType.Red_UnlimitedCars: return Get("FEAT_UNLIMITEDCARS");
                case GiftActionType.Red_EnergyBottle: return Get("FEAT_REDENERGY");
                case GiftActionType.Special_GiftDance: return Get("FEAT_DANCE");
                case GiftActionType.Special_RainHazard: return Get("FEAT_RAINHAZARD");
                case GiftActionType.Follow_Runner: return Get("FEAT_NEXTRUNNER");
                case GiftActionType.Like_Energy: return Get("FEAT_TEAMENERGY");
                default: return action.ToString();
            }
        }

        public string GetDefaultDescription(GiftActionType action)
        {
            switch (action)
            {
                case GiftActionType.Blue_Shield: return Get("DESC_SHIELD");
                case GiftActionType.Blue_SpeedBoost: return Get("DESC_SPEEDBOOST");
                case GiftActionType.Blue_FreeControl: return Get("DESC_FREECONTROL");
                case GiftActionType.Blue_EnergyBottle: return Get("DESC_BLUEENERGY");
                case GiftActionType.Red_SpawnPickup: return Get("DESC_PICKUP");
                case GiftActionType.Red_SpawnHeavyTruck: return Get("DESC_HEAVYTRUCK");
                case GiftActionType.Red_UnlimitedCars: return Get("DESC_UNLIMITEDCARS");
                case GiftActionType.Red_EnergyBottle: return Get("DESC_REDENERGY");
                case GiftActionType.Special_GiftDance: return Get("DESC_DANCE");
                case GiftActionType.Special_RainHazard: return Get("DESC_RAINHAZARD");
                case GiftActionType.Follow_Runner: return Get("DESC_NEXTRUNNER");
                case GiftActionType.Like_Energy: return Get("DESC_TEAMENERGY");
                default: return "";
            }
        }
    }
}

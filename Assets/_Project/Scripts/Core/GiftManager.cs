using System;
using UnityEngine;
using StreamRushLive.Features.Spawning;
using SteamRush.Features.Runner;
using SteamRush.Features.UI;
using SteamRush.Features.StreamIntegration;
using SteamRush.Features.Environment;

namespace StreamRushLive.Features.Gifts
{
    /// <summary>
    /// Cấu hình chi tiết cho từng loại xe cản đường của phe Red (Anti).
    /// Cho phép Designer/Tester tùy chỉnh mọi thông số: năng lượng tốn, độ đẩy lùi, % trừ năng lượng, số mét phạt...
    /// </summary>
    [Serializable]
    public class AntiCarGiftConfig
    {
        [Tooltip("Tên hiển thị loại xe")]
        public string vehicleName = "Xe";

        [Tooltip("Năng lượng phe Red (Anti) tiêu hao khi thả loại xe này (mặc định: 100).")]
        public int energyCost = 100;

        [Tooltip("Khoảng cách Runner bị húc đẩy lùi về phía sau (mét) khi va chạm.")]
        public float knockbackDistance = 2.0f;

        [Tooltip("Thời gian Runner bị đẩy lùi (giây).")]
        public float knockbackDuration = 0.5f;

        [Tooltip("Phần trăm (%) năng lượng của Runner/Fan bị trừ khi va chạm với loại xe này.")]
        [Range(0f, 100f)]
        public float energyPenaltyPercent = 20f;

        [Tooltip("Số mét cự ly tiến trình đường chạy bị trừ khi va chạm (m).")]
        public float distancePenaltyMeters = 100f;

        [Tooltip("Tốc độ xe tự động di chuyển ngược chiều về phía Runner (m/s).")]
        public float drivingSpeed = 7.0f;

        [Tooltip("Thời gian khựng hình (Hit-Stop) tạo cảm giác va chạm mạnh (giây).")]
        public float hitStopDuration = 0.1f;

        [Tooltip("Tốc độ đỉnh khi thế giới bị cuộn giật lùi lại phía sau.")]
        public float reverseWorldPeakSpeed = -87.0f;

        [Tooltip("Thời gian thế giới cuộn giật lùi (giây).")]
        public float reverseWorldDuration = 1.8f;
    }

    /// <summary>
    /// Trung tâm quản lý và cấu hình toàn bộ các loại Gift tương tác (Interactive Gifts / Stream Gifts).
    /// Vì trong cơ chế livestream, quà donate của khán giả được kích hoạt trực tiếp ngay lập tức
    /// (không còn nhặt item vật lý rơi trên đường), script này tập trung toàn bộ thông số
    /// thời gian, chi phí, năng lượng của từng loại Gift trên một GameObject duy nhất trong Hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    public class GiftManager : MonoBehaviour
    {
        public static GiftManager Instance { get; private set; }

        [Header("Blue Team Gifts")]
        [Tooltip("Thời gian tồn tại của Khiên bảo vệ (giây). Mặc định 15s.")]
        [SerializeField] private float _shieldDuration = 15f;

        [Tooltip("Chi phí năng lượng Fan tiêu hao khi bật Khiên (nếu có tính năng lượng phe). Mặc định 0 (miễn phí khi viewer tặng).")]
        [SerializeField] private int _shieldEnergyCost = 0;

        [Tooltip("Thời gian duy trì Tăng Tốc (Sprint Buff) (giây). Mặc định 30s.")]
        [SerializeField] private float _sprintDuration = 30f;

        [Tooltip("Hệ số nhân tốc độ khi nhận Sprint Buff (1.5 = +50% tốc độ).")]
        [SerializeField] private float _sprintSpeedMultiplier = 1.5f;

        [Tooltip("Thời gian duy trì Thao Tác Tự Do (Free Control) - 0% cost chuyển làn & nhảy (giây). Mặc định 30s.")]
        [SerializeField] private float _freeControlDuration = 30f;

        [Tooltip("Lượng năng lượng cộng cho Blue Team khi nhận Bình Năng Lượng Xanh (Blue Energy Bottle). Mặc định +300.")]
        [SerializeField] private int _blueEnergyBottleAmount = 300;

        [Header("Red Team General")]
        [Tooltip("Lượng năng lượng cộng cho Red Team khi nhận Bình Năng Lượng Đỏ (Red Energy Bottle). Mặc định +500.")]
        [SerializeField] private int _redEnergyBottleAmount = 500;

        [Tooltip("Thời gian kích hoạt Bão Xe (Unlimited Cars) - thả xe liên tục không tốn năng lượng (giây). Mặc định 60s.")]
        [SerializeField] private float _unlimitedCarsDuration = 60f;

        [Tooltip("Duration of Vehicle Special Phases (Pickup Truck & Heavy Truck phases in seconds). Default 60s.")]
        [SerializeField] private float _vehiclePhaseDuration = 60f;

        [Header("Sedan Car")]
        [SerializeField] private AntiCarGiftConfig _sedanConfig = new AntiCarGiftConfig
        {
            vehicleName = "Sedan Car",
            energyCost = 100,
            knockbackDistance = 2.0f,
            knockbackDuration = 0.5f,
            energyPenaltyPercent = 20f,
            distancePenaltyMeters = 100f,
            drivingSpeed = 7.0f,
            hitStopDuration = 0.10f,
            reverseWorldPeakSpeed = -87.0f,
            reverseWorldDuration = 1.8f
        };

        [Header("Pickup Truck")]
        [SerializeField] private AntiCarGiftConfig _pickupConfig = new AntiCarGiftConfig
        {
            vehicleName = "Pickup Truck",
            energyCost = 100,
            knockbackDistance = 3.0f,
            knockbackDuration = 0.6f,
            energyPenaltyPercent = 40f,
            distancePenaltyMeters = 200f,
            drivingSpeed = 6.2f,
            hitStopDuration = 0.18f,
            reverseWorldPeakSpeed = -130.0f,
            reverseWorldDuration = 2.4f
        };

        [Header("Heavy Truck")]
        [SerializeField] private AntiCarGiftConfig _heavyTruckConfig = new AntiCarGiftConfig
        {
            vehicleName = "Heavy Truck",
            energyCost = 100,
            knockbackDistance = 4.2f,
            knockbackDuration = 0.7f,
            energyPenaltyPercent = 60f,
            distancePenaltyMeters = 400f,
            drivingSpeed = 5.2f,
            hitStopDuration = 0.25f,
            reverseWorldPeakSpeed = -196.0f,
            reverseWorldDuration = 3.2f
        };

        [Header("Interactive Gifts")]
        [Tooltip("Thời gian Runner nhảy múa ăn mừng khi viewer tặng Gift Dance (giây).")]
        [SerializeField] private float _giftDanceDuration = 5f;

        [Header("References")]
        [SerializeField] private ChatLaneRunnerController _runnerController;
        [SerializeField] private RunnerGiftEffects _runnerEffects;
        [SerializeField] private FactionTugOfWarManager _factionManager;
        [SerializeField] private SingleObstacleSpawner _obstacleSpawner;
        [SerializeField] private GiftDanceController _giftDanceController;
        [SerializeField] private WeatherHazardManager _weatherManager;
        [SerializeField] private HUDManager _hudManager;

        #region Public Properties (Getters)
        public float ShieldDuration => _shieldDuration;
        public int ShieldEnergyCost => _shieldEnergyCost;
        public float SprintDuration => _sprintDuration;
        public float SprintSpeedMultiplier => _sprintSpeedMultiplier;
        public float FreeControlDuration => _freeControlDuration;
        public int BlueEnergyBottleAmount => _blueEnergyBottleAmount;
        public int RedEnergyBottleAmount => _redEnergyBottleAmount;
        public float UnlimitedCarsDuration => _unlimitedCarsDuration;
        public float VehiclePhaseDuration => _vehiclePhaseDuration;

        public AntiCarGiftConfig SedanConfig => _sedanConfig;
        public AntiCarGiftConfig PickupConfig => _pickupConfig;
        public AntiCarGiftConfig HeavyTruckConfig => _heavyTruckConfig;

        public int SedanCarCost => _sedanConfig.energyCost;
        public int PickupTruckCost => _pickupConfig.energyCost;
        public int HeavyTruckCost => _heavyTruckConfig.energyCost;

        public float GiftDanceDuration => _giftDanceDuration;
        #endregion

        /// <summary>
        /// Lấy cấu hình chi tiết của từng loại xe theo VehicleTier.
        /// </summary>
        public AntiCarGiftConfig GetCarConfig(VehicleTier tier)
        {
            return tier switch
            {
                VehicleTier.SedanCar => _sedanConfig,
                VehicleTier.PickupTruck => _pickupConfig,
                VehicleTier.HeavyTruck => _heavyTruckConfig,
                _ => _sedanConfig
            };
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ResolveReferences();
        }

        private void Start()
        {
            SyncSettingsToSubsystems();
        }

        private void OnValidate()
        {
            // Tự động đồng bộ các giá trị khi designer chỉnh trên Inspector trong Editor
            if (Application.isPlaying)
            {
                SyncSettingsToSubsystems();
            }
        }

        public void ResolveReferences()
        {
            if (_runnerController == null)
                _runnerController = FindFirstObjectByType<ChatLaneRunnerController>();

            if (_runnerEffects == null)
            {
                if (_runnerController != null)
                {
                    _runnerEffects = _runnerController.GetComponent<RunnerGiftEffects>()
                                    ?? _runnerController.GetComponentInChildren<RunnerGiftEffects>();
                }
                if (_runnerEffects == null)
                    _runnerEffects = FindFirstObjectByType<RunnerGiftEffects>();
            }

            if (_factionManager == null)
                _factionManager = FindFirstObjectByType<FactionTugOfWarManager>();

            if (_obstacleSpawner == null)
                _obstacleSpawner = FindFirstObjectByType<SingleObstacleSpawner>();

            if (_giftDanceController == null)
            {
                if (_runnerController != null)
                {
                    _giftDanceController = _runnerController.GetComponent<GiftDanceController>()
                                           ?? _runnerController.GetComponentInChildren<GiftDanceController>();
                }
                if (_giftDanceController == null)
                    _giftDanceController = FindFirstObjectByType<GiftDanceController>();
            }

            if (_weatherManager == null)
                _weatherManager = FindFirstObjectByType<WeatherHazardManager>();

            if (_hudManager == null)
                _hudManager = FindFirstObjectByType<HUDManager>();
        }

        private void SyncSettingsToSubsystems()
        {
            if (_factionManager != null)
            {
                _factionManager.FanEnergyGiftAmount = _blueEnergyBottleAmount;
                _factionManager.AntiCarLaneCost = _sedanConfig.energyCost;
            }
        }

        #region Direct Gift Activation APIs

        /// <summary>
        /// Kích hoạt trực tiếp Khiên bảo hộ lên Runner.
        /// </summary>
        public bool ActivateShield(string sender = "Blue Team", float customDuration = -1f)
        {
            ResolveReferences();
            float duration = customDuration > 0f ? customDuration : _shieldDuration;

            if (_runnerEffects != null)
            {
                _runnerEffects.ActivateShield(duration);
                _hudManager?.ShowFanAction(sender, $"Shield ({duration:F0}s)");
                _hudManager?.ShowStatusPopup($"[{sender}] Shield ({duration:F0}s)", true);
                Debug.Log($"[GiftManager] {sender} -> Kích hoạt Khiên bảo vệ trực tiếp ({duration:F0}s).");
                return true;
            }

            Debug.LogWarning("[GiftManager] Không tìm thấy RunnerGiftEffects để kích hoạt Khiên!");
            return false;
        }

        /// <summary>
        /// Kích hoạt trực tiếp Tăng Tốc (Sprint Buff) lên Runner.
        /// </summary>
        public bool ActivateSprintBuff(string sender = "Blue Team", float customDuration = -1f)
        {
            ResolveReferences();
            float duration = customDuration > 0f ? customDuration : _sprintDuration;

            if (_runnerController != null)
            {
                _runnerController.ActivateSprintBuff(duration);
                _hudManager?.ShowFanAction(sender, $"Speed Boost ({duration:F0}s)");
                _hudManager?.ShowStatusPopup($"[{sender}] Speed ({duration:F0}s)", true);
                Debug.Log($"[GiftManager] {sender} -> Kích hoạt Tăng Tốc trực tiếp ({duration:F0}s).");
                return true;
            }

            Debug.LogWarning("[GiftManager] Không tìm thấy ChatLaneRunnerController để kích hoạt Sprint Buff!");
            return false;
        }

        /// <summary>
        /// Kích hoạt trực tiếp Thao Tác Tự Do (Free Control) - 0% cost chuyển làn & nhảy.
        /// </summary>
        public bool ActivateFreeControl(string sender = "Blue Team", float customDuration = -1f)
        {
            ResolveReferences();
            float duration = customDuration > 0f ? customDuration : _freeControlDuration;

            if (_runnerController != null)
            {
                _runnerController.ActivateFreeControl(duration);
                _hudManager?.ShowFanAction(sender, $"Free Control ({duration:F0}s)");
                _hudManager?.ShowStatusPopup($"[{sender}] Free Control ({duration:F0}s)", true);
                AudioManager.Instance?.PlaySFX(SFXType.RunnerSpeedBoost, 0.85f);
                Debug.Log($"[GiftManager] {sender} -> Kích hoạt Thao Tác Tự Do trực tiếp ({duration:F0}s).");
                return true;
            }

            Debug.LogWarning("[GiftManager] Không tìm thấy ChatLaneRunnerController để kích hoạt Free Control!");
            return false;
        }

        /// <summary>
        /// Kích hoạt cộng trực tiếp năng lượng cho Blue Team (Fan).
        /// </summary>
        public void AddBlueEnergy(string sender = "Blue Team", int customAmount = -1)
        {
            ResolveReferences();
            int amount = customAmount > 0 ? customAmount : _blueEnergyBottleAmount;

            if (_factionManager != null)
            {
                _factionManager.OnChatCommand(sender, "#fan");
                _factionManager.DebugAdjustFanEnergy(amount);
                _hudManager?.ShowFanAction(sender, $"+{amount} Energy");
                _hudManager?.ShowStatusPopup($"[{sender}] +{amount} Blue Energy", true);
                AudioManager.Instance?.PlaySFX(SFXType.CollectEnergy, 1.0f);
                Debug.Log($"[GiftManager] {sender} -> Tặng Bình Năng Lượng Xanh +{amount} (Total: {_factionManager.FanLikes})");
            }
            else
            {
                Debug.LogWarning("[GiftManager] Không tìm thấy FactionTugOfWarManager để cộng năng lượng Blue Team!");
            }
        }

        /// <summary>
        /// Kích hoạt cộng trực tiếp năng lượng cho Red Team (Anti).
        /// </summary>
        public void AddRedEnergy(string sender = "Red Team", int customAmount = -1)
        {
            ResolveReferences();
            int amount = customAmount > 0 ? customAmount : _redEnergyBottleAmount;

            if (_factionManager != null)
            {
                _factionManager.OnChatCommand(sender, "#anti");
                _factionManager.DebugAdjustAntiEnergy(amount);
                _hudManager?.ShowAntiAction(sender, $"+{amount} Energy");
                _hudManager?.ShowStatusPopup($"[{sender}] +{amount} Red Energy", false);
                AudioManager.Instance?.PlaySFX(SFXType.CollectEnergy, 1.0f);
                Debug.Log($"[GiftManager] {sender} -> Tặng Bình Năng Lượng Đỏ +{amount} (Total: {_factionManager.AntiLikes})");
            }
            else
            {
                Debug.LogWarning("[GiftManager] Không tìm thấy FactionTugOfWarManager để cộng năng lượng Red Team!");
            }
        }

        /// <summary>
        /// Kích hoạt chế độ Bão Xe (Unlimited Cars) trong thời gian quy định.
        /// </summary>
        public bool ActivateUnlimitedCars(string sender = "Red Team", float customDuration = -1f)
        {
            ResolveReferences();
            float duration = customDuration > 0f ? customDuration : _unlimitedCarsDuration;

            if (_obstacleSpawner != null)
            {
                if (!_obstacleSpawner.IsUnlimitedModeActive)
                {
                    _obstacleSpawner.ActivateUnlimitedMode();
                    _hudManager?.ShowAntiAction(sender, $"Unlimited Cars ({duration:F0}s)");
                    _hudManager?.ShowStatusPopup($"[{sender}] CAR STORM!", false);
                    Debug.Log($"[GiftManager] {sender} -> Kích hoạt Bão Xe ({duration:F0}s).");
                    return true;
                }
                return false;
            }

            Debug.LogWarning("[GiftManager] Không tìm thấy SingleObstacleSpawner để kích hoạt Unlimited Cars!");
            return false;
        }

        /// <summary>
        /// Kích hoạt Giai đoạn Xe Bán Tải (Pickup Truck Phase).
        /// </summary>
        public bool ActivatePickupTruckPhase(string sender = "Red Team")
        {
            ResolveReferences();
            if (_obstacleSpawner != null)
            {
                _obstacleSpawner.ActivatePickupTruckPhase();
                _hudManager?.ShowAntiAction(sender, $"Pickup Phase ({_vehiclePhaseDuration:F0}s)");
                _hudManager?.ShowStatusPopup($"[{sender}] Pickup Phase!", false);
                AudioManager.Instance?.PlaySFX(SFXType.PickupHorn, 0.9f);
                return true;
            }
            Debug.LogWarning("[GiftManager] Không tìm thấy SingleObstacleSpawner để kích hoạt Pickup Truck Phase!");
            return false;
        }

        /// <summary>
        /// Kích hoạt Giai đoạn Xe Tải Hạng Nặng (Heavy Truck Phase).
        /// </summary>
        public bool ActivateHeavyTruckPhase(string sender = "Red Team")
        {
            ResolveReferences();
            if (_obstacleSpawner != null)
            {
                _obstacleSpawner.ActivateHeavyTruckPhase();
                _hudManager?.ShowAntiAction(sender, $"Heavy Truck Phase ({_vehiclePhaseDuration:F0}s)");
                _hudManager?.ShowStatusPopup($"[{sender}] Heavy Truck Phase!", false);
                AudioManager.Instance?.PlaySFX(SFXType.HeavyTruckHorn, 1.0f);
                return true;
            }
            Debug.LogWarning("[GiftManager] Không tìm thấy SingleObstacleSpawner để kích hoạt Heavy Truck Phase!");
            return false;
        }

        /// <summary>
        /// Kích hoạt xe cản đường của Red Team có trừ năng lượng theo từng loại xe được cấu hình.
        /// </summary>
        public bool SpawnAntiCar(VehicleTier tier, string sender = "Red Team")
        {
            ResolveReferences();
            if (_obstacleSpawner == null) return false;

            bool isUnlimited = _obstacleSpawner.IsUnlimitedModeActive;
            var carConfig = GetCarConfig(tier);
            int cost = carConfig != null ? carConfig.energyCost : 100;

            if (!isUnlimited && _factionManager != null)
            {
                if (!_factionManager.TrySpendAntiEnergy(cost))
                {
                    _hudManager?.ShowAntiAction(sender, $"Need {cost} ({_factionManager.AntiLikes})");
                    // Đã loại bỏ popup lỗi thiếu năng lượng để tránh làm rác màn hình Top Banner
                    return false;
                }
            }

            bool spawned = _obstacleSpawner.TriggerSpawnCarTier(tier);
            if (spawned)
            {
                string tierName = carConfig != null ? carConfig.vehicleName : tier.ToString();
                _hudManager?.ShowAntiAction(sender, $"Spawned {tierName}");
                _hudManager?.ShowStatusPopup($"[{sender}] {tierName}", false);

                // Phát còi xe cảnh báo tương ứng ngay thời điểm quà xe được kích hoạt
                switch (tier)
                {
                    case VehicleTier.HeavyTruck:
                        AudioManager.Instance?.PlaySFX(SFXType.HeavyTruckHorn, 1.0f);
                        break;
                    case VehicleTier.PickupTruck:
                        AudioManager.Instance?.PlaySFX(SFXType.PickupHorn, 0.9f);
                        break;
                    case VehicleTier.SedanCar:
                    default:
                        AudioManager.Instance?.PlaySFX(SFXType.CarHorn, 0.85f);
                        break;
                }
                return true;
            }
            else if (!isUnlimited && _factionManager != null)
            {
                // Hoàn trả nếu làn kẹt
                _factionManager.AddAntiEnergy(cost);
            }

            return false;
        }

        /// <summary>
        /// Kích hoạt Gift Dance (nhảy múa) cho Runner.
        /// </summary>
        public bool TriggerGiftDance(string sender = "Khán Giả", float customDuration = -1f)
        {
            ResolveReferences();
            if (_giftDanceController != null)
            {
                float duration = customDuration > 0f ? customDuration : _giftDanceDuration;
                if (_giftDanceController.TriggerDance(duration))
                {
                    _hudManager?.ShowFanAction(sender, $"Dance ({duration:F0}s)");
                    _hudManager?.ShowStatusPopup($"[{sender}] Meme Dance!", true);
                    Debug.Log($"[GiftManager] {sender} -> Kích hoạt Gift Dance ({duration:F0}s).");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Kích hoạt Gift Thời Tiết Mưa (Rain Storm) từ Viewer livestream.
        /// </summary>
        public bool ActivateRainHazard(string sender = "Khán Giả", float duration = 60f)
        {
            ResolveReferences();
            if (_weatherManager != null)
            {
                _weatherManager.TriggerWeatherHazard(duration);
                _hudManager?.ShowStatusPopup($"[{sender}] Rain Storm!", false);
                Debug.Log($"[GiftManager] {sender} -> Kích hoạt Gift Thời Tiết Mưa ({duration}s).");
                return true;
            }
            Debug.LogWarning("[GiftManager] Không tìm thấy WeatherHazardManager để kích hoạt hiệu ứng Mưa!");
            return false;
        }

        #endregion
    }
}

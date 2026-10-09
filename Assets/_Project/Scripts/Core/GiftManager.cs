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
    /// Detailed configuration for Anti team obstacle vehicle tiers.
    /// Configurable parameters: energy cost, knockback, damage penalty, distance penalty, speed, etc.
    /// </summary>
    [Serializable]
    public class AntiCarGiftConfig
    {
        [Tooltip("Display name of vehicle tier")]
        public string vehicleName = "Vehicle";

        [Tooltip("Anti team energy cost to deploy this vehicle (default: 100).")]
        public int energyCost = 100;

        [Tooltip("Knockback distance in meters pushed backward upon collision.")]
        public float knockbackDistance = 2.0f;

        [Tooltip("Knockback duration in seconds.")]
        public float knockbackDuration = 0.5f;

        [Tooltip("Energy damage percentage deducted from runner on collision.")]
        [Range(0f, 100f)]
        public float energyPenaltyPercent = 20f;

        [Tooltip("Distance penalty in meters deducted from leg progress on collision.")]
        public float distancePenaltyMeters = 100f;

        [Tooltip("Vehicle forward speed traveling toward runner (m/s).")]
        public float drivingSpeed = 7.0f;

        [Tooltip("Hit-stop freeze duration on heavy collision (seconds).")]
        public float hitStopDuration = 0.1f;

        [Tooltip("Peak reverse scroll speed when world jolts backward on collision.")]
        public float reverseWorldPeakSpeed = -87.0f;

        [Tooltip("Reverse scroll duration in seconds.")]
        public float reverseWorldDuration = 1.8f;
    }

    /// <summary>
    /// Centralized manager and configuration for all interactive stream gifts.
    /// Handles gift actions, durations, costs, and cooldowns from a single component.
    /// </summary>
    [DisallowMultipleComponent]
    public class GiftManager : MonoBehaviour
    {
        public static GiftManager Instance { get; private set; }

        [Header("Blue Team Gifts")]
        [Tooltip("Shield buff duration in seconds (default: 15s).")]
        [SerializeField] private float _shieldDuration = 15f;

        [Tooltip("Fan energy cost for shield activation (0 for free when gifted by viewer).")]
        [SerializeField] private int _shieldEnergyCost = 0;

        [Tooltip("Sprint buff duration in seconds (default: 30s).")]
        [SerializeField] private float _sprintDuration = 30f;

        [Tooltip("Speed multiplier during sprint buff (1.5 = +50% speed).")]
        [SerializeField] private float _sprintSpeedMultiplier = 1.5f;

        [Tooltip("Free Control buff duration in seconds (0 energy cost for jump and lane switches).")]
        [SerializeField] private float _freeControlDuration = 30f;

        [Tooltip("Energy awarded to Blue Team per Blue Energy Bottle (default: +300).")]
        [SerializeField] private int _blueEnergyBottleAmount = 300;

        [Header("Red Team General")]
        [Tooltip("Energy awarded to Red Team per Red Energy Bottle (default: +500).")]
        [SerializeField] private int _redEnergyBottleAmount = 500;

        [Tooltip("Unlimited Cars hazard duration in seconds (default: 60s).")]
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

        [Header("Animals (Tier 2)")]
        [SerializeField] private AntiCarGiftConfig _pickupConfig = new AntiCarGiftConfig
        {
            vehicleName = "Animals",
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

        [Header("Train (Tier 3)")]
        [SerializeField] private AntiCarGiftConfig _heavyTruckConfig = new AntiCarGiftConfig
        {
            vehicleName = "Train",
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
        [Tooltip("Celebration dance duration when viewer donates Gift Dance (seconds).")]
        [SerializeField] private float _giftDanceDuration = 5f;
        [Tooltip("Icon displayed on top banner notification when Meme Dance gift is sent.")]
        [SerializeField] private Sprite _giftDanceIcon;

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

        public void SetSprintDuration(float duration) => _sprintDuration = Mathf.Max(1f, duration);
        public void SetFreeControlDuration(float duration) => _freeControlDuration = Mathf.Max(1f, duration);
        #endregion

        /// <summary>
        /// Retrieves vehicle tier configuration.
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
            // Synchronize values when modified in inspector
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

            if (_giftDanceController == null || !_giftDanceController.gameObject.activeInHierarchy)
            {
                if (_runnerController != null)
                {
                    _giftDanceController = _runnerController.GetComponent<GiftDanceController>()
                                           ?? _runnerController.GetComponentInChildren<GiftDanceController>();
                }
                if (_giftDanceController == null)
                    _giftDanceController = FindFirstObjectByType<GiftDanceController>();
            }

            if (_giftDanceController != null)
            {
                _giftDanceController.EnsureInitialized();
            }

            if (_giftDanceIcon == null)
            {
                _giftDanceIcon = GetGiftDanceIcon();
            }

            if (_weatherManager == null)
                _weatherManager = FindFirstObjectByType<WeatherHazardManager>();

            if (_hudManager == null)
                _hudManager = FindFirstObjectByType<HUDManager>();
        }

        private Sprite GetGiftDanceIcon()
        {
            if (_giftDanceIcon != null) return _giftDanceIcon;
#if UNITY_EDITOR
            _giftDanceIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Textures/TikTokGifts/dance.png");
            if (_giftDanceIcon == null)
            {
                _giftDanceIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Textures/TikTokGifts/6037.png");
            }
#endif
            return _giftDanceIcon;
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
        /// Activates protective shield buff on runner.
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
                Debug.Log($"[GiftManager] {sender} -> Activated Shield buff ({duration:F0}s).");
                return true;
            }

            Debug.LogWarning("[GiftManager] RunnerGiftEffects not found to activate Shield!");
            return false;
        }

        /// <summary>
        /// Activates Sprint buff on runner.
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
                Debug.Log($"[GiftManager] {sender} -> Activated Sprint buff ({duration:F0}s).");
                return true;
            }

            Debug.LogWarning("[GiftManager] ChatLaneRunnerController not found to activate Sprint buff!");
            return false;
        }

        /// <summary>
        /// Activates Free Control buff (0 energy cost for jump and lane switches).
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
                Debug.Log($"[GiftManager] {sender} -> Activated Free Control buff ({duration:F0}s).");
                return true;
            }

            Debug.LogWarning("[GiftManager] ChatLaneRunnerController not found to activate Free Control!");
            return false;
        }

        /// <summary>
        /// Adds energy directly to Blue Team (Fan).
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
                Debug.Log($"[GiftManager] {sender} -> Blue Energy Bottle +{amount} (Total: {_factionManager.FanLikes})");
            }
            else
            {
                Debug.LogWarning("[GiftManager] FactionTugOfWarManager not found to add Blue energy!");
            }
        }

        /// <summary>
        /// Adds energy directly to Red Team (Anti).
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
                Debug.Log($"[GiftManager] {sender} -> Red Energy Bottle +{amount} (Total: {_factionManager.AntiLikes})");
            }
            else
            {
                Debug.LogWarning("[GiftManager] FactionTugOfWarManager not found to add Red energy!");
            }
        }

        /// <summary>
        /// Activates Unlimited Cars storm mode for specified duration.
        /// </summary>
        public bool ActivateUnlimitedCars(string sender = "Red Team", float customDuration = -1f)
        {
            ResolveReferences();
            float duration = customDuration > 0f ? customDuration : _unlimitedCarsDuration;

            if (_obstacleSpawner != null)
            {
                if (!_obstacleSpawner.IsUnlimitedModeActive)
                {
                    _obstacleSpawner.ActivateUnlimitedMode(duration);
                    _hudManager?.ShowAntiAction(sender, $"Unlimited Cars ({duration:F0}s)");
                    _hudManager?.ShowStatusPopup($"[{sender}] CAR STORM!", false);
                    Debug.Log($"[GiftManager] {sender} -> Activated Unlimited Cars ({duration:F0}s).");
                    return true;
                }
                return false;
            }

            Debug.LogWarning("[GiftManager] SingleObstacleSpawner not found to activate Unlimited Cars!");
            return false;
        }

        /// <summary>
        /// Activates Pickup Truck Phase.
        /// </summary>
        public bool ActivatePickupTruckPhase(string sender = "Red Team", float customDuration = -1f)
        {
            ResolveReferences();
            float duration = customDuration > 0f ? customDuration : _vehiclePhaseDuration;
            if (_obstacleSpawner != null)
            {
                _obstacleSpawner.ActivatePickupTruckPhase(duration);
                _hudManager?.ShowAntiAction(sender, $"Animals Phase ({duration:F0}s)");
                _hudManager?.ShowStatusPopup($"[{sender}] Animals Phase!", false);
                AudioManager.Instance?.PlaySFX(SFXType.PickupHorn, 0.9f);
                return true;
            }
            Debug.LogWarning("[GiftManager] SingleObstacleSpawner not found to activate Hunting Beasts Phase!");
            return false;
        }

        /// <summary>
        /// Activates Train Phase.
        /// </summary>
        public bool ActivateHeavyTruckPhase(string sender = "Red Team", float customDuration = -1f)
        {
            ResolveReferences();
            float duration = customDuration > 0f ? customDuration : _vehiclePhaseDuration;
            if (_obstacleSpawner != null)
            {
                _obstacleSpawner.ActivateHeavyTruckPhase(duration);
                _hudManager?.ShowAntiAction(sender, $"Train Phase ({duration:F0}s)");
                _hudManager?.ShowStatusPopup($"[{sender}] Train Phase!", false);
                AudioManager.Instance?.PlaySFX(SFXType.HeavyTruckHorn, 1.0f);
                return true;
            }
            Debug.LogWarning("[GiftManager] SingleObstacleSpawner not found to activate Train Phase!");
            return false;
        }

        /// <summary>
        /// Spawns an obstacle vehicle for Red Team, deducting configured energy cost.
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
                    // Suppressed insufficient energy popup to avoid HUD clutter
                    return false;
                }
            }

            bool spawned = _obstacleSpawner.TriggerSpawnCarTier(tier);
            if (spawned)
            {
                string tierName = carConfig != null ? carConfig.vehicleName : tier.ToString();
                _hudManager?.ShowAntiAction(sender, $"Spawned {tierName}");
                _hudManager?.ShowStatusPopup($"[{sender}] {tierName}", false);

                // Play vehicle horn warning when gift is activated
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
                // Refund energy if lane is blocked
                _factionManager.AddAntiEnergy(cost);
            }

            return false;
        }

        /// <summary>
        /// Triggers celebration Gift Dance for runner and displays donor notification on HUD.
        /// </summary>
        public bool TriggerGiftDance(string sender = "Viewer", float customDuration = -1f)
        {
            ResolveReferences();
            float duration = customDuration > 0f ? customDuration : _giftDanceDuration;
            string displayName = !string.IsNullOrEmpty(sender) ? sender : "Viewer";

            // Always display donor notification on HUD Top Banner & Fan Feed
            _hudManager?.ShowFanAction(displayName, $"Dance ({duration:F0}s)");
            _hudManager?.ShowStatusPopup($"[{displayName}] Meme Dance!", true, GetGiftDanceIcon());
            Debug.Log($"[GiftManager] {displayName} -> Activated Gift Dance ({duration:F0}s).");

            if (_giftDanceController != null)
            {
                _giftDanceController.TriggerDance(duration);
            }

            return true;
        }

        /// <summary>
        /// Activates rain storm weather hazard gift from stream viewers.
        /// </summary>
        public bool ActivateRainHazard(string sender = "Viewer", float duration = 60f)
        {
            ResolveReferences();
            if (_weatherManager != null)
            {
                _weatherManager.TriggerWeatherHazard(duration);
                _hudManager?.ShowStatusPopup($"[{sender}] Rain Storm!", false);
                Debug.Log($"[GiftManager] {sender} -> Activated Rain Storm ({duration}s).");
                return true;
            }
            Debug.LogWarning("[GiftManager] WeatherHazardManager not found to activate Rain Storm!");
            return false;
        }

        #endregion
    }
}

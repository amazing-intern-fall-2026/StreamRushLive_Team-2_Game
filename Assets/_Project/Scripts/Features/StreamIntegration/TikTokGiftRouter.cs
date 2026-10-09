using System;
using System.Collections.Generic;
using UnityEngine;
using SteamRush.Core;
using SteamRush.Features.Runner;
using StreamRushLive.Features.Gifts;
using StreamRushLive.Features.Spawning;
using SteamRush.Features.UI;
using SteamRush.Features.UI.Views;

namespace SteamRush.Features.StreamIntegration
{
    public enum GiftActionType
    {
        [InspectorName("Blue: Shield")]
        Blue_Shield,

        [InspectorName("Blue: Speed Boost")]
        Blue_SpeedBoost,

        [InspectorName("Blue: Free Control")]
        Blue_FreeControl,

        [InspectorName("Blue: +Energy")]
        Blue_EnergyBottle,

        [InspectorName("Red: Hunting Beasts")]
        Red_SpawnPickup,

        [InspectorName("Red: Train")]
        Red_SpawnHeavyTruck,

        [InspectorName("Red: Unlimited Cars")]
        Red_UnlimitedCars,

        [InspectorName("Red: +Energy")]
        Red_EnergyBottle,

        [InspectorName("Special: Dance")]
        Special_GiftDance,

        [InspectorName("Special: Rain Hazard")]
        Special_RainHazard,

        [InspectorName("Special: VIP Ticket")]
        Special_VIPRelayTicket,

        [InspectorName("Dynamic: By Faction")]
        Dynamic_ByFaction,

        [InspectorName("Like: +Energy")]
        Like_Energy,

        [InspectorName("Follow: Runner")]
        Follow_Runner
    }

    [System.Serializable]
    public class TikTokGiftMapping
    {
        [Tooltip("TikTok Gift ID (e.g. 5655 for Rose, 5269 for TikTok). Match the exact ID to link effects.")]
        public int giftId = 0;

        [Tooltip("Gift name for identification in Inspector (e.g. Rose, Donut, Lion, Cap).")]
        public string giftName = "Rose";

        [Tooltip("Sprite icon for this gift (leaves empty to auto-resolve official TikTok icon).")]
        public Sprite giftIcon;

        [Tooltip("Description displayed directly on the in-game gift card UI.")]
        [UnityEngine.Serialization.FormerlySerializedAs("englishDescription")]
        public string description = "Blue Energy";

        [Tooltip("Gameplay action triggered when viewers send this gift.")]
        public GiftActionType action = GiftActionType.Blue_EnergyBottle;

        [HideInInspector]
        public float customValue = 0f;
    }

    /// <summary>
    /// Routes incoming TikTok gift events via EventBus to specific in-game gameplay actions.
    /// Supports custom gift ID/name mappings, fallback behavior, and UI card highlight effects.
    /// </summary>
    [DisallowMultipleComponent]
    public class TikTokGiftRouter : MonoBehaviour
    {
        [Header("TikTok Gift Mappings")]
        [Tooltip("Configured list of gift mappings linking TikTok gift IDs/names to gameplay actions.")]
        [SerializeField] private List<TikTokGiftMapping> _giftMappings = new List<TikTokGiftMapping>();

        [Tooltip("When enabled, gift cards on UI will display strictly according to element order.")]
        [SerializeField] private bool _showByElementOrder = true;

        [Header("Subsystems")]
        [SerializeField] private GiftManager _giftManager;
        [SerializeField] private ChatRunnerQueueManager _queueManager;
        [SerializeField] private FactionTugOfWarManager _factionManager;
        [SerializeField] private GiftInfoPanelController _giftPanelController;
        [SerializeField] private HUDManager _hudManager;

        [Header("Diagnostics")]
        [SerializeField] private bool _showPopups = true;
        [SerializeField] private bool _logEvents = true;

        public List<TikTokGiftMapping> GiftMappings => _giftMappings;

        private void Awake()
        {
            EnsureReferences();
        }

        private void Start()
        {
            EnsureReferences();
            if (_showByElementOrder && _giftPanelController != null)
            {
                _giftPanelController.SortOption = GiftSortOption.ByElementOrder;
            }
        }

        private void OnEnable()
        {
            EnsureReferences();
            EventBus.Subscribe<TikTokGiftEvent>(OnGiftReceived);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TikTokGiftEvent>(OnGiftReceived);
        }

        public void EnsureReferences()
        {
            if (_giftManager == null) _giftManager = GiftManager.Instance ?? FindFirstObjectByType<GiftManager>();
            if (_queueManager == null) _queueManager = FindFirstObjectByType<ChatRunnerQueueManager>();
            if (_factionManager == null) _factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
            if (_giftPanelController == null) _giftPanelController = FindFirstObjectByType<GiftInfoPanelController>();
            if (_hudManager == null) _hudManager = FindFirstObjectByType<HUDManager>();
        }

        public void SetGiftMappings(List<TikTokGiftMapping> mappings)
        {
            if (mappings != null)
            {
                _giftMappings = mappings;
            }
        }

        public void AddGiftMapping(TikTokGiftMapping mapping)
        {
            if (mapping == null) return;
            _giftMappings.Add(mapping);
            if (_giftPanelController != null)
            {
                _giftPanelController.BuildGiftDisplay();
            }
        }

        public bool RemoveGiftMapping(int giftId, string giftName = null)
        {
            int removed = _giftMappings.RemoveAll(m =>
                (giftId > 0 && m.giftId == giftId) ||
                (!string.IsNullOrEmpty(giftName) && string.Equals(m.giftName, giftName, StringComparison.OrdinalIgnoreCase)));

            if (removed > 0 && _giftPanelController != null)
            {
                _giftPanelController.BuildGiftDisplay();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Directly executes action of a gift mapping (for button clicks or offline testing).
        /// </summary>
        public void TriggerGiftMappingDirect(TikTokGiftMapping mapping, string displayName = "Streamer")
        {
            if (mapping == null) return;
            EnsureReferences();

            string name = !string.IsNullOrEmpty(displayName) ? displayName : "Streamer";
            FactionType faction = _factionManager != null ? _factionManager.GetFaction(name) : FactionType.Fan;

            // Audio feedback per gift action
            if (mapping.action == GiftActionType.Like_Energy)
            {
                AudioManager.Instance?.PlaySFX(SFXType.StreamLike, 0.45f);
            }

            // Trigger gift card bounce animation
            _giftPanelController?.HighlightGift(mapping.giftId, mapping.giftName);

            // Execute gift action directly
            ExecuteActionByEnum(mapping.action, name, faction);
        }

        private void OnGiftReceived(TikTokGiftEvent evt)
        {
            EnsureReferences();

            if (string.IsNullOrEmpty(evt.UserId) || string.IsNullOrEmpty(evt.GiftName))
            {
                return;
            }

            string displayName = !string.IsNullOrEmpty(evt.DisplayName) ? evt.DisplayName : evt.UserId;
            string lowerName = evt.GiftName.ToLowerInvariant();

            // Play subtle StreamLike feedback for like/heart gifts
            if (lowerName.Contains("like") || lowerName.Contains("tim") || lowerName.Contains("heart"))
            {
                AudioManager.Instance?.PlaySFX(SFXType.StreamLike, 0.45f);
            }

            FactionType faction = _factionManager != null ? _factionManager.GetFaction(evt.UserId) : FactionType.Fan;

            if (_logEvents)
            {
                Debug.Log($"[TikTokGiftRouter] GIFT: [{displayName}] sent [{evt.GiftName}] (ID: {evt.GiftId}) x{evt.RepeatCount} ({evt.TotalCoins} coins) - Faction: {faction}");
            }

            // Sync dynamic icon from live stream if available
            if (!string.IsNullOrEmpty(evt.GiftIconUrl) && evt.GiftId > 0)
            {
                _giftPanelController?.UpdateGiftIconFromLive(evt.GiftId, evt.GiftName, evt.GiftIconUrl);
            }

            _giftPanelController?.HighlightGift(evt.GiftId, evt.GiftName);

            // Priority 1: Match configured gift mappings
            if (TryExecuteGiftMapping(evt.GiftId, evt.GiftName, displayName, evt.RepeatCount, evt.TotalCoins, faction))
            {
                return;
            }

            // Priority 2: Execute fallback routing rules
            ExecuteFallbackGift(evt, displayName, lowerName, faction);
        }

        private bool TryExecuteGiftMapping(int giftId, string giftName, string displayName, int repeatCount, int totalCoins, FactionType faction)
        {
            if (_giftMappings == null || _giftMappings.Count == 0) return false;

            TikTokGiftMapping matched = null;

            if (giftId > 0)
            {
                matched = _giftMappings.Find(m => m.giftId == giftId);
            }

            if (matched == null && !string.IsNullOrEmpty(giftName))
            {
                string lower = giftName.ToLowerInvariant();
                matched = _giftMappings.Find(m => !string.IsNullOrEmpty(m.giftName) && lower.Contains(m.giftName.ToLowerInvariant()));
            }

            if (matched == null) return false;

            ExecuteActionByEnum(matched.action, displayName, faction, matched.customValue);
            return true;
        }

        /// <summary>
        /// Executes gift action by GiftActionType. Centrally managed by GiftManager.
        /// </summary>
        public void ExecuteActionByEnum(GiftActionType action, string displayName, FactionType faction, float customValue = -1f)
        {
            EnsureReferences();
            switch (action)
            {
                case GiftActionType.Blue_Shield:
                    _giftManager?.ActivateShield(displayName, customValue);
                    break;
                case GiftActionType.Blue_SpeedBoost:
                    _giftManager?.ActivateSprintBuff(displayName, customValue);
                    break;
                case GiftActionType.Blue_FreeControl:
                    _giftManager?.ActivateFreeControl(displayName, customValue);
                    break;
                case GiftActionType.Blue_EnergyBottle:
                    _giftManager?.AddBlueEnergy(displayName, customValue > 0f ? Mathf.RoundToInt(customValue) : -1);
                    break;
                case GiftActionType.Red_SpawnPickup:
                    // Activates Pickup Truck Phase
                    _giftManager?.ActivatePickupTruckPhase(displayName, customValue);
                    break;
                case GiftActionType.Red_SpawnHeavyTruck:
                    // Activates Heavy Truck Phase
                    _giftManager?.ActivateHeavyTruckPhase(displayName, customValue);
                    break;
                case GiftActionType.Red_UnlimitedCars:
                    _giftManager?.ActivateUnlimitedCars(displayName, customValue);
                    break;
                case GiftActionType.Red_EnergyBottle:
                    _giftManager?.AddRedEnergy(displayName, customValue > 0f ? Mathf.RoundToInt(customValue) : -1);
                    break;
                case GiftActionType.Special_GiftDance:
                    _giftManager?.TriggerGiftDance(displayName, customValue);
                    break;
                case GiftActionType.Special_RainHazard:
                    _giftManager?.ActivateRainHazard(displayName, customValue > 0f ? customValue : 60f);
                    break;
                case GiftActionType.Follow_Runner:
                case GiftActionType.Special_VIPRelayTicket:
                    _queueManager?.EnqueueFollowerAsRunner(displayName);
                    ShowPopup($"Runner: [{displayName}]", true);
                    break;
                case GiftActionType.Like_Energy:
                    int energy = customValue > 0f ? Mathf.RoundToInt(customValue) : 20;
                    if (faction == FactionType.Fan)
                        _factionManager?.AddLikes(FactionType.Fan, energy);
                    else
                        _factionManager?.AddLikes(FactionType.Anti, energy);
                    break;
                case GiftActionType.Dynamic_ByFaction:
                    break;
            }

            if (_logEvents)
            {
                Debug.Log($"[TikTokGiftRouter] Executed [{action}] (Value: {customValue}) from {displayName}!");
            }
        }

        private void ExecuteFallbackGift(TikTokGiftEvent evt, string displayName, string lowerName, FactionType faction)
        {
            if (lowerName.Contains("dance"))
            {
                _giftManager?.TriggerGiftDance(displayName);
                return;
            }

            if (lowerName.Contains("follow") || lowerName.Contains("vip") || lowerName.Contains("ticket") || lowerName.Contains("runner"))
            {
                _queueManager?.EnqueueFollowerAsRunner(displayName);
                ShowPopup($"Runner: [{displayName}]", true);
                return;
            }

            if (lowerName.Contains("rain") || lowerName.Contains("umbrella"))
            {
                _giftManager?.ActivateRainHazard(displayName, 60f);
                return;
            }

            if (evt.TotalCoins >= 100 || lowerName.Contains("lion") || lowerName.Contains("rocket"))
            {
                if (faction == FactionType.Anti)
                {
                    _giftManager?.ActivateUnlimitedCars(displayName, 60f);
                }
                else
                {
                    _giftManager?.ActivateHeavyTruckPhase(displayName);
                }
                return;
            }

            if (faction == FactionType.Fan)
            {
                if (lowerName.Contains("shield") || lowerName.Contains("donut") || evt.TotalCoins >= 30)
                {
                    _giftManager?.ActivateShield(displayName);
                }
                else if (lowerName.Contains("sprint") || lowerName.Contains("speed") || lowerName.Contains("cap") || lowerName.Contains("hat"))
                {
                    _giftManager?.ActivateSprintBuff(displayName);
                }
                else
                {
                    _giftManager?.AddBlueEnergy(displayName);
                }
            }
            else
            {
                if (lowerName.Contains("heavy") || lowerName.Contains("truck") || evt.TotalCoins >= 50)
                {
                    _giftManager?.ActivateHeavyTruckPhase(displayName);
                }
                else if (lowerName.Contains("pickup") || evt.TotalCoins >= 20)
                {
                    _giftManager?.ActivatePickupTruckPhase(displayName);
                }
                else if (lowerName.Contains("car") || lowerName.Contains("sedan") || evt.TotalCoins >= 10)
                {
                    _giftManager?.SpawnAntiCar(VehicleTier.SedanCar, displayName);
                }
                else
                {
                    _giftManager?.AddRedEnergy(displayName, 500);
                }
            }
        }

        private void ShowPopup(string message, bool isFan)
        {
            if (!_showPopups) return;
            EnsureReferences();
            _hudManager?.ShowStatusPopup(message, isFan);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_giftMappings != null && _giftMappings.Count > 0)
            {
                bool changed = false;
                string dir = "Assets/_Project/Textures/TikTokGifts";

                foreach (var item in _giftMappings)
                {
                    if (item.giftId > 0)
                    {
                        string idPath = $"{dir}/{item.giftId}.png";
                        Sprite idSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(idPath);
                        if (idSprite != null && item.giftIcon != idSprite)
                        {
                            item.giftIcon = idSprite;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }

            if (_giftPanelController == null)
            {
                _giftPanelController = FindFirstObjectByType<GiftInfoPanelController>();
            }

            if (_giftPanelController != null && !Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall -= RefreshGiftPanelInEditor;
                UnityEditor.EditorApplication.delayCall += RefreshGiftPanelInEditor;
            }
        }

        private void RefreshGiftPanelInEditor()
        {
            if (this == null || _giftPanelController == null) return;
            if (_showByElementOrder)
            {
                _giftPanelController.SortOption = GiftSortOption.ByElementOrder;
            }
            _giftPanelController.BuildGiftDisplay();
        }
#endif

        [ContextMenu("Show Gifts By Element Order (Left -> Right, Top -> Bottom)")]
        public void ShowGiftsByElementOrder()
        {
            _showByElementOrder = true;
            if (_giftPanelController == null)
            {
                _giftPanelController = FindFirstObjectByType<GiftInfoPanelController>();
            }

            if (_giftPanelController != null)
            {
                _giftPanelController.SortOption = GiftSortOption.ByElementOrder;
                _giftPanelController.BuildGiftDisplay();
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            if (_giftPanelController != null) UnityEditor.EditorUtility.SetDirty(_giftPanelController);
#endif
            Debug.Log("<color=#00FF88>[TikTokGiftRouter] UI display order synchronized with Gift Mappings element order.</color>");
        }

        [ContextMenu("Sort Gift Mappings By Team")]
        public void SortGiftMappingsByTeam()
        {
            if (_giftPanelController == null)
            {
                _giftPanelController = FindFirstObjectByType<GiftInfoPanelController>();
            }

            if (_giftPanelController != null)
            {
                _giftPanelController.SyncSortedOrderToClient();
            }
        }
    }
}

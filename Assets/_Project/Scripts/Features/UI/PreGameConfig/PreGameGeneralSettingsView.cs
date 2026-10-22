using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SteamRush.Features.Runner;

namespace SteamRush.Features.UI.PreGameConfig
{
    /// <summary>
    /// [EN] Manages the General / Basic game settings inputs in PreGameConfig UI.
    /// Handles user profile, target distance, energy costs, buff durations, anti vehicle settings, and live demo simulation toggles.
    /// [VI] Quản lý các trường nhập cài đặt chung / cơ bản trong giao diện PreGameConfig.
    /// Xử lý hồ sơ người dùng, cự ly mục tiêu, chi phí năng lượng, thời lượng buff, cấu hình xe cản đường và các toggle giả lập live demo.
    /// </summary>
    public class PreGameGeneralSettingsView : MonoBehaviour
    {
        [Header("Profile & Distance")]
        [SerializeField] private TMP_InputField _inputUsername;
        [SerializeField] private TMP_InputField _inputTargetDistance;
        [SerializeField] private Toggle _toggleInfiniteDistance;

        [Header("Costs & Durations")]
        [SerializeField] private TMP_InputField _inputLaneCost;
        [SerializeField] private TMP_InputField _inputJumpCost;
        [SerializeField] private TMP_InputField _inputFreeControlDuration;
        [SerializeField] private TMP_InputField _inputSprintDuration;
        [SerializeField] private Toggle _toggleEnableKeyboardControl;

        [Header("Anti Settings")]
        [SerializeField] private Toggle _toggleAutoSpawnCar;
        [SerializeField] private TMP_InputField _inputMaxCars;
        [SerializeField] private TMP_InputField _inputDistancePenalty;
        [SerializeField] private TMP_InputField _inputSedanEnergyPenalty;
        [SerializeField] private TMP_InputField _inputPickupEnergyPenalty;
        [SerializeField] private TMP_InputField _inputHeavyEnergyPenalty;

        [Header("Live Demo Simulation Toggles")]
        [SerializeField] private Toggle _toggleLiveDemoMaster;
        [SerializeField] private Toggle _toggleSimulatedChats;
        [SerializeField] private Toggle _toggleSimulatedGifts;
        [SerializeField] private Toggle _toggleSimulatedLikes;
        [SerializeField] private Toggle _toggleSimulatedFollowers;
        [SerializeField] private Toggle _toggleSimulatedDelay;
        [SerializeField] private Toggle _toggleShowHowToPlayGuide;
        [SerializeField] private Toggle _toggleShowGiftInfoPanel;
        [SerializeField] private Toggle _toggleShowStopwatch;
        [SerializeField] private Toggle _toggleShowTimerCircles;
        [SerializeField] private Toggle _toggleDebugUI;

        private float _lastFiniteDistanceKm = 1000f;

        public TMP_InputField InputUsername => _inputUsername;

        public void Initialize(Action onInfiniteToggled = null)
        {
            if (_toggleInfiniteDistance != null)
            {
                _toggleInfiniteDistance.onValueChanged.RemoveAllListeners();
                _toggleInfiniteDistance.onValueChanged.AddListener((isOn) =>
                {
                    OnInfiniteToggleChanged(isOn);
                    onInfiniteToggled?.Invoke();
                });
            }

            if (_toggleShowHowToPlayGuide != null)
            {
                _toggleShowHowToPlayGuide.onValueChanged.RemoveAllListeners();
                _toggleShowHowToPlayGuide.onValueChanged.AddListener(val =>
                {
                    var canvas = GetComponentInParent<Canvas>();
                    var guide = canvas != null ? canvas.transform.Find("HowToPlayGuide") : null;
                    if (guide != null) guide.gameObject.SetActive(val);
                });
            }

            if (_toggleShowGiftInfoPanel != null)
            {
                _toggleShowGiftInfoPanel.onValueChanged.RemoveAllListeners();
                _toggleShowGiftInfoPanel.onValueChanged.AddListener(val =>
                {
                    var canvas = GetComponentInParent<Canvas>();
                    var panel = canvas != null ? canvas.transform.Find("GiftInfoPanel") : null;
                    if (panel != null) panel.gameObject.SetActive(val);
                });
            }

            if (_toggleEnableKeyboardControl != null)
            {
                _toggleEnableKeyboardControl.onValueChanged.RemoveAllListeners();
                ApplyKeyboardControlDimming();
            }
        }

        private void ApplyKeyboardControlDimming()
        {
            if (_toggleEnableKeyboardControl == null) return;
            _toggleEnableKeyboardControl.interactable = false;
            _toggleEnableKeyboardControl.isOn = false;

            Transform row = _toggleEnableKeyboardControl.transform.parent;
            if (row != null)
            {
                var cg = row.GetComponent<CanvasGroup>();
                if (cg == null) cg = row.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0.45f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
        }

        private void OnInfiniteToggleChanged(bool isInfinite)
        {
            if (_inputTargetDistance == null) return;

            if (isInfinite)
            {
                if (float.TryParse(_inputTargetDistance.text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float curVal) && curVal > 0f)
                {
                    _lastFiniteDistanceKm = curVal;
                }
                _inputTargetDistance.text = "Infinite";
                _inputTargetDistance.interactable = false;
            }
            else
            {
                float restored = _lastFiniteDistanceKm > 0f ? _lastFiniteDistanceKm : 1000.0f;
                _inputTargetDistance.text = restored.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                _inputTargetDistance.interactable = true;
            }
        }

        public void BindConfig(PreGameConfigData data)
        {
            if (data == null) return;

            if (_inputUsername != null)
            {
                _inputUsername.text = "";
            }

            if (_toggleInfiniteDistance != null)
            {
                _toggleInfiniteDistance.isOn = data.isInfiniteDistance;
            }

            float finiteKm = (data.finiteTargetDistanceMeters > 0f && data.finiteTargetDistanceMeters < 900000000f)
                ? data.finiteTargetDistanceMeters / 1000f
                : 1000.0f;
            _lastFiniteDistanceKm = finiteKm;

            if (_inputTargetDistance != null)
            {
                if (data.isInfiniteDistance)
                {
                    _inputTargetDistance.text = "Infinite";
                    _inputTargetDistance.interactable = false;
                }
                else
                {
                    _inputTargetDistance.text = finiteKm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    _inputTargetDistance.interactable = true;
                }
            }

            if (_inputLaneCost != null) _inputLaneCost.text = data.laneChangeEnergyCost.ToString();
            if (_inputJumpCost != null) _inputJumpCost.text = data.jumpEnergyCost.ToString();
            if (_inputFreeControlDuration != null) _inputFreeControlDuration.text = data.freeControlDuration.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            if (_inputSprintDuration != null) _inputSprintDuration.text = data.sprintBuffDuration.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            if (_toggleEnableKeyboardControl != null)
            {
                ApplyKeyboardControlDimming();
                var guideCtrl = FindFirstObjectByType<Views.HowToPlayGuideController>(FindObjectsInactive.Include);
                if (guideCtrl != null)
                {
                    guideCtrl.SetRedOnlyMode(false);
                }
            }

            if (_toggleAutoSpawnCar != null) _toggleAutoSpawnCar.isOn = data.autoSpawnOnFullEnergy;
            if (_inputMaxCars != null) _inputMaxCars.text = data.maxConcurrentCars.ToString();
            if (_inputDistancePenalty != null) _inputDistancePenalty.text = data.distancePenaltyMeters.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            if (_inputSedanEnergyPenalty != null) _inputSedanEnergyPenalty.text = data.sedanEnergyPenaltyPercent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            if (_inputPickupEnergyPenalty != null) _inputPickupEnergyPenalty.text = data.pickupEnergyPenaltyPercent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            if (_inputHeavyEnergyPenalty != null) _inputHeavyEnergyPenalty.text = data.heavyTruckEnergyPenaltyPercent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

            if (_toggleLiveDemoMaster != null) _toggleLiveDemoMaster.isOn = data.enableLiveDemoSimulation;
            if (_toggleSimulatedChats != null) _toggleSimulatedChats.isOn = data.enableSimulatedChats;
            if (_toggleSimulatedGifts != null) _toggleSimulatedGifts.isOn = data.enableSimulatedGifts;
            if (_toggleSimulatedLikes != null) _toggleSimulatedLikes.isOn = data.enableSimulatedLikes;
            if (_toggleSimulatedFollowers != null) _toggleSimulatedFollowers.isOn = data.enableSimulatedFollowers;
            if (_toggleSimulatedDelay != null) _toggleSimulatedDelay.isOn = data.enableSimulatedStreamDelay;
            if (_toggleShowHowToPlayGuide != null) _toggleShowHowToPlayGuide.isOn = data.showHowToPlayGuide;
            if (_toggleShowGiftInfoPanel != null) _toggleShowGiftInfoPanel.isOn = data.showGiftInfoPanel;
            if (_toggleShowStopwatch != null) _toggleShowStopwatch.isOn = data.showStopwatchTimer;
            if (_toggleShowTimerCircles != null) _toggleShowTimerCircles.isOn = data.showTimerCircles;
            if (_toggleDebugUI != null) _toggleDebugUI.isOn = data.enableDebugUI;
        }

        public void ReadConfig(PreGameConfigData data)
        {
            if (data == null) return;

            if (_inputUsername != null)
            {
                string raw = _inputUsername.text.Trim().TrimStart('@');
                data.tiktokUsername = (string.IsNullOrEmpty(raw) || raw.Equals("Username", StringComparison.OrdinalIgnoreCase)) ? "" : raw;
            }

            if (_toggleInfiniteDistance != null && _toggleInfiniteDistance.isOn)
            {
                data.isInfiniteDistance = true;
                data.targetDistanceMeters = 999999f * 1000f;
            }
            else
            {
                data.isInfiniteDistance = false;
                float kmVal = 0f;
                bool parsed = false;

                if (_inputTargetDistance != null)
                {
                    string rawText = _inputTargetDistance.text.Replace(',', '.').Trim();
                    if (!string.Equals(rawText, "Infinite", StringComparison.OrdinalIgnoreCase))
                    {
                        parsed = float.TryParse(rawText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out kmVal);
                    }
                }

                if (parsed && kmVal > 0f)
                {
                    data.finiteTargetDistanceMeters = Mathf.Max(50f, kmVal * 1000f);
                    data.targetDistanceMeters = data.finiteTargetDistanceMeters;
                    _lastFiniteDistanceKm = kmVal;
                }
                else
                {
                    float fallback = (data.finiteTargetDistanceMeters > 0f && data.finiteTargetDistanceMeters < 900000000f)
                        ? data.finiteTargetDistanceMeters
                        : 1000000f;
                    data.finiteTargetDistanceMeters = fallback;
                    data.targetDistanceMeters = fallback;
                    _lastFiniteDistanceKm = fallback / 1000f;
                    if (_inputTargetDistance != null)
                    {
                        _inputTargetDistance.text = _lastFiniteDistanceKm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }

            if (_inputLaneCost != null && int.TryParse(_inputLaneCost.text, out int laneCost))
                data.laneChangeEnergyCost = Mathf.Max(0, laneCost);

            if (_inputJumpCost != null && int.TryParse(_inputJumpCost.text, out int jumpCost))
                data.jumpEnergyCost = Mathf.Max(0, jumpCost);

            if (_inputFreeControlDuration != null && float.TryParse(_inputFreeControlDuration.text, out float freeDuration))
                data.freeControlDuration = Mathf.Max(1f, freeDuration);

            if (_inputSprintDuration != null && float.TryParse(_inputSprintDuration.text, out float sprintDuration))
                data.sprintBuffDuration = Mathf.Max(1f, sprintDuration);

            if (_toggleEnableKeyboardControl != null)
                data.enableKeyboardControl = false;

            if (_toggleAutoSpawnCar != null)
                data.autoSpawnOnFullEnergy = _toggleAutoSpawnCar.isOn;

            if (_inputMaxCars != null && int.TryParse(_inputMaxCars.text, out int maxCars))
                data.maxConcurrentCars = Mathf.Clamp(maxCars, 1, 3);

            if (_inputDistancePenalty != null && float.TryParse(_inputDistancePenalty.text, out float distPen))
                data.distancePenaltyMeters = Mathf.Max(0f, distPen);

            if (_inputSedanEnergyPenalty != null && float.TryParse(_inputSedanEnergyPenalty.text, out float sedanPen))
                data.sedanEnergyPenaltyPercent = Mathf.Clamp(sedanPen, 0f, 100f);

            if (_inputPickupEnergyPenalty != null && float.TryParse(_inputPickupEnergyPenalty.text, out float pickupPen))
                data.pickupEnergyPenaltyPercent = Mathf.Clamp(pickupPen, 0f, 100f);

            if (_inputHeavyEnergyPenalty != null && float.TryParse(_inputHeavyEnergyPenalty.text, out float heavyPen))
                data.heavyTruckEnergyPenaltyPercent = Mathf.Clamp(heavyPen, 0f, 100f);

            if (_toggleLiveDemoMaster != null) data.enableLiveDemoSimulation = _toggleLiveDemoMaster.isOn;
            if (_toggleSimulatedChats != null) data.enableSimulatedChats = _toggleSimulatedChats.isOn;
            if (_toggleSimulatedGifts != null) data.enableSimulatedGifts = _toggleSimulatedGifts.isOn;
            if (_toggleSimulatedLikes != null) data.enableSimulatedLikes = _toggleSimulatedLikes.isOn;
            if (_toggleSimulatedFollowers != null) data.enableSimulatedFollowers = _toggleSimulatedFollowers.isOn;
            if (_toggleSimulatedDelay != null) data.enableSimulatedStreamDelay = _toggleSimulatedDelay.isOn;
            if (_toggleShowHowToPlayGuide != null) data.showHowToPlayGuide = _toggleShowHowToPlayGuide.isOn;
            if (_toggleShowGiftInfoPanel != null) data.showGiftInfoPanel = _toggleShowGiftInfoPanel.isOn;
            if (_toggleShowStopwatch != null) data.showStopwatchTimer = _toggleShowStopwatch.isOn;
            if (_toggleShowTimerCircles != null) data.showTimerCircles = _toggleShowTimerCircles.isOn;
            if (_toggleDebugUI != null) data.enableDebugUI = _toggleDebugUI.isOn;
        }

        public void AutoWireIfNull(Transform root)
        {
            if (root == null) root = transform;
            EnsureKeyboardControlRow(root);

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;

                // 1. Inputs
                if (_inputUsername == null && (n == "Input_Username" || n == "InputUsername")) _inputUsername = t.GetComponent<TMP_InputField>();
                if (_inputTargetDistance == null && (n == "Input_Distance" || n == "InputTargetDistance")) _inputTargetDistance = t.GetComponent<TMP_InputField>();
                if (_toggleInfiniteDistance == null && (n.Contains("Infinite Run") || n == "Toggle_Infinite" || n == "ToggleInfinite")) _toggleInfiniteDistance = t.GetComponentInChildren<Toggle>(true);

                if (_inputLaneCost == null && (n.Contains("Lane change energy cost") || n.Contains("LaneCost"))) _inputLaneCost = t.GetComponentInChildren<TMP_InputField>(true);
                if (_inputJumpCost == null && (n.Contains("Jump energy cost") || n.Contains("JumpCost"))) _inputJumpCost = t.GetComponentInChildren<TMP_InputField>(true);
                if (_inputFreeControlDuration == null && (n.Contains("Freedom Charm duration") || n.Contains("FreeControl"))) _inputFreeControlDuration = t.GetComponentInChildren<TMP_InputField>(true);
                if (_inputSprintDuration == null && (n.Contains("Sprint Boost duration") || n.Contains("Sprint"))) _inputSprintDuration = t.GetComponentInChildren<TMP_InputField>(true);
                if (_toggleEnableKeyboardControl == null && (n.Contains("keyboard controls") || n.Contains("KeyboardControl"))) _toggleEnableKeyboardControl = t.GetComponentInChildren<Toggle>(true);

                // 2. Anti-Faction
                if (_toggleAutoSpawnCar == null && (n.Contains("Auto-spawn car") || n.Contains("AutoSpawn"))) _toggleAutoSpawnCar = t.GetComponentInChildren<Toggle>(true);
                if (_inputMaxCars == null && (n.Contains("Max concurrent cars") || n.Contains("MaxCars"))) _inputMaxCars = t.GetComponentInChildren<TMP_InputField>(true);

                // 3. Collision Penalties
                if (_inputDistancePenalty == null && (n.Contains("Distance penalty on crash") || n.Contains("DistancePenalty"))) _inputDistancePenalty = t.GetComponentInChildren<TMP_InputField>(true);
                if (_inputSedanEnergyPenalty == null && (n.Contains("Energy lost on Sedan hit") || n.Contains("Sedan"))) _inputSedanEnergyPenalty = t.GetComponentInChildren<TMP_InputField>(true);
                if (_inputPickupEnergyPenalty == null && (n.Contains("Energy lost on Hunting Beast hit") || n.Contains("Pickup"))) _inputPickupEnergyPenalty = t.GetComponentInChildren<TMP_InputField>(true);
                if (_inputHeavyEnergyPenalty == null && (n.Contains("Energy lost on Train hit") || n.Contains("Heavy"))) _inputHeavyEnergyPenalty = t.GetComponentInChildren<TMP_InputField>(true);

                // 4. Offline Simulation
                if (_toggleLiveDemoMaster == null && (n.Contains("Auto live simulation") || n.Contains("LiveDemoMaster"))) _toggleLiveDemoMaster = t.GetComponentInChildren<Toggle>(true);
                if (_toggleSimulatedChats == null && (n.Contains("Simulate chat comments") || n.Contains("SimulatedChats"))) _toggleSimulatedChats = t.GetComponentInChildren<Toggle>(true);
                if (_toggleSimulatedGifts == null && (n.Contains("Simulate viewer gifts") || n.Contains("SimulatedGifts"))) _toggleSimulatedGifts = t.GetComponentInChildren<Toggle>(true);
                if (_toggleSimulatedLikes == null && (n.Contains("Simulate continuous hearts") || n.Contains("SimulatedLikes"))) _toggleSimulatedLikes = t.GetComponentInChildren<Toggle>(true);
                if (_toggleSimulatedFollowers == null && (n.Contains("Simulate baton handover") || n.Contains("SimulatedFollowers"))) _toggleSimulatedFollowers = t.GetComponentInChildren<Toggle>(true);
                if (_toggleSimulatedDelay == null && (n.Contains("Simulate stream broadcast delay") || n.Contains("SimulatedDelay"))) _toggleSimulatedDelay = t.GetComponentInChildren<Toggle>(true);

                // 5. HUD & Overlay Display
                if (_toggleShowHowToPlayGuide == null && (n.Contains("Show How-To-Play Guide") || n.Contains("HowToPlay"))) _toggleShowHowToPlayGuide = t.GetComponentInChildren<Toggle>(true);
                if (_toggleShowGiftInfoPanel == null && (n.Contains("Show Gift Info Panel") || n.Contains("GiftInfo"))) _toggleShowGiftInfoPanel = t.GetComponentInChildren<Toggle>(true);
                if (_toggleShowStopwatch == null && (n.Contains("Show Match Timer") || n.Contains("Stopwatch"))) _toggleShowStopwatch = t.GetComponentInChildren<Toggle>(true);
                if (_toggleShowTimerCircles == null && (n.Contains("Show Skill & Buff Timer Circles") || n.Contains("TimerCircles"))) _toggleShowTimerCircles = t.GetComponentInChildren<Toggle>(true);
                if (_toggleDebugUI == null && (n.Contains("Show Debug UI in game") || n.Contains("DebugUI"))) _toggleDebugUI = t.GetComponentInChildren<Toggle>(true);
            }
        }

        private void EnsureKeyboardControlRow(Transform root)
        {
            if (root == null) root = transform;

            Transform content = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "PanelAdvancedContent")
                {
                    content = t.Find("Viewport/Content");
                    break;
                }
            }
            if (content == null) return;

            // Find all matching keyboard control rows to clean up any duplicates
            var matchingRows = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (child.name.Contains("keyboard controls") || child.name.Contains("KeyboardControl"))
                {
                    matchingRows.Add(child);
                }
            }

            Transform rowToKeep = null;
            if (matchingRows.Count > 0)
            {
                rowToKeep = matchingRows[0];
                for (int i = 1; i < matchingRows.Count; i++)
                {
                    if (Application.isPlaying) Destroy(matchingRows[i].gameObject);
                    else DestroyImmediate(matchingRows[i].gameObject);
                }
            }
            else
            {
                var template = content.Find("Row_Auto-spawn car at 100% energy:");
                if (template != null)
                {
                    var newRow = Instantiate(template.gameObject, content, false);
                    newRow.transform.SetSiblingIndex(5);
                    rowToKeep = newRow.transform;
                }
            }

            if (rowToKeep != null)
            {
                rowToKeep.name = "Row_Enable manual keyboard controls (A/D/Space):";
                var txt = rowToKeep.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (txt != null)
                {
                    txt.text = "Enable manual keyboard controls (A/D/Space):";
                }
                _toggleEnableKeyboardControl = rowToKeep.GetComponentInChildren<Toggle>(true);
            }
        }
    }
}

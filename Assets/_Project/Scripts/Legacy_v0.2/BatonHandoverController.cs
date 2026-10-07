using System.Collections.Generic;
using UnityEngine;
using SteamRush.Track;
using SteamRush.Features.UI;
using SteamRush.Features.Runner;

namespace SteamRush.Relay
{
    /// <summary>
    /// Coordinates in-place relay handover mechanism at progress milestones (GDD v1.2 Section 6).
    /// Automatically spawns the next character from outfitVariants on the track and swaps costumes upon handover.
    /// </summary>
    public class BatonHandoverController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TrackProgressTracker progressTracker;
        [SerializeField] private RelayQueueManager relayQueueManager;
        [SerializeField] private HUDManager hudManager;
        [SerializeField] private WorldSpeedManager worldSpeedManager;
        [SerializeField] private Transform runnerTransform;

        [Header("Proxy")]
        [Tooltip("Nameplate prefab displaying the viewer's name above the handover proxy.")]
        [SerializeField] private GameObject nameplatePrefab;
        [Tooltip("Distance threshold along X axis to trigger handover completion (meters).")]
        [SerializeField] private float arrivalThresholdMeters = 0.5f;
        [SerializeField] private string waitingLabel = "Next Player...";

        [Header("Model Swap")]
        [Tooltip("List of character model prefabs for random outfit selection during baton handovers.")]
        [SerializeField] private List<GameObject> outfitVariants = new List<GameObject>();

        private HandoverProxyController _activeProxy;
        private bool _proxySpawnedForCurrentLeg;
        private string _currentOutfitName;
        private GameObject _pendingNextOutfit;
        private float _lastTotalDistance;

        // Read IsHandlingHit from RunnerCollisionHandler to defer handover
        // according to GDD 6.4 when runner is recovering from a hit.
        private RunnerCollisionHandler _runnerCollisionHandler;
        private bool _hasPendingHandover;
        private string _pendingFollowerName;

        private void Awake()
        {
            if (progressTracker == null) progressTracker = FindFirstObjectByType<TrackProgressTracker>();
            if (relayQueueManager == null) relayQueueManager = FindFirstObjectByType<RelayQueueManager>();
            if (hudManager == null) hudManager = FindFirstObjectByType<HUDManager>();
            if (worldSpeedManager == null) worldSpeedManager = FindFirstObjectByType<WorldSpeedManager>();

            if (runnerTransform != null)
            {
                _runnerCollisionHandler = runnerTransform.GetComponent<RunnerCollisionHandler>();

                // Identify active outfit on runner
                foreach (Transform child in runnerTransform)
                {
                    if (child.name.StartsWith("Character_") && child.gameObject.activeSelf)
                    {
                        _currentOutfitName = CleanOutfitName(child.name);
                        break;
                    }
                }

                // Fallback: gather existing character meshes on runner if outfitVariants is empty in inspector
                if (outfitVariants == null || outfitVariants.Count == 0)
                {
                    outfitVariants = new List<GameObject>();
                    foreach (Transform child in runnerTransform)
                    {
                        if (child.name.StartsWith("Character_") && child.GetComponent<SkinnedMeshRenderer>() != null)
                        {
                            outfitVariants.Add(child.gameObject);
                        }
                    }
                }
            }

            // Assign runner as nameplate follow target for HUD
            if (hudManager != null && runnerTransform != null)
            {
                hudManager.UpdateRunnerTarget(runnerTransform);
            }
        }

        private void OnEnable()
        {
            if (progressTracker != null)
            {
                progressTracker.ProgressChanged.AddListener(OnProgressChanged);
                progressTracker.RelayCompleted.AddListener(OnRelayCompleted);
            }

            if (relayQueueManager != null)
            {
                relayQueueManager.FollowerNameChanged.AddListener(OnFollowerNameChanged);
            }
        }

        private void OnDisable()
        {
            if (progressTracker != null)
            {
                progressTracker.ProgressChanged.RemoveListener(OnProgressChanged);
                progressTracker.RelayCompleted.RemoveListener(OnRelayCompleted);
            }

            if (relayQueueManager != null)
            {
                relayQueueManager.FollowerNameChanged.RemoveListener(OnFollowerNameChanged);
            }
        }

        private void OnRelayCompleted(int relayNumber)
        {
            // Reset proxy spawn flag for the next leg
            _proxySpawnedForCurrentLeg = false;
        }

        private void OnProgressChanged(float legDistance, float totalDistance, float goalProgress)
        {
            if (progressTracker == null) return;

            // Handle progress deduction upon obstacle collision:
            // If totalDistance drops, push proxy back by the penalty distance to keep the 100m sync
            if (_lastTotalDistance > 0f && totalDistance < _lastTotalDistance)
            {
                float distanceLost = _lastTotalDistance - totalDistance;
                if (_activeProxy != null)
                {
                    _activeProxy.transform.position += Vector3.right * distanceLost;
                }
            }
            _lastTotalDistance = totalDistance;

            // New leg start
            if (legDistance < 1f)
            {
                _proxySpawnedForCurrentLeg = false;
            }

            if (_proxySpawnedForCurrentLeg && _activeProxy != null) return;

            float aheadDistance = GetSpawnAheadDistance();
            float triggerDistance = progressTracker.RelayDistanceMeters - aheadDistance;
            if (legDistance >= triggerDistance)
            {
                _proxySpawnedForCurrentLeg = true;
                TrySpawnProxy();
            }
        }

        public float GetSpawnAheadDistance()
        {
            if (runnerTransform != null)
            {
                float diffX = transform.position.x - runnerTransform.position.x;
                if (diffX > 0f) return diffX;
            }
            return 25f;
        }

        private void TrySpawnProxy()
        {
            if (runnerTransform == null)
            {
                Debug.LogWarning("[BatonHandoverController] runnerTransform not assigned - skipping proxy spawn.");
                return;
            }

            // Empty queue = Solo Marathon Mode (GDD section 8) - no handover viewer, skip proxy.
            if (relayQueueManager == null || relayQueueManager.Count == 0)
            {
                Debug.Log("[BatonHandoverController] Queue empty - Solo Marathon Mode, skipping handover.");
                return;
            }

            string displayName = relayQueueManager.PeekNextFollower() ?? waitingLabel;

            // Pick next outfit in advance
            PickPendingNextOutfit();

            if (_pendingNextOutfit == null)
            {
                Debug.LogWarning("[BatonHandoverController] outfitVariants is empty - cannot spawn proxy.");
                return;
            }

            Vector3 spawnPosition = transform.position;
            Quaternion spawnRotation = runnerTransform.rotation;

            // 1. Instantiate selected model prefab from outfitVariants
            GameObject proxyObj = Instantiate(_pendingNextOutfit, spawnPosition, spawnRotation);
            proxyObj.name = $"Proxy_{_pendingNextOutfit.name}";

            // 2. Disable colliders on proxy to avoid unwanted collisions
            Collider[] colliders = proxyObj.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            // 3. Attach HandoverProxyController
            _activeProxy = proxyObj.GetComponent<HandoverProxyController>();
            if (_activeProxy == null)
            {
                _activeProxy = proxyObj.AddComponent<HandoverProxyController>();
            }

            // 4. Attach floating nameplate
            AttachNameplate(proxyObj, displayName);

            // 5. Setup run animation on proxy
            var runnerAnim = runnerTransform.GetComponent<Animator>();
            var proxyAnimators = proxyObj.GetComponentsInChildren<Animator>();
            for (int i = 0; i < proxyAnimators.Length; i++)
            {
                proxyAnimators[i].applyRootMotion = false;
                if (runnerAnim != null)
                {
                    proxyAnimators[i].runtimeAnimatorController = runnerAnim.runtimeAnimatorController;
                }
            }

            // 6. Attach MovingWorldObject so proxy scrolls towards runner
            MovingWorldObject mover = proxyObj.GetComponent<MovingWorldObject>();
            if (mover == null)
            {
                mover = proxyObj.AddComponent<MovingWorldObject>();
            }

            if (worldSpeedManager != null)
            {
                mover.Initialize(worldSpeedManager);
            }
        }

        private void AttachNameplate(GameObject proxyObj, string displayName)
        {
            if (nameplatePrefab == null) return;

            GameObject nameplateObj = Instantiate(nameplatePrefab, proxyObj.transform);
            if (nameplateObj != null)
            {
                // Clean clone suffix
                nameplateObj.name = "RelayNameplate";
                nameplateObj.transform.localPosition = new Vector3(0f, 2.1f, 0f);
                
                // Initialize rotation towards camera
                if (Camera.main != null)
                {
                    nameplateObj.transform.rotation = Camera.main.transform.rotation;
                }

                var tmp = nameplateObj.GetComponentInChildren<TMPro.TMP_Text>();
                if (tmp != null)
                {
                    tmp.text = displayName;
                }

                if (_activeProxy != null)
                {
                    _activeProxy.SetNameplate(nameplateObj.transform, tmp);
                }
            }
        }

        private void PickPendingNextOutfit()
        {
            if (outfitVariants == null || outfitVariants.Count == 0)
            {
                _pendingNextOutfit = null;
                return;
            }

            // Exclude current runner outfit so each handover picks a different model
            List<GameObject> candidates = outfitVariants.FindAll(o =>
            {
                if (o == null) return false;
                string cleanName = CleanOutfitName(o.name);
                return !string.Equals(cleanName, _currentOutfitName, System.StringComparison.OrdinalIgnoreCase);
            });

            if (candidates.Count > 0)
            {
                _pendingNextOutfit = candidates[Random.Range(0, candidates.Count)];
            }
            else
            {
                _pendingNextOutfit = outfitVariants[Random.Range(0, outfitVariants.Count)];
            }
        }

        private string CleanOutfitName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "";
            string clean = rawName.Replace("(Clone)", "").Trim();
            if (clean.EndsWith("_01")) clean = clean.Substring(0, clean.Length - 3);
            return clean;
        }

        // Defer handover until proxy reaches runner and runner is fully recovered from any hit
        private void OnFollowerNameChanged(string followerId)
        {
            _hasPendingHandover = true;
            _pendingFollowerName = followerId;
        }

        private void Update()
        {
            // Protect proxy: don't let proxy slip behind runner while waiting for handover
            if (_activeProxy != null && runnerTransform != null)
            {
                if (_activeProxy.transform.position.x < runnerTransform.position.x)
                {
                    Vector3 p = _activeProxy.transform.position;
                    p.x = runnerTransform.position.x;
                    _activeProxy.transform.position = p;
                }
            }

            if (!_hasPendingHandover) return;

            // GDD section 6: Defer handover until runner is upright (IsHandlingHit == false)
            if (_runnerCollisionHandler != null && _runnerCollisionHandler.IsHandlingHit) return;

            if (!HasProxyArrived()) return;

            _hasPendingHandover = false;
            CompleteHandover(_pendingFollowerName);
        }

        private bool HasProxyArrived()
        {
            if (_activeProxy == null) return true;

            // Proxy scrolls from +X towards runner. Reaching arrivalThresholdMeters signifies arrival.
            float deltaX = _activeProxy.transform.position.x - runnerTransform.position.x;
            return deltaX <= arrivalThresholdMeters;
        }

        private void CompleteHandover(string followerName)
        {
            // 1. Swap model mesh
            SwapOutfit();

            // 2. Update runner info on HUD
            if (hudManager != null)
            {
                string display = !string.IsNullOrEmpty(followerName) ? followerName : waitingLabel;
                hudManager.UpdateRunnerInfo(display, null);
                hudManager.ShowStatusPopup($"Baton Handover: {display}!", true);
            }

            // 3. Destroy proxy
            if (_activeProxy != null)
            {
                Destroy(_activeProxy.gameObject);
                _activeProxy = null;
            }

            // 4. Reset state for next leg
            _proxySpawnedForCurrentLeg = false;
            _hasPendingHandover = false;

            Debug.Log($"[BatonHandoverController] Handover complete for: {followerName} (Swapped to model: {_currentOutfitName})");
        }

        private void SwapOutfit()
        {
            if (_pendingNextOutfit == null)
            {
                PickPendingNextOutfit();
            }

            if (_pendingNextOutfit == null)
            {
                Debug.Log("[BatonHandoverController] No outfits available - skipping mesh swap.");
                return;
            }

            string targetOutfitName = CleanOutfitName(_pendingNextOutfit.name);
            bool swapped = false;

            if (runnerTransform != null)
            {
                foreach (Transform child in runnerTransform)
                {
                    if (child.name.StartsWith("Character_") && child.GetComponent<SkinnedMeshRenderer>() != null)
                    {
                        bool isMatch = string.Equals(child.name, targetOutfitName, System.StringComparison.OrdinalIgnoreCase);
                        child.gameObject.SetActive(isMatch);
                        if (isMatch)
                        {
                            swapped = true;
                            _currentOutfitName = child.name;
                        }
                    }
                }
            }

            if (swapped)
            {
                Debug.Log($"[BatonHandoverController] Swapped runner outfit to: {_currentOutfitName}");
            }
            else
            {
                Debug.LogWarning($"[BatonHandoverController] Matching child mesh for '{targetOutfitName}' not found on runner");
            }

            _pendingNextOutfit = null;
        }
    }
}

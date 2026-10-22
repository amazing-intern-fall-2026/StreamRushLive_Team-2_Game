using System;
using System.Collections.Generic;
using UnityEngine;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Single Responsibility: Manages character outfit variants, outfit selection,
    /// and visual outfit mesh swapping on the runner transform.
    /// </summary>
    [Serializable]
    public class RunnerOutfitRegistry
    {
        [SerializeField] private List<GameObject> _outfitVariants = new List<GameObject>();
        private string _currentOutfitName = "";

        public List<GameObject> OutfitVariants => _outfitVariants;
        public string CurrentOutfitName => _currentOutfitName;

        public void Initialize(Transform runnerTransform, List<GameObject> initialVariants)
        {
            if (initialVariants != null && initialVariants.Count > 0)
            {
                _outfitVariants = initialVariants;
            }

            if (runnerTransform != null)
            {
                foreach (Transform child in runnerTransform)
                {
                    if (child.name.StartsWith("Character_") && child.gameObject.activeSelf)
                    {
                        _currentOutfitName = CleanOutfitName(child.name);
                        break;
                    }
                }

                if (_outfitVariants == null || _outfitVariants.Count == 0)
                {
                    AutoCollectOutfits(runnerTransform);
                }
            }
        }

        public GameObject PickNextOutfit(Transform runnerTransform)
        {
            if (_outfitVariants == null || _outfitVariants.Count == 0)
            {
                AutoCollectOutfits(runnerTransform);
            }

            if (_outfitVariants == null || _outfitVariants.Count == 0) return null;

            List<GameObject> candidates = _outfitVariants.FindAll(o =>
            {
                if (o == null) return false;
                if (o.GetComponent<ChatLaneRunnerController>() != null || 
                    o.GetComponent<RunnerMovement>() != null || 
                    o.GetComponent<Rigidbody>() != null) return false;

                string clean = CleanOutfitName(o.name);
                return !string.Equals(clean, _currentOutfitName, StringComparison.OrdinalIgnoreCase);
            });

            return candidates.Count > 0
                ? candidates[UnityEngine.Random.Range(0, candidates.Count)]
                : (_outfitVariants.Find(o => o != null && o.GetComponent<ChatLaneRunnerController>() == null) ?? _outfitVariants[0]);
        }

        public void SwapRunnerOutfit(Transform runnerTransform, string rawTargetName)
        {
            string targetName = CleanOutfitName(rawTargetName);
            if (string.IsNullOrEmpty(targetName) || runnerTransform == null) return;

            foreach (Transform child in runnerTransform)
            {
                if (child.name.StartsWith("Character_") && child.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    bool isMatch = string.Equals(CleanOutfitName(child.name), targetName, StringComparison.OrdinalIgnoreCase);
                    child.gameObject.SetActive(isMatch);
                    if (isMatch)
                    {
                        _currentOutfitName = child.name;
                    }
                }
            }

            var runnerOutline = runnerTransform.GetComponent<CharacterVisualOutline>();
            if (runnerOutline != null)
            {
                runnerOutline.RefreshOutline();
            }
        }

        private void AutoCollectOutfits(Transform runnerTransform)
        {
            if (runnerTransform == null) return;
            if (_outfitVariants == null) _outfitVariants = new List<GameObject>();

            foreach (Transform child in runnerTransform)
            {
                if (child.name.StartsWith("Character_") && child.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    _outfitVariants.Add(child.gameObject);
                }
            }
        }

        public string CleanOutfitName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "";
            string clean = rawName.Replace("(Clone)", "").Trim();
            if (clean.EndsWith("_01")) clean = clean.Substring(0, clean.Length - 3);
            return clean;
        }
    }
}

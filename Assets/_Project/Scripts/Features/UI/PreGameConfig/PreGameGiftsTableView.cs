using System;
using System.Collections.Generic;
using UnityEngine;
using SteamRush.Features.StreamIntegration;

namespace SteamRush.Features.UI.PreGameConfig
{
    /// <summary>
    /// Manages the Gifts Table in PreGameConfig UI.
    /// Handles row instantiation from Prefab, data binding via GiftRowItemView, reordering, and saving values.
    /// </summary>
    public class PreGameGiftsTableView : MonoBehaviour
    {
        [Header("Template & Container")]
        [SerializeField] private Transform _giftsContainer;
        [SerializeField] private GameObject _giftRowTemplate;

        private readonly List<GiftRowItemView> _activeRowViews = new List<GiftRowItemView>();

        public Transform GiftsContainer => _giftsContainer;
        public GameObject GiftRowTemplate => _giftRowTemplate;

        public void SetReferences(Transform container, GameObject template)
        {
            _giftsContainer = container;
            _giftRowTemplate = template;
        }

        public void RebuildGiftsList(List<PreGameGiftItemConfig> gifts)
        {
            _activeRowViews.Clear();

            if (_giftsContainer == null || _giftRowTemplate == null)
            {
                AutoWireIfNull(transform);
            }

            if (_giftsContainer == null)
            {
                Debug.LogWarning("[PreGameGiftsTableView] _giftsContainer is null. Cannot build gifts list.");
                return;
            }

            // Clear old rows
            for (int i = _giftsContainer.childCount - 1; i >= 0; i--)
            {
                var child = _giftsContainer.GetChild(i);
                if (_giftRowTemplate != null && child.gameObject == _giftRowTemplate) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(child.gameObject);
                else Destroy(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }

            if (gifts == null || gifts.Count == 0) return;

            if (_giftRowTemplate == null)
            {
                Debug.LogError("[PreGameGiftsTableView] _giftRowTemplate is null! Make sure GiftRowTemplate.prefab is assigned or in Resources.");
                return;
            }

            PreGameGiftIconResolver.ClearCache();

            // Register master gift icons from TikTokGiftRouter
            var router = FindFirstObjectByType<TikTokGiftRouter>();
            if (router != null && router.GiftMappings != null)
            {
                foreach (var m in router.GiftMappings)
                {
                    var canonical = PreGameGiftIconResolver.ResolveGiftIcon(m.giftId, m.giftName);
                    if (canonical != null)
                    {
                        m.giftIcon = canonical;
                    }
                    else if (m.giftIcon != null)
                    {
                        PreGameGiftIconResolver.RegisterIcon(m.giftId, m.giftName, m.giftIcon);
                    }
                }
            }

            var iconMap = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            foreach (var opt in PreGameConfigData.AvailableGifts)
            {
                Sprite s = PreGameGiftIconResolver.ResolveGiftIcon(opt.giftId, opt.giftName);
                if (s != null)
                {
                    iconMap[opt.giftName] = s;
                    if (opt.giftId > 0) iconMap[opt.giftId.ToString()] = s;
                }
            }
            if (gifts != null)
            {
                foreach (var g in gifts)
                {
                    Sprite s = PreGameGiftIconResolver.ResolveGiftIcon(g.giftId, g.giftName);
                    if (s != null)
                    {
                        if (!string.IsNullOrEmpty(g.giftName)) iconMap[g.giftName] = s;
                        if (g.giftId > 0) iconMap[g.giftId.ToString()] = s;
                    }
                }
            }

            for (int i = 0; i < gifts.Count; i++)
            {
                int index = i;
                var item = gifts[i];
                var rowObj = Instantiate(_giftRowTemplate, _giftsContainer);
                rowObj.SetActive(true);

                var view = rowObj.GetComponent<GiftRowItemView>() ?? rowObj.AddComponent<GiftRowItemView>();
                _activeRowViews.Add(view);

                view.Bind(
                    item,
                    index,
                    gifts.Count,
                    iconMap,
                    onMoveUp: (idx) => MoveGift(idx, idx - 1, gifts),
                    onMoveDown: (idx) => MoveGift(idx, idx + 1, gifts),
                    onGiftChanged: (idx, oldId, oldName, newOpt) => OnRowGiftChanged(idx, oldId, oldName, newOpt, gifts, iconMap)
                );
            }
        }

        private void OnRowGiftChanged(int changedIndex, int oldGiftId, string oldGiftName, AvailableGiftInfo newGift, List<PreGameGiftItemConfig> gifts, Dictionary<string, Sprite> iconMap)
        {
            if (gifts == null || changedIndex < 0 || changedIndex >= gifts.Count) return;

            // Check if any other row already has newGift
            int collisionIndex = -1;
            for (int i = 0; i < gifts.Count; i++)
            {
                if (i != changedIndex)
                {
                    bool sameId = newGift.giftId > 0 && gifts[i].giftId > 0 && gifts[i].giftId == newGift.giftId;
                    bool sameName = string.Equals(gifts[i].giftName, newGift.giftName, StringComparison.OrdinalIgnoreCase);
                    if (sameId || sameName)
                    {
                        collisionIndex = i;
                        break;
                    }
                }
            }

            // If collision found, swap gifts so no two rows ever have duplicate gifts!
            if (collisionIndex >= 0 && collisionIndex < _activeRowViews.Count)
            {
                var otherItem = gifts[collisionIndex];
                otherItem.giftId = oldGiftId;
                otherItem.giftName = oldGiftName;

                var otherView = _activeRowViews[collisionIndex];
                if (otherView != null)
                {
                    otherView.SetGiftOptionWithoutNotify(oldGiftId, oldGiftName, iconMap);
                }

                Debug.Log($"<color=#38BDF8>[PreGameGiftsTableView] Auto-swapped gifts: Row {changedIndex + 1} ('{newGift.giftName}') <-> Row {collisionIndex + 1} ('{oldGiftName}')</color>");
            }
        }

        public void MoveGift(int from, int to, List<PreGameGiftItemConfig> gifts)
        {
            if (gifts == null || from < 0 || from >= gifts.Count || to < 0 || to >= gifts.Count) return;
            var temp = gifts[from];
            gifts[from] = gifts[to];
            gifts[to] = temp;
            RebuildGiftsList(gifts);
        }

        public void SaveCurrentValues()
        {
            if (_giftsContainer == null) return;
            for (int i = 0; i < _giftsContainer.childCount; i++)
            {
                var child = _giftsContainer.GetChild(i);
                if (_giftRowTemplate != null && child.gameObject == _giftRowTemplate) continue;
                var view = child.GetComponent<GiftRowItemView>();
                view?.SaveCurrentValues();
            }
        }

        public void AutoWireIfNull(Transform root)
        {
            if (root == null) root = transform;
            if (_giftsContainer == null)
            {
                var vp = root.Find("GiftsSection/Scroll View/Viewport/Content")
                    ?? root.Find("Scroll View/Viewport/Content");
                if (vp != null) _giftsContainer = vp;
                else
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "Content" && t.parent != null && t.parent.name == "Viewport")
                        {
                            _giftsContainer = t;
                            break;
                        }
                    }
                }
            }

            if (_giftRowTemplate == null)
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "GiftRowTemplate" || t.name == "GiftRow")
                    {
                        _giftRowTemplate = t.gameObject;
                        break;
                    }
                }

#if UNITY_EDITOR
                if (_giftRowTemplate == null)
                {
                    _giftRowTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/PreGameConfig/GiftRowTemplate.prefab");
                }
#endif
                if (_giftRowTemplate == null)
                {
                    _giftRowTemplate = Resources.Load<GameObject>("UI/PreGameConfig/GiftRowTemplate");
                }
            }
        }
    }
}

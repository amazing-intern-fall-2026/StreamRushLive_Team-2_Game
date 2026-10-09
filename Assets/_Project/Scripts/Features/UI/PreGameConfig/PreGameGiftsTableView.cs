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

        public Transform GiftsContainer => _giftsContainer;
        public GameObject GiftRowTemplate => _giftRowTemplate;

        public void SetReferences(Transform container, GameObject template)
        {
            _giftsContainer = container;
            _giftRowTemplate = template;
        }

        public void RebuildGiftsList(List<PreGameGiftItemConfig> gifts)
        {
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

            // Cache gift icons from TikTokGiftRouter and AvailableGifts
            var router = FindFirstObjectByType<TikTokGiftRouter>();
            var iconMap = new Dictionary<string, Sprite>();
            if (router != null && router.GiftMappings != null)
            {
                foreach (var m in router.GiftMappings)
                {
                    if (m.giftIcon != null && !iconMap.ContainsKey(m.giftName.ToLowerInvariant()))
                    {
                        iconMap[m.giftName.ToLowerInvariant()] = m.giftIcon;
                    }
                }
            }

            foreach (var opt in PreGameConfigData.AvailableGifts)
            {
                string key = opt.giftName.ToLowerInvariant();
                if (!iconMap.ContainsKey(key))
                {
                    Sprite s = PreGameUIBuilder.ResolveGiftIcon(opt.giftId, opt.giftName);
                    if (s != null) iconMap[key] = s;
                }
            }

            for (int i = 0; i < gifts.Count; i++)
            {
                int index = i;
                var item = gifts[i];
                var rowObj = Instantiate(_giftRowTemplate, _giftsContainer);
                rowObj.SetActive(true);

                var view = rowObj.GetComponent<GiftRowItemView>() ?? rowObj.AddComponent<GiftRowItemView>();
                view.Bind(
                    item,
                    index,
                    gifts.Count,
                    iconMap,
                    onMoveUp: (idx) => MoveGift(idx, idx - 1, gifts),
                    onMoveDown: (idx) => MoveGift(idx, idx + 1, gifts)
                );
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

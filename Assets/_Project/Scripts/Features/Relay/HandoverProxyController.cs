using TMPro;
using UnityEngine;

namespace SteamRush.Relay
{
    /// <summary>
    /// Quản lý hiển thị thông tin người chơi tiếp theo trên nhân vật Proxy chờ tại mốc chuyển gậy.
    /// Hỗ trợ Billboard xoay nameplate mượt mà hướng về Main Camera.
    /// </summary>
    public class HandoverProxyController : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Transform nameplateTransform;
        private Camera _cachedCamera;

        private void Awake()
        {
            _cachedCamera = Camera.main;
            ResolveReferences();
        }

        public void SetNameplate(Transform npTransform, TMP_Text label = null)
        {
            nameplateTransform = npTransform;
            if (label != null)
            {
                nameLabel = label;
            }
            else if (nameplateTransform != null)
            {
                nameLabel = nameplateTransform.GetComponentInChildren<TMP_Text>();
            }
        }

        private void ResolveReferences()
        {
            if (nameLabel == null)
            {
                nameLabel = GetComponentInChildren<TMP_Text>();
            }

            if (nameplateTransform == null && nameLabel != null)
            {
                nameplateTransform = nameLabel.transform.parent != null ? nameLabel.transform.parent : nameLabel.transform;
            }
        }

        public void SetInfo(string displayName, Sprite avatar = null)
        {
            ResolveReferences();

            if (nameLabel != null)
            {
                nameLabel.text = displayName;
            }
        }

        private void LateUpdate()
        {
            if (nameplateTransform == null) return;

            if (_cachedCamera == null)
            {
                _cachedCamera = Camera.main;
                if (_cachedCamera == null) return;
            }

            // Billboard: Luôn hướng Nameplate về phía camera chính để không bị xoay lệch
            nameplateTransform.rotation = _cachedCamera.transform.rotation;
        }
    }
}

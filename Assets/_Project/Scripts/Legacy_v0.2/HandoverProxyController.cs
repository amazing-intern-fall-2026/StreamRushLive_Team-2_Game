using TMPro;
using UnityEngine;

namespace SteamRush.Relay
{
    /// <summary>
    /// Displays next player info on the handover proxy character at relay milestones.
    /// Billboard behavior aligns the nameplate towards Main Camera.
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

            // Billboard: Keep nameplate oriented toward main camera
            nameplateTransform.rotation = _cachedCamera.transform.rotation;
        }
    }
}

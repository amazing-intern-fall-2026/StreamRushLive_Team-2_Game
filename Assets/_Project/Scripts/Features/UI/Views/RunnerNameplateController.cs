using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamRush.Core.Typography;

namespace SteamRush.Features.UI.Views
{
    // View: displays world-space nameplate (avatar + name) floating above active runner.
    public class RunnerNameplateController : MonoBehaviour
    {
        private const float NameplateHeight = 2.2f;

        [Header("UI Elements")]
        [SerializeField] private Image avatar;
        [SerializeField] private TMP_Text runnerName;

        [Header("VIP Styling")]
        [Tooltip("Default name text color.")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("Highlighted name text color for VIP runners.")]
        [SerializeField] private Color vipColor = new Color(1f, 0.85f, 0.1f, 1f);

        [Header("Position Offset Settings")]
        [SerializeField] private float offsetX = 0f;

        [Tooltip("Vertical padding above the runner's head.")]
        [SerializeField] private float extraPadding = 0.3f;

        [SerializeField] private float offsetZ = 0f;

        [Header("Rotation Settings")]
        [Tooltip("Billboard face towards camera if true.")]
        [SerializeField] private bool faceCamera = true;

        [Tooltip("Fixed Euler angles when faceCamera is false.")]
        [SerializeField] private Vector3 fixedRotation = Vector3.zero;

        [Tooltip("Rotation offset added when faceCamera is true.")]
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;

        [Header("Size & Scaling Settings")]
        [Tooltip("Overall scale multiplier for the runner nameplate. Default: 1.6x.")]
        [Range(0.5f, 3.5f)]
        [SerializeField] private float scaleMultiplier = 1.6f;

        [Tooltip("Diameter size of the avatar in pixels.")]
        [SerializeField] private float avatarSize = 80f;

        [Tooltip("Font size of the runner name text.")]
        [SerializeField] private float nameFontSize = 42f;

        [Tooltip("Target runner transform to follow.")]
        [SerializeField] private Transform target;
        private Transform nameplateAnchor;
        private Camera cachedCamera;

        public float OffsetX
        {
            get => offsetX;
            set => offsetX = value;
        }

        public float ExtraPadding
        {
            get => extraPadding;
            set => extraPadding = value;
        }

        public float OffsetZ
        {
            get => offsetZ;
            set => offsetZ = value;
        }

        public bool FaceCamera
        {
            get => faceCamera;
            set => faceCamera = value;
        }

        public Vector3 FixedRotation
        {
            get => fixedRotation;
            set => fixedRotation = value;
        }

        public Vector3 RotationOffset
        {
            get => rotationOffset;
            set => rotationOffset = value;
        }

        public float ScaleMultiplier
        {
            get => scaleMultiplier;
            set
            {
                scaleMultiplier = Mathf.Clamp(value, 0.5f, 3.5f);
                ApplyVisualSizing();
            }
        }

        public float AvatarSizeSetting
        {
            get => avatarSize;
            set
            {
                avatarSize = Mathf.Max(20f, value);
                ApplyVisualSizing();
            }
        }

        public float NameFontSizeSetting
        {
            get => nameFontSize;
            set
            {
                nameFontSize = Mathf.Max(12f, value);
                ApplyVisualSizing();
            }
        }

        [Header("Default Visuals")]
        [Tooltip("Fallback default avatar sprite if runner does not provide one.")]
        [SerializeField] private Sprite defaultAvatar;
        private Sprite _initialAvatar;

        private void Awake()
        {
            cachedCamera = Camera.main;

            // Increase world-space dynamic pixel density so avatars and fonts render razor sharp
            var scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null)
            {
                scaler.dynamicPixelsPerUnit = 4f;
                scaler.referencePixelsPerUnit = 100f;
            }

            // Cache initial avatar sprite to serve as fallback.
            if (avatar != null && avatar.sprite != null)
            {
                _initialAvatar = avatar.sprite;
            }

            if (target != null && nameplateAnchor == null)
            {
                nameplateAnchor = target.Find("NameplateAnchor");
            }

            // Use existing scene rotation if fixedRotation is not configured
            if (fixedRotation == Vector3.zero && transform.rotation != Quaternion.identity)
            {
                fixedRotation = transform.eulerAngles;
            }

            ApplyVisualSizing();
        }

        public void ApplyVisualSizing()
        {
            transform.localScale = Vector3.one * (0.005f * scaleMultiplier);

            float avatarCenterY = 12f + (avatarSize * 0.5f);

            if (avatar != null)
            {
                // When avatar is parented inside a Mask GameObject ("Avatar"), scale the parent mask container
                Transform avatarParent = avatar.transform.parent != null && avatar.transform.parent != transform 
                    ? avatar.transform.parent 
                    : avatar.transform;

                RectTransform parentRect = avatarParent.GetComponent<RectTransform>();
                if (parentRect != null)
                {
                    parentRect.sizeDelta = new Vector2(avatarSize, avatarSize);
                    parentRect.anchoredPosition = new Vector2(0f, avatarCenterY);
                }

                RectTransform avatarRect = avatar.GetComponent<RectTransform>();
                if (avatarRect != null && avatar.transform.parent == transform)
                {
                    avatarRect.sizeDelta = new Vector2(avatarSize, avatarSize);
                    avatarRect.anchoredPosition = new Vector2(0f, avatarCenterY);
                }

                Transform glow = transform.Find("Glow");
                if (glow != null)
                {
                    RectTransform glowRect = glow.GetComponent<RectTransform>();
                    if (glowRect != null)
                    {
                        glowRect.sizeDelta = new Vector2(avatarSize * 1.25f, avatarSize * 1.25f);
                        glowRect.anchoredPosition = new Vector2(0f, avatarCenterY);
                    }
                }
            }

            if (runnerName != null)
            {
                runnerName.fontSize = nameFontSize;
                RectTransform nameRect = runnerName.GetComponent<RectTransform>();
                if (nameRect != null)
                {
                    nameRect.sizeDelta = new Vector2(Mathf.Max(240f, nameFontSize * 6.5f), nameFontSize * 1.4f);
                    nameRect.anchoredPosition = new Vector2(0f, -15f);
                }
            }
        }

        // Target runner transform to track above head; null hides nameplate
        public void SetTarget(Transform runner)
        {
            target = runner;
            nameplateAnchor = target != null ? target.Find("NameplateAnchor") : null;
            gameObject.SetActive(target != null);
        }

        // Updates display name, avatar, and VIP badge status on nameplate.
        public void SetRunnerInfo(string name, Sprite runnerAvatar, bool isVip)
        {
            if (runnerName != null)
            {
                runnerName.text = ViewerNameSanitizer.SanitizeDisplayName(name);
                runnerName.color = isVip ? vipColor : normalColor;
            }

            if (avatar != null)
            {
                // If new avatar provided, use it; otherwise fallback to default avatar so previous runner's avatar is replaced.
                Sprite fallback = defaultAvatar != null ? defaultAvatar : _initialAvatar;
                Sprite finalSprite = runnerAvatar != null ? runnerAvatar : fallback;
                avatar.sprite = finalSprite;

                if (finalSprite != null && finalSprite.texture != null)
                {
                    finalSprite.texture.filterMode = FilterMode.Trilinear;
                    finalSprite.texture.anisoLevel = 16;
                }
            }
        }

        public void SetRunnerInfo(string name, Sprite runnerAvatar)
        {
            SetRunnerInfo(name, runnerAvatar, false);
        }

        public void SetNameColor(Color color)
        {
            if (runnerName != null)
            {
                runnerName.color = color;
            }
        }

        /// <summary>
        /// Sets fixed world rotation for nameplate, disabling camera billboarding.
        /// </summary>
        public void SetFixedRotation(Vector3 euler)
        {
            fixedRotation = euler;
            faceCamera = false;
        }

        /// <summary>
        /// Sets camera billboarding rotation offset.
        /// </summary>
        public void SetRotationOffset(Vector3 offset)
        {
            rotationOffset = offset;
        }

        private float GetTopY()
        {
            // Priority: anchor transform -> model bounds -> collider -> default offset
            if (nameplateAnchor != null)
            {
                return nameplateAnchor.position.y;
            }

            if (target == null)
            {
                return 0f;
            }

            Renderer modelRenderer = target.GetComponentInChildren<Renderer>();
            if (modelRenderer != null)
            {
                return modelRenderer.bounds.max.y;
            }

            Collider modelCollider = target.GetComponentInChildren<Collider>();
            if (modelCollider != null)
            {
                return modelCollider.bounds.max.y;
            }

            return target.position.y + NameplateHeight;
        }

        private void LateUpdate()
        {
            // Update nameplate rotation in LateUpdate
            UpdateRotation();

            // Update nameplate position tracking target
            if (target != null)
            {
                UpdatePosition();
            }
        }

        /// <summary>
        /// Applies billboard or fixed rotation each frame.
        /// </summary>
        private void UpdateRotation()
        {
            if (faceCamera)
            {
                if (cachedCamera == null)
                {
                    cachedCamera = Camera.main;
                }

                if (cachedCamera != null)
                {
                    transform.rotation = cachedCamera.transform.rotation * Quaternion.Euler(rotationOffset);
                }
                else
                {
                    transform.rotation = Quaternion.Euler(fixedRotation);
                }
            }
            else
            {
                transform.rotation = Quaternion.Euler(fixedRotation);
            }
        }

        /// <summary>
        /// Updates position tracking runner head position with XYZ offsets.
        /// </summary>
        private void UpdatePosition()
        {
            transform.position = new Vector3(
                target.position.x + offsetX,
                GetTopY() + extraPadding,
                target.position.z + offsetZ);
        }
    }
}

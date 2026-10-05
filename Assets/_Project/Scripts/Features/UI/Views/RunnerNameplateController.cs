using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField] private float avatarSize = 90f;

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

        private void Awake()
        {
            cachedCamera = Camera.main;

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

            if (avatar != null)
            {
                RectTransform avatarRect = avatar.GetComponent<RectTransform>();
                if (avatarRect != null)
                {
                    avatarRect.sizeDelta = new Vector2(avatarSize, avatarSize);
                }

                Transform glow = transform.Find("Glow");
                if (glow != null)
                {
                    RectTransform glowRect = glow.GetComponent<RectTransform>();
                    if (glowRect != null)
                    {
                        glowRect.sizeDelta = new Vector2(avatarSize * 1.45f, avatarSize * 1.45f);
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

        // Updates display name, avatar, and VIP badge status on nameplate
        public void SetRunnerInfo(string name, Sprite runnerAvatar, bool isVip)
        {
            if (runnerName != null)
            {
                runnerName.text = name;
                runnerName.color = isVip ? vipColor : normalColor;
            }

            if (avatar != null && runnerAvatar != null)
            {
                avatar.sprite = runnerAvatar;
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

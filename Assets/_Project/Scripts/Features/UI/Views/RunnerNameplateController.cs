using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI.Views
{
    // View: chỉ hiển thị bảng tên (avatar tròn + tên) world-space lơ lửng trên đầu runner,
    // không truy cập module khác (SRP).
    public class RunnerNameplateController : MonoBehaviour
    {
        private const float NameplateHeight = 2.2f;

        [Header("UI Elements")]
        [SerializeField] private Image avatar;
        [SerializeField] private TMP_Text runnerName;

        [Header("Position Offset Settings")]
        [Tooltip("Độ lệch vị trí trục X so với Runner (cho phép dịch chuyển bảng tên sang trái/phải).")]
        [SerializeField] private float offsetX = 0f;

        [Tooltip("Khoảng cách đệm chiều cao trục Y phía trên đầu Runner.")]
        [SerializeField] private float extraPadding = 0.3f;

        [Tooltip("Độ lệch vị trí trục Z so với Runner.")]
        [SerializeField] private float offsetZ = 0f;

        [Header("Rotation Settings")]
        [Tooltip("Bật để tự động xoay mặt theo Camera (Billboard). Tắt để sử dụng góc xoay cố định được setup bên dưới.")]
        [SerializeField] private bool faceCamera = true;

        [Tooltip("Góc xoay cố định (Euler angles) khi faceCamera tắt - cập nhật liên tục mỗi frame.")]
        [SerializeField] private Vector3 fixedRotation = Vector3.zero;

        [Tooltip("Góc xoay bù trừ (Offset Euler) cộng thêm khi faceCamera đang bật - cập nhật liên tục mỗi frame.")]
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;

        private Transform target;
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

        private void Awake()
        {
            cachedCamera = Camera.main;

            // Nếu người dùng đã xoay sẵn trong Scene mà fixedRotation chưa đặt, lấy góc xoay đó làm mặc định
            if (fixedRotation == Vector3.zero && transform.rotation != Quaternion.identity)
            {
                fixedRotation = transform.eulerAngles;
            }
        }

        // Runner cần theo dõi để bảng tên bám theo đúng vị trí phía trên đầu; null = ẩn bảng tên (hàng đợi trống).
        public void SetTarget(Transform runner)
        {
            target = runner;
            nameplateAnchor = target != null ? target.Find("NameplateAnchor") : null;
            gameObject.SetActive(target != null);
        }

        // Cập nhật tên hiển thị và avatar tròn của runner hiện tại trên bảng tên.
        public void SetRunnerInfo(string name, Sprite runnerAvatar)
        {
            if (runnerName != null)
            {
                runnerName.text = name;
            }

            if (avatar != null)
            {
                avatar.sprite = runnerAvatar;
            }
        }

        /// <summary>
        /// Gán góc xoay cố định cho bảng tên và tắt chế độ bám theo camera.
        /// </summary>
        public void SetFixedRotation(Vector3 euler)
        {
            fixedRotation = euler;
            faceCamera = false;
        }

        /// <summary>
        /// Gán góc xoay bù khi đang bật chế độ bám theo camera.
        /// </summary>
        public void SetRotationOffset(Vector3 offset)
        {
            rotationOffset = offset;
        }

        private float GetTopY()
        {
            // Ưu tiên anchor, sau đó dùng bounds của model, rồi collider và cuối cùng là mặc định.
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
            // Cập nhật góc xoay liên tục mỗi frame trong LateUpdate
            UpdateRotation();

            // Cập nhật vị trí bám theo target nếu có
            if (target != null)
            {
                UpdatePosition();
            }
        }

        /// <summary>
        /// Xử lý cập nhật góc xoay liên tục theo thiết lập Inspector mỗi frame
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
        /// Cập nhật vị trí bám sát phía trên đầu runner kết hợp độ lệch (Offsets) X, Y, Z
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

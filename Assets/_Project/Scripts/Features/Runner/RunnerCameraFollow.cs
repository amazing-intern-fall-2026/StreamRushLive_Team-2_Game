namespace SteamRush.Features.Runner
{
    using UnityEngine;

    /// Camera side-scroller kiểu "Dino Chrome": vì Runner đứng yên hoàn toàn (mô hình
    /// treadmill), camera KHÔNG cần bám theo bất kỳ Transform nào — chỉ cần đặt cố định 1 lần
    /// trong Inspector là đủ, không dao động, không xoay, không "third-person" lệch góc.
    /// Class này chỉ còn giữ lại để dự phòng bám theo trục Y (ví dụ muốn camera hơi nhấp nhô
    /// theo cú nhảy cho sống động hơn) — mặc định tắt (_followTargetY = false).
    public class RunnerCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;

        [Tooltip("Smoothly follow runner's vertical jump height if enabled.")]
        [SerializeField] private bool _followTargetY = false;

        [SerializeField] private float _followSmoothTime = 0.15f;

        private Vector3 _velocity;
        private float _fixedX;
        private float _fixedZ;

        private void Start()
        {
            _fixedX = transform.position.x;
            _fixedZ = transform.position.z;
        }

        private void LateUpdate()
        {
            if (!_followTargetY || _target == null) return;

            Vector3 desiredPosition = new Vector3(_fixedX, _target.position.y + transform.position.y, _fixedZ);
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, _followSmoothTime);
        }
    }
}
namespace SteamRush.Features.Runner
{
    using UnityEngine;

    /// <summary>
    /// Gift Dance: Runner vừa chạy vừa nhảy múa trong _danceDuration giây (mặc định 60s) khi được
    /// kích hoạt. Hiện F8 (qua MockChatConsole) gọi TriggerDance(); sau này gift thật từ stream
    /// chỉ cần gọi cùng hàm này.
    ///
    /// Cách hoạt động: Player.controller có layer "Dance" (Override, Weight 0) chứa 1 state Dance
    /// (loop). Script CHỈ đổi Weight của layer đó (0 -> 1 -> 0), không đụng Base Layer, nên
    /// Running/Jump, isGrounded, vật lý, thế giới cuộn, đổi làn và né xe chạy nguyên như cũ. Hết
    /// giờ, Base Layer hiện lại đúng trạng thái đang có (Running, hoặc Jump nếu đang ở trên không).
    ///
    /// Thời gian đếm bằng Time.deltaTime (scaled): hit-stop hoặc pause đều tự dừng đồng hồ.
    /// KHÔNG đụng Animator.speed — ChatLaneRunnerController đang điều khiển nó theo tốc độ thế giới
    /// nên dance cũng nhanh/chậm theo nhịp thế giới.
    /// </summary>
    public class GiftDanceController : MonoBehaviour
    {
        [Header("Dance Settings")]
        [Tooltip("Tên layer trong Player.controller chứa state nhảy.")]
        [SerializeField] private string _danceLayerName = "Dance";
        [Tooltip("Tên state nhảy (Loop Time bật) bên trong layer Dance.")]
        [SerializeField] private string _danceStateName = "Dance";
        [Tooltip("Thời gian nhảy (giây). Task Gift Dance = 60s.")]
        [SerializeField] private float _danceDuration = 60f;
        [Tooltip("Thời gian blend vào/ra giữa animation chạy và nhảy (giây). Đã tính trong 60s.")]
        [SerializeField] private float _blendTime = 0.25f;

        [Header("Victory Dance (GDD v1.4.1 - Finish Line)")]
        [Tooltip("Ten state animation rieng cho luc ve dich (Kevin Iglesias HumanM@Dance01), khac voi state Dance dung cho Gift Meme-Dance de khong bi trung - cung layer 'Dance', chi khac state.")]
        [SerializeField] private string _victoryDanceStateName = "VictoryDance";

        private Animator _animator;
        private int _danceLayerIndex = -1;
        private int _danceStateHash;
        private int _victoryDanceStateHash;
        private float _remainingTime;
        private float _currentWeight;

        public bool IsDancing => _remainingTime > 0f;
        public float RemainingSeconds => Mathf.Max(0f, _remainingTime);
        public float Duration => _danceDuration;

        private void Awake()
        {
            // Animator có thể nằm trên Player hoặc object con — tìm giống ChatLaneRunnerController.
            _animator = GetComponentInChildren<Animator>();
            if (_animator == null)
            {
                Debug.LogError("[GiftDance] Không tìm thấy Animator trên Player hoặc object con!", this);
                return;
            }

            _danceLayerIndex = _animator.GetLayerIndex(_danceLayerName);
            if (_danceLayerIndex < 0)
            {
                Debug.LogError($"[GiftDance] Player.controller chưa có layer '{_danceLayerName}'!", this);
                return;
            }

            _danceStateHash = Animator.StringToHash(_danceStateName);
            _victoryDanceStateHash = Animator.StringToHash(_victoryDanceStateName);
            _animator.SetLayerWeight(_danceLayerIndex, 0f);
        }

        /// <summary>
        /// Bắt đầu nhảy. Trả về false nếu đang nhảy sẵn (bấm lại bị bỏ qua, không reset 60s)
        /// hoặc Animator/layer chưa sẵn sàng.
        /// </summary>
        /// <param name="overrideDuration">
        /// Ghi đè _danceDuration mặc định (vd. VictoryCeremonyController muốn nhảy "vô thời hạn"
        /// tới hết phiên live). Bỏ trống hoặc &lt;= 0 thì dùng _danceDuration như cũ.
        /// </param>
        public bool TriggerDance(float overrideDuration = -1f)
        {
            return PlayState(_danceStateHash, overrideDuration);
        }

        /// <summary>
        /// GDD v1.4.1 muc 7 (Victory Celebration): nhay state VictoryDance rieng (Kevin Iglesias
        /// HumanM@Dance01) thay vi state Dance dung chung voi Gift Meme-Dance, tranh trung lap.
        /// </summary>
        public bool TriggerVictoryDance(float overrideDuration = -1f)
        {
            return PlayState(_victoryDanceStateHash, overrideDuration);
        }

        private bool PlayState(int stateHash, float overrideDuration)
        {
            if (_animator == null || _danceLayerIndex < 0) return false;
            if (IsDancing) return false;

            _remainingTime = overrideDuration > 0f ? overrideDuration : _danceDuration;

            // Layer weight 0 vẫn chạy ngầm state machine, nên phải ép về đầu clip để dance
            // luôn bắt đầu từ frame đầu thay vì giữa chừng.
            _animator.Play(stateHash, _danceLayerIndex, 0f);
            return true;
        }

        /// <summary>Dừng dance: mặc định blend ra mượt, immediate = true thì về chạy bộ ngay.</summary>
        public void StopDance(bool immediate = false)
        {
            _remainingTime = 0f;
            if (immediate)
            {
                ForceReset();
            }
        }

        private void Update()
        {
            if (_animator == null || _danceLayerIndex < 0) return;

            if (_remainingTime > 0f)
            {
                _remainingTime -= Time.deltaTime;
            }

            // Blend ra bắt đầu ở _blendTime giây cuối, để tổng thời gian đúng _danceDuration.
            float targetWeight = _remainingTime > _blendTime ? 1f : 0f;
            if (Mathf.Approximately(_currentWeight, targetWeight)) return;

            float step = _blendTime > 0.0001f ? Time.deltaTime / _blendTime : 1f;
            _currentWeight = Mathf.MoveTowards(_currentWeight, targetWeight, step);
            _animator.SetLayerWeight(_danceLayerIndex, _currentWeight);
        }

        private void OnDisable()
        {
            ForceReset();
        }

        private void ForceReset()
        {
            _remainingTime = 0f;
            _currentWeight = 0f;
            if (_animator != null && _danceLayerIndex >= 0)
            {
                _animator.SetLayerWeight(_danceLayerIndex, 0f);
            }
        }
    }
}
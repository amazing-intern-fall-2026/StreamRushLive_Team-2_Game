namespace SteamRush.Features.Runner
{
    using UnityEngine;

    /// <summary>
    /// Gift Dance: Runner executes a celebration dance for _danceDuration seconds (default 60s).
    /// Modulates the Weight of the "Dance" override layer (0 -> 1 -> 0) without affecting
    /// physics, movement, or the base layer state machine.
    /// </summary>
    public class GiftDanceController : MonoBehaviour
    {
        [Header("Dance Settings")]
        [Tooltip("Layer name in Player.controller containing the dance state.")]
        [SerializeField] private string _danceLayerName = "Dance";
        [Tooltip("State name of looping dance clip inside Dance layer.")]
        [SerializeField] private string _danceStateName = "Dance";
        // Dance duration is managed via GiftManager
        private float _danceDuration = 5f;
        [Tooltip("Blend duration (seconds) between running and dance animation.")]
        [SerializeField] private float _blendTime = 0.25f;

        [Header("Victory Dance (GDD v1.4.1 - Finish Line)")]
        [Tooltip("Animation state name for finish line victory celebration (Kevin Iglesias HumanM@Dance01) in Dance layer.")]
        [SerializeField] private string _victoryDanceStateName = "VictoryDance";

        private Animator _animator;
        private int _danceLayerIndex = -1;
        private int _danceStateHash;
        private int _victoryDanceStateHash;
        private float _remainingTime;
        private float _currentWeight;

        public bool IsDancing => _remainingTime > 0f;
        public float RemainingSeconds => Mathf.Max(0f, _remainingTime);
        public float Duration => StreamRushLive.Features.Gifts.GiftManager.Instance != null 
            ? StreamRushLive.Features.Gifts.GiftManager.Instance.GiftDanceDuration 
            : _danceDuration;

        private void Awake()
        {
            // Animator can be on Player root or child - search recursively
            _animator = GetComponentInChildren<Animator>();
            if (_animator == null)
            {
                Debug.LogError("[GiftDance] Animator not found on Player or children!", this);
                return;
            }

            _danceLayerIndex = _animator.GetLayerIndex(_danceLayerName);
            if (_danceLayerIndex < 0)
            {
                Debug.LogError($"[GiftDance] Player.controller missing layer '{_danceLayerName}'!", this);
                return;
            }

            _danceStateHash = Animator.StringToHash(_danceStateName);
            _victoryDanceStateHash = Animator.StringToHash(_victoryDanceStateName);
            _animator.SetLayerWeight(_danceLayerIndex, 0f);
        }

        /// <summary>
        /// Starts celebration dance. Returns false if already dancing or layer is unready.
        /// </summary>
        /// <param name="overrideDuration">
        /// Overrides default _danceDuration if > 0.
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

            _remainingTime = overrideDuration > 0f ? overrideDuration : Duration;

            // Reset state to normalized time 0 so dance begins from the first frame
            _animator.Play(stateHash, _danceLayerIndex, 0f);

            // Play dedicated dance background music, ducking game BGM
            AudioManager.Instance?.StartDanceMusic(_remainingTime);

            return true;
        }

        /// <summary>Stops dance: blends out smoothly by default, or abruptly if immediate is true.</summary>
        public void StopDance(bool immediate = false)
        {
            _remainingTime = 0f;
            AudioManager.Instance?.StopDanceMusic();
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
                if (_remainingTime <= 0f)
                {
                    AudioManager.Instance?.StopDanceMusic();
                }
            }

            // Blend out begins at the final _blendTime seconds to match total duration
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
            AudioManager.Instance?.StopDanceMusic();
            if (_animator != null && _danceLayerIndex >= 0)
            {
                _animator.SetLayerWeight(_danceLayerIndex, 0f);
            }
        }
    }
}
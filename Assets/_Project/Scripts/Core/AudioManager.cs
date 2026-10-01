using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public enum SFXType
{
    // Runner (0-6)
    RunnerJump = 0,
    RunnerLand = 1,
    RunnerLaneSwitch = 2,
    RunnerSlide = 3,
    RunnerKnockback = 4,
    RunnerSpeedBoost = 5,
    BatonHandover = 6,

    // Item & Buff (7-10)
    ShieldActive = 7,
    ShieldBreak = 8,
    ShieldPassive = 9,
    CollectEnergy = 10,

    // Environment & Weather (11)
    EnvironmentRain = 11,

    // Obstacle & Vehicles (12-16)
    CarHorn = 12,
    PickupHorn = 13,
    HeavyTruckHorn = 14,
    HitBarrier = 15,
    HitCarCrash = 16,

    // Livestream & HUD (17-25)
    StreamNewFollower = 17,
    StreamLike = 18,
    StreamDonateGift = 19,
    AntiCarSpawn = 20,
    WarningSiren = 21,
    RelayFinish100m = 22,
    VictoryApplause = 23,
    CountdownTick = 24,
    UIButtonClick = 25,

    // Dance Background Music (26)
    GiftDanceMusic = 26
}

[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
        public static AudioManager Instance { get; private set; }

        [Header("Volume Settings (0.0 to 1.0)")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.55f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.85f;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource loopSfxSource;
        [SerializeField] private AudioSource danceMusicSource;
        [SerializeField] private AudioSource uiSource;
        [SerializeField] private int sfxPoolSize = 10;

        [Header("BGM Playlist")]
        [SerializeField] private List<AudioClip> bgmPlaylist = new List<AudioClip>();

        [Header("SFX Library")]
        [SerializeField] private List<SFXEntry> sfxEntries = new List<SFXEntry>();

        [Header("Anti-Spam / Duplicate Prevention")]
        [Tooltip("Khi bật, nếu cùng một SFX được yêu cầu phát lại liên tiếp dưới minSFXRepeatInterval giây thì sẽ bỏ qua để tránh spam tiếng chói tai.")]
        [SerializeField] private bool preventDuplicateSFX = true;
        [Tooltip("Khoảng cách tối thiểu giữa 2 lần phát cùng một loại SFX (giây). 0.12s vừa chống spam dồn dập trong 1 frame vừa đảm bảo quà tặng liên tiếp vẫn phát ra tiếng.")]
        [SerializeField] private float minSFXRepeatInterval = 0.12f;

        [System.Serializable]
        public struct SFXEntry
        {
            public SFXType type;
            public AudioClip clip;
            [Range(0f, 1.5f)] public float volumeMultiplier;
        }

        private readonly Dictionary<SFXType, SFXEntry> _sfxLookup = new Dictionary<SFXType, SFXEntry>();
        private readonly Dictionary<SFXType, float> _lastPlayTimeByType = new Dictionary<SFXType, float>();
        private readonly Dictionary<AudioClip, float> _lastPlayTimeByClip = new Dictionary<AudioClip, float>();
        private readonly List<AudioSource> _sfxPool = new List<AudioSource>();
        private int _poolIndex;
        private int _lastBgmIndex = -1;
        private Coroutine _bgmCrossfadeRoutine;
        private bool _isDanceMusicActive = false;

        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                UpdateVolumes();
            }
        }

        public float BGMVolume
        {
            get => bgmVolume;
            set
            {
                bgmVolume = Mathf.Clamp01(value);
                UpdateVolumes();
            }
        }

        public float SFXVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                UpdateVolumes();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildLookup();
            SetupAudioSources();
        }

        private void Start()
        {
            if (bgmPlaylist != null && bgmPlaylist.Count > 0)
            {
                PlayRandomBGM();
            }
        }

        private void Update()
        {
            // Nếu đang trong chế độ Gift Dance, không tự động chuyển/phát lại BGM game
            if (_isDanceMusicActive) return;

            // Tự động chuyển bài khi hết nhạc
            if (bgmSource != null && bgmPlaylist != null && bgmPlaylist.Count > 0 && !bgmSource.isPlaying)
            {
                PlayRandomBGM();
            }
        }

        private void BuildLookup()
        {
            _sfxLookup.Clear();
            for (int i = 0; i < sfxEntries.Count; i++)
            {
                var entry = sfxEntries[i];
                if (entry.clip != null && !_sfxLookup.ContainsKey(entry.type))
                {
                    _sfxLookup.Add(entry.type, entry);
                }
            }
        }

        private void SetupAudioSources()
        {
            // 1. BGM Source
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
            }
            bgmSource.loop = false;
            bgmSource.playOnAwake = false;
            bgmSource.spatialBlend = 0f;
            bgmSource.volume = bgmVolume * masterVolume;

            // 2. Loop SFX Source (dung cho Shield Passive, Rain, Siren...)
            if (loopSfxSource == null)
            {
                loopSfxSource = gameObject.AddComponent<AudioSource>();
            }
            loopSfxSource.loop = true;
            loopSfxSource.playOnAwake = false;
            loopSfxSource.spatialBlend = 0f;
            loopSfxSource.volume = sfxVolume * masterVolume;

            // 3. Dance Music Source (nhạc background riêng khi Viewer tặng Gift Dance)
            if (danceMusicSource == null)
            {
                danceMusicSource = gameObject.AddComponent<AudioSource>();
            }
            danceMusicSource.loop = true;
            danceMusicSource.playOnAwake = false;
            danceMusicSource.spatialBlend = 0f;
            danceMusicSource.volume = sfxVolume * masterVolume;

            // 4. UI Source (2D UI clicks)
            if (uiSource == null)
            {
                uiSource = gameObject.AddComponent<AudioSource>();
            }
            uiSource.loop = false;
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
            uiSource.volume = sfxVolume * masterVolume;

            // 5. SFX Pool (tranh cat tieng khi phat nhieu am thanh don dap)
            _sfxPool.Clear();
            for (int i = 0; i < sfxPoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.loop = false;
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _sfxPool.Add(src);
            }
        }

        private void UpdateVolumes()
        {
            if (bgmSource != null) bgmSource.volume = bgmVolume * masterVolume;
            if (loopSfxSource != null) loopSfxSource.volume = sfxVolume * masterVolume;
            if (danceMusicSource != null) danceMusicSource.volume = sfxVolume * masterVolume;
            if (uiSource != null) uiSource.volume = sfxVolume * masterVolume;
        }

        #region SFX Playback & Anti-Spam Control

        /// <summary>
        /// Kiểm tra xem SFXType này có bị throttle do vừa mới phát cách đây dưới minSFXRepeatInterval giây hay không.
        /// </summary>
        public bool IsSFXPlaying(SFXType type)
        {
            return _lastPlayTimeByType.TryGetValue(type, out float lastTime) && (Time.unscaledTime - lastTime) < minSFXRepeatInterval;
        }

        /// <summary>
        /// Kiểm tra xem AudioClip này có bị throttle do vừa mới phát cách đây dưới minSFXRepeatInterval giây hay không.
        /// </summary>
        public bool IsSFXPlaying(AudioClip clip)
        {
            return clip != null && _lastPlayTimeByClip.TryGetValue(clip, out float lastTime) && (Time.unscaledTime - lastTime) < minSFXRepeatInterval;
        }

        /// <summary>
        /// Phát Sound Effect theo SFXType.
        /// Áp dụng khoảng giãn cách an toàn 0.12s để chống spam dồn dập trong 1 frame mà không nuốt tiếng quà tặng.
        /// </summary>
        public void PlaySFX(SFXType type, float volumeMultiplier = 1f, bool randomizePitch = true, bool allowOverlap = false)
        {
            if (preventDuplicateSFX && !allowOverlap && IsSFXPlaying(type))
            {
                // Vừa mới phát cách đây chưa tới 0.12s -> Bỏ qua chống spam
                return;
            }

            if (_sfxLookup.TryGetValue(type, out var entry) && entry.clip != null)
            {
                _lastPlayTimeByType[type] = Time.unscaledTime;
                float finalVol = (entry.volumeMultiplier > 0f ? entry.volumeMultiplier : 1f) * volumeMultiplier;
                PlaySFXClip(entry.clip, finalVol, randomizePitch, allowOverlap);
            }
        }

        /// <summary>
        /// Phát Sound Effect trực tiếp bằng AudioClip.
        /// </summary>
        public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f, bool randomizePitch = true, bool allowOverlap = false)
        {
            if (clip == null) return;
            if (preventDuplicateSFX && !allowOverlap && IsSFXPlaying(clip))
            {
                return;
            }

            PlaySFXClip(clip, volumeMultiplier, randomizePitch, allowOverlap);
        }

        /// <summary>
        /// Phát Sound Effect theo tên chuỗi.
        /// </summary>
        public void PlaySFX(string soundName, float volumeMultiplier = 1f, bool randomizePitch = true, bool allowOverlap = false)
        {
            if (Enum.TryParse<SFXType>(soundName, true, out var type))
            {
                PlaySFX(type, volumeMultiplier, randomizePitch, allowOverlap);
            }
        }

        /// <summary>
        /// Dừng theo dõi và cho phép phát lại một SFXType ngay lập tức.
        /// </summary>
        public void StopSFX(SFXType type)
        {
            _lastPlayTimeByType.Remove(type);
            if (_sfxLookup.TryGetValue(type, out var entry) && entry.clip != null)
            {
                _lastPlayTimeByClip.Remove(entry.clip);
            }
        }

        private float PlaySFXClip(AudioClip clip, float volumeMultiplier, bool randomizePitch, bool allowOverlap = false)
        {
            if (clip == null || _sfxPool.Count == 0) return 0f;

            if (preventDuplicateSFX && !allowOverlap && IsSFXPlaying(clip))
            {
                return 0f;
            }

            _lastPlayTimeByClip[clip] = Time.unscaledTime;

            // Tìm AudioSource đang rảnh trong Pool để tránh ngắt âm thanh khác đang phát
            AudioSource availableSource = null;
            for (int i = 0; i < _sfxPool.Count; i++)
            {
                int idx = (_poolIndex + i) % _sfxPool.Count;
                if (!_sfxPool[idx].isPlaying)
                {
                    availableSource = _sfxPool[idx];
                    _poolIndex = (idx + 1) % _sfxPool.Count;
                    break;
                }
            }

            if (availableSource == null)
            {
                availableSource = _sfxPool[_poolIndex];
                _poolIndex = (_poolIndex + 1) % _sfxPool.Count;
            }

            float pitch = randomizePitch ? Random.Range(0.95f, 1.05f) : 1f;
            availableSource.pitch = pitch;
            availableSource.volume = sfxVolume * masterVolume * Mathf.Clamp01(volumeMultiplier);
            availableSource.PlayOneShot(clip);

            float effectiveDuration = clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            return effectiveDuration;
        }

        public void PlayLoopSFX(SFXType type, float volumeMultiplier = 1f)
        {
            if (_sfxLookup.TryGetValue(type, out var entry) && entry.clip != null && loopSfxSource != null)
            {
                if (loopSfxSource.isPlaying && loopSfxSource.clip == entry.clip) return;
                loopSfxSource.clip = entry.clip;
                loopSfxSource.volume = sfxVolume * masterVolume * volumeMultiplier;
                loopSfxSource.Play();
            }
        }

        public void StopLoopSFX()
        {
            if (loopSfxSource != null && loopSfxSource.isPlaying)
            {
                loopSfxSource.Stop();
                loopSfxSource.clip = null;
            }
        }

        public void PlayUISFX(SFXType type = SFXType.UIButtonClick)
        {
            if (preventDuplicateSFX && IsSFXPlaying(type)) return;

            if (_sfxLookup.TryGetValue(type, out var entry) && entry.clip != null && uiSource != null)
            {
                uiSource.pitch = 1f;
                uiSource.PlayOneShot(entry.clip, sfxVolume * masterVolume);

                _lastPlayTimeByType[type] = Time.unscaledTime;
                _lastPlayTimeByClip[entry.clip] = Time.unscaledTime;
            }
        }

        #endregion

        #region Dance Background Music (Gift Meme Dance)

        private Coroutine _danceMusicRoutine;

        /// <summary>
        /// Bật nhạc background riêng khi Runner nhảy Gift Dance.
        /// Tạm dừng BGM game và chuyển hoàn toàn sang bản nhạc nhảy riêng.
        /// </summary>
        public void StartDanceMusic(float duration = -1f)
        {
            if (_sfxLookup.Count == 0) BuildLookup();

            if (_sfxLookup.TryGetValue(SFXType.GiftDanceMusic, out var entry) && entry.clip != null)
            {
                _isDanceMusicActive = true;
                if (danceMusicSource == null)
                {
                    danceMusicSource = gameObject.AddComponent<AudioSource>();
                    danceMusicSource.loop = true;
                    danceMusicSource.playOnAwake = false;
                    danceMusicSource.spatialBlend = 0f;
                }

                danceMusicSource.clip = entry.clip;
                danceMusicSource.volume = sfxVolume * masterVolume * (entry.volumeMultiplier > 0f ? entry.volumeMultiplier : 1f);
                danceMusicSource.Play();

                // Tạm dừng BGM game hoàn toàn để bản nhạc nhảy nền chiếm trọn không gian âm nhạc
                if (bgmSource != null && bgmSource.isPlaying)
                {
                    bgmSource.Pause();
                }

                if (duration > 0f)
                {
                    if (_danceMusicRoutine != null) StopCoroutine(_danceMusicRoutine);
                    _danceMusicRoutine = StartCoroutine(StopDanceMusicDelayed(duration));
                }
                Debug.Log($"[AudioManager] Bắt đầu phát Dance Background Music ({entry.clip.name}) trong {duration}s.");
            }
            else
            {
                Debug.LogWarning("[AudioManager] Không tìm thấy AudioClip cho SFXType.GiftDanceMusic trong sfxEntries!");
            }
        }

        /// <summary>
        /// Dừng nhạc nhảy và tiếp tục phát lại bài BGM game ban đầu.
        /// </summary>
        public void StopDanceMusic()
        {
            _isDanceMusicActive = false;

            if (_danceMusicRoutine != null)
            {
                StopCoroutine(_danceMusicRoutine);
                _danceMusicRoutine = null;
            }

            if (danceMusicSource != null && danceMusicSource.isPlaying)
            {
                danceMusicSource.Stop();
                danceMusicSource.clip = null;
            }

            // Tiếp tục bài BGM game, nếu đang dừng thì phát bài ngẫu nhiên mới
            if (bgmSource != null)
            {
                bgmSource.UnPause();
                if (!bgmSource.isPlaying && bgmPlaylist != null && bgmPlaylist.Count > 0)
                {
                    PlayRandomBGM();
                }
            }
            Debug.Log("[AudioManager] Dừng Dance Music - Tiếp tục phát BGM game.");
        }

        private IEnumerator StopDanceMusicDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            StopDanceMusic();
        }

        #endregion

        #region BGM Playback

        public void PlayRandomBGM()
        {
            if (bgmPlaylist == null || bgmPlaylist.Count == 0 || bgmSource == null) return;

            int nextIndex;
            if (bgmPlaylist.Count == 1)
            {
                nextIndex = 0;
            }
            else
            {
                do
                {
                    nextIndex = Random.Range(0, bgmPlaylist.Count);
                }
                while (nextIndex == _lastBgmIndex);
            }

            _lastBgmIndex = nextIndex;
            PlayBGMClip(bgmPlaylist[nextIndex]);
        }

        public void PlayBGM(int index)
        {
            if (bgmPlaylist == null || index < 0 || index >= bgmPlaylist.Count) return;
            _lastBgmIndex = index;
            PlayBGMClip(bgmPlaylist[index]);
        }

        private void PlayBGMClip(AudioClip clip)
        {
            if (clip == null || bgmSource == null) return;

            if (gameObject.activeInHierarchy)
            {
                if (_bgmCrossfadeRoutine != null) StopCoroutine(_bgmCrossfadeRoutine);
                _bgmCrossfadeRoutine = StartCoroutine(CrossfadeBGM(clip, 1.0f));
            }
            else
            {
                bgmSource.clip = clip;
                bgmSource.volume = bgmVolume * masterVolume;
                bgmSource.Play();
            }
        }

        private IEnumerator CrossfadeBGM(AudioClip newClip, float duration)
        {
            float targetVol = bgmVolume * masterVolume;
            if (bgmSource.isPlaying)
            {
                float startVol = bgmSource.volume;
                for (float t = 0; t < duration * 0.5f; t += Time.unscaledDeltaTime)
                {
                    bgmSource.volume = Mathf.Lerp(startVol, 0f, t / (duration * 0.5f));
                    yield return null;
                }
            }

            bgmSource.clip = newClip;
            bgmSource.Play();

            for (float t = 0; t < duration * 0.5f; t += Time.unscaledDeltaTime)
            {
                bgmSource.volume = Mathf.Lerp(0f, targetVol, t / (duration * 0.5f));
                yield return null;
            }
            bgmSource.volume = targetVol;
            _bgmCrossfadeRoutine = null;
        }

        public void PauseBGM()
        {
            if (bgmSource != null && bgmSource.isPlaying) bgmSource.Pause();
        }

        public void ResumeBGM()
        {
            if (bgmSource != null && !bgmSource.isPlaying) bgmSource.UnPause();
        }

        public void StopBGM()
        {
            if (_bgmCrossfadeRoutine != null)
            {
                StopCoroutine(_bgmCrossfadeRoutine);
                _bgmCrossfadeRoutine = null;
            }
            if (bgmSource != null) bgmSource.Stop();
        }

        #endregion

        #region Editor Setup Helper

#if UNITY_EDITOR
        public void AutoPopulateAudioClips(List<AudioClip> playlist, List<SFXEntry> entries)
        {
            bgmPlaylist = playlist;
            sfxEntries = entries;
            BuildLookup();
        }
#endif

        #endregion
    }
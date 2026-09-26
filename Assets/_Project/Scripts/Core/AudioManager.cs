using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("SFX")]
    [SerializeField] private AudioSource sfxSource;

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private List<AudioClip> bgmClips = new List<AudioClip>();

    private int lastBgmIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureAudioSources();
    }

    private void EnsureAudioSources()
    {
        if (bgmSource == null)
        {
            var sources = GetComponents<AudioSource>();
            if (sources.Length > 0) bgmSource = sources[0];
            else
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = false;
                bgmSource.playOnAwake = false;
            }
        }

        if (sfxSource == null)
        {
            var sources = GetComponents<AudioSource>();
            if (sources.Length > 1) sfxSource = sources[1];
            else
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
            }
        }
    }

    private void Start()
    {
        PlayRandomBGM();
    }

    private void Update()
    {
        if (bgmClips != null && bgmClips.Count > 0 && bgmSource != null && !bgmSource.isPlaying)
        {
            PlayRandomBGM();
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null)
            return;

        EnsureAudioSources();
        if (sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    private void PlayRandomBGM()
    {
        if (bgmClips == null || bgmClips.Count == 0)
            return;

        EnsureAudioSources();
        if (bgmSource == null)
            return;

        int randomIndex;

        if (bgmClips.Count == 1)
        {
            randomIndex = 0;
        }
        else
        {
            do
            {
                randomIndex = Random.Range(0, bgmClips.Count);
            }
            while (randomIndex == lastBgmIndex);
        }

        lastBgmIndex = randomIndex;
        bgmSource.clip = bgmClips[randomIndex];
        bgmSource.Play();
    }
}
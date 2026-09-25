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
    }

    private void Start()
    {
        PlayRandomBGM();
    }

    private void Update()
    {
        if (!bgmSource.isPlaying && bgmClips.Count > 0)
        {
            PlayRandomBGM();
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null)
            return;

        sfxSource.PlayOneShot(clip);
    }

    private void PlayRandomBGM()
    {
        if (bgmClips.Count == 0)
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
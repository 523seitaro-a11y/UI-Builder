using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[DefaultExecutionOrder(-1000)]
public sealed class AudioManager : MonoBehaviour
{
    private static AudioManager instance;
    private static float sharedBgmVolume = 0.5f;

    public static AudioManager Instance => instance;
    public static float CurrentBgmVolume =>
        instance != null ? instance.bgmVolume : sharedBgmVolume;
    public static event Action<float> BgmVolumeChanged;

    [Header("BGM")]
    [SerializeField] private AudioClip bgmClip;
    [Range(0f, 1f)]
    [SerializeField] private float bgmVolume = 0.5f;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool playOnAwake = true;

    private AudioSource audioSource;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        sharedBgmVolume = bgmVolume;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        ApplyAudioSourceSettings();
    }

    private void Start()
    {
        if (playOnAwake)
        {
            PlayBgm();
        }
    }

    public void PlayBgm()
    {
        if (audioSource == null || bgmClip == null || audioSource.isPlaying)
        {
            return;
        }

        ApplyAudioSourceSettings();
        audioSource.Play();
    }

    public void StopBgm() => audioSource?.Stop();

    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        sharedBgmVolume = bgmVolume;
        if (audioSource != null)
        {
            audioSource.volume = bgmVolume;
        }

        BgmVolumeChanged?.Invoke(bgmVolume);
    }

    private void ApplyAudioSourceSettings()
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.clip = bgmClip;
        audioSource.volume = bgmVolume;
        audioSource.loop = loop;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void OnValidate()
    {
        bgmVolume = Mathf.Clamp01(bgmVolume);
        if (TryGetComponent(out AudioSource source))
        {
            audioSource = source;
            ApplyAudioSourceSettings();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}

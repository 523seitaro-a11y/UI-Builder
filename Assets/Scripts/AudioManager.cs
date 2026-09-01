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

    [Header("効果音")]
    [Tooltip("ブロックのドラッグ中、照準セルが切り替わったときに再生する音です。")]
    [SerializeField] private AudioClip cursorClip;
    [Range(0f, 1f)]
    [SerializeField] private float cursorVolume = 1f;

    [Tooltip("ブロックを有効なセルへ設置したときに再生する音です。")]
    [SerializeField] private AudioClip blockPlacementClip;
    [Range(0f, 1f)]
    [SerializeField] private float blockPlacementVolume = 1f;

    [Tooltip("ブロックをつかんでドラッグを開始したときに再生する音です。")]
    [SerializeField] private AudioClip blockPickupClip;
    [Range(0f, 1f)]
    [SerializeField] private float blockPickupVolume = 1f;

    [Tooltip("ブロックを置けない場所でドロップしたときに再生する音です。")]
    [SerializeField] private AudioClip invalidPlacementClip;
    [Range(0f, 1f)]
    [SerializeField] private float invalidPlacementVolume = 1f;

    [Tooltip("ビルドモード中、ブロックへカーソルが入ったときに再生する音です。")]
    [SerializeField] private AudioClip blockHoverClip;
    [Range(0f, 1f)]
    [SerializeField] private float blockHoverVolume = 1f;

    [Tooltip("上部ブロックパネルが上がるときに再生する音です。")]
    [SerializeField] private AudioClip upperPanelRaiseClip;
    [Range(0f, 1f)]
    [SerializeField] private float upperPanelRaiseVolume = 1f;

    [Tooltip("上部ブロックパネルが下がるときに再生する音です。")]
    [SerializeField] private AudioClip upperPanelLowerClip;
    [Range(0f, 1f)]
    [SerializeField] private float upperPanelLowerVolume = 1f;

    [Tooltip("ゲームがビルドモードからプレイモードへ切り替わったときに再生する音です。")]
    [SerializeField] private AudioClip gameStartClip;
    [Range(0f, 1f)]
    [SerializeField] private float gameStartVolume = 1f;

    [Tooltip("プレイモード中、接地したプレイヤーがジャンプしたときに再生する音です。")]
    [SerializeField] private AudioClip jumpClip;
    [Range(0f, 1f)]
    [SerializeField] private float jumpVolume = 1f;

    [Tooltip("プレイヤーが死亡した瞬間に再生する音です。")]
    [SerializeField] private AudioClip deathClip;
    [Range(0f, 1f)]
    [SerializeField] private float deathVolume = 1f;

    [Tooltip("プレイヤーが鍵を取得した瞬間に再生する音です。")]
    [SerializeField] private AudioClip keyCollectClip;
    [Range(0f, 1f)]
    [SerializeField] private float keyCollectVolume = 1f;

    [Tooltip("鍵取得演出の完了後、ゴールのロックが解除されたときに再生する音です。")]
    [SerializeField] private AudioClip goalUnlockClip;
    [Range(0f, 1f)]
    [SerializeField] private float goalUnlockVolume = 1f;

    [Tooltip("ゴール後、ゴールのスプライトがクリア表示へ変わる瞬間に再生する音です。")]
    [SerializeField] private AudioClip goalSpriteChangeClip;
    [Range(0f, 1f)]
    [SerializeField] private float goalSpriteChangeVolume = 1f;

    [Tooltip("ステージクリアが確定し、リザルトへ移行するときに再生する音です。")]
    [SerializeField] private AudioClip stageClearClip;
    [Range(0f, 1f)]
    [SerializeField] private float stageClearVolume = 1f;

    private AudioSource audioSource;
    private AudioSource cursorAudioSource;

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

        cursorAudioSource = gameObject.AddComponent<AudioSource>();
        cursorAudioSource.playOnAwake = false;
        cursorAudioSource.loop = false;
        cursorAudioSource.spatialBlend = 0f;
        cursorAudioSource.volume = 1f;
        cursorAudioSource.priority = 0;
        ApplyAudioSourceSettings();

        if (deathClip != null && deathClip.loadState == AudioDataLoadState.Unloaded)
        {
            deathClip.LoadAudioData();
        }
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

    public void PlayCursorSound()
    {
        AudioClip clipToPlay = cursorClip;
#if UNITY_WEBGL && !UNITY_EDITOR
        // click_003.ogg is only about 10 ms long. WebGL audio conversion can
        // consume such a short transient, so use the audible hover WAV there.
        clipToPlay = blockHoverClip != null ? blockHoverClip : cursorClip;
#endif

        if (cursorAudioSource == null || clipToPlay == null)
        {
            return;
        }

        cursorAudioSource.PlayOneShot(clipToPlay, cursorVolume);
    }

    public void PlayBlockPlacementSound()
    {
        if (cursorAudioSource == null || blockPlacementClip == null)
        {
            return;
        }

        cursorAudioSource.PlayOneShot(blockPlacementClip, blockPlacementVolume);
    }

    public void PlayBlockPickupSound()
    {
        if (cursorAudioSource == null || blockPickupClip == null)
        {
            return;
        }

        cursorAudioSource.PlayOneShot(blockPickupClip, blockPickupVolume);
    }

    public void PlayInvalidPlacementSound()
    {
        if (cursorAudioSource == null || invalidPlacementClip == null)
        {
            return;
        }

        cursorAudioSource.PlayOneShot(invalidPlacementClip, invalidPlacementVolume);
    }

    public void PlayBlockHoverSound()
    {
        if (cursorAudioSource == null || blockHoverClip == null)
        {
            return;
        }

        cursorAudioSource.PlayOneShot(blockHoverClip, blockHoverVolume);
    }

    public void PlayUpperPanelRaiseSound()
    {
        if (cursorAudioSource == null || upperPanelRaiseClip == null)
        {
            return;
        }

        cursorAudioSource.PlayOneShot(upperPanelRaiseClip, upperPanelRaiseVolume);
    }

    public void PlayUpperPanelLowerSound()
    {
        if (cursorAudioSource == null || upperPanelLowerClip == null)
        {
            return;
        }

        cursorAudioSource.PlayOneShot(upperPanelLowerClip, upperPanelLowerVolume);
    }

    public void PlayGameStartSound()
    {
        if (cursorAudioSource == null || gameStartClip == null)
        {
            return;
        }

        cursorAudioSource.PlayOneShot(gameStartClip, gameStartVolume);
    }

    public void PlayJumpSound()
    {
        if (cursorAudioSource == null || jumpClip == null)
        {
            return;
        }

        cursorAudioSource.PlayOneShot(jumpClip, jumpVolume);
    }

    public void PlayDeathSound()
    {
        if (cursorAudioSource == null || deathClip == null)
        {
            return;
        }

        cursorAudioSource.PlayOneShot(deathClip, deathVolume);
    }

    public void PlayKeyCollectSound()
    {
        if (cursorAudioSource == null || keyCollectClip == null)
        {
            return;
        }

        cursorAudioSource.PlayOneShot(keyCollectClip, keyCollectVolume);
    }

    public void PlayGoalUnlockSound()
    {
        if (cursorAudioSource == null || goalUnlockClip == null)
        {
            return;
        }

        cursorAudioSource.PlayOneShot(goalUnlockClip, goalUnlockVolume);
    }

    public void PlayGoalSpriteChangeSound()
    {
        if (cursorAudioSource == null || goalSpriteChangeClip == null)
        {
            return;
        }

        cursorAudioSource.PlayOneShot(goalSpriteChangeClip, goalSpriteChangeVolume);
    }

    public void PlayStageClearSound()
    {
        if (cursorAudioSource == null || stageClearClip == null)
        {
            return;
        }

        cursorAudioSource.PlayOneShot(stageClearClip, stageClearVolume);
    }

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
        cursorVolume = Mathf.Clamp01(cursorVolume);
        blockPlacementVolume = Mathf.Clamp01(blockPlacementVolume);
        blockPickupVolume = Mathf.Clamp01(blockPickupVolume);
        invalidPlacementVolume = Mathf.Clamp01(invalidPlacementVolume);
        blockHoverVolume = Mathf.Clamp01(blockHoverVolume);
        upperPanelRaiseVolume = Mathf.Clamp01(upperPanelRaiseVolume);
        upperPanelLowerVolume = Mathf.Clamp01(upperPanelLowerVolume);
        gameStartVolume = Mathf.Clamp01(gameStartVolume);
        jumpVolume = Mathf.Clamp01(jumpVolume);
        deathVolume = Mathf.Clamp01(deathVolume);
        keyCollectVolume = Mathf.Clamp01(keyCollectVolume);
        goalUnlockVolume = Mathf.Clamp01(goalUnlockVolume);
        goalSpriteChangeVolume = Mathf.Clamp01(goalSpriteChangeVolume);
        stageClearVolume = Mathf.Clamp01(stageClearVolume);
        if (TryGetComponent(out AudioSource source))
        {
            audioSource = source;
        ApplyAudioSourceSettings();

        if (deathClip != null && deathClip.loadState == AudioDataLoadState.Unloaded)
        {
            deathClip.LoadAudioData();
        }
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

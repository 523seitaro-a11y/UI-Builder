using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[DefaultExecutionOrder(-1000)]
public sealed class AudioManager : MonoBehaviour
{
    private static AudioManager instance;
    private static float sharedBgmVolume = 0.5f;
    private static float sharedSeVolume = 0.5f;

    public static AudioManager Instance => instance;
    public static float CurrentBgmVolume =>
        instance != null ? instance.bgmVolume : sharedBgmVolume;
    public static float CurrentSeVolume =>
        instance != null ? instance.seVolume : sharedSeVolume;
    public static event Action<float> BgmVolumeChanged;
    public static event Action<float> SeVolumeChanged;

    [Header("BGM")]
    [SerializeField] private AudioClip bgmClip;
    [Range(0f, 1f)]
    [SerializeField] private float bgmVolume = 0.5f;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool playOnAwake = true;

    [Header("効果音")]
    [Range(0f, 1f)]
    [SerializeField] private float seVolume = 0.5f;

    [Tooltip("SE音量バーが1段階動くたびに再生する確認音です。未設定時はブロックHover音を使用します。")]
    [SerializeField] private AudioClip seVolumeStepClip;
    [Range(0f, 1f)]
    [SerializeField] private float seVolumeStepVolume = 1f;

    [Header("UI操作音")]
    [Tooltip("Saveブロックでプレイヤー状態を保存したときに再生する音です。")]
    [SerializeField] private AudioClip saveClip;
    [Range(0f, 1f)]
    [SerializeField] private float saveVolume = 1f;

    [Tooltip("Saveブロックで保存済みのプレイヤー状態をロードしたときに再生する音です。")]
    [SerializeField] private AudioClip loadClip;
    [Range(0f, 1f)]
    [SerializeField] private float loadVolume = 1f;

    [Tooltip("Stopブロックでプレイヤーを停止したときに再生する音です。")]
    [SerializeField] private AudioClip stopClip;
    [Range(0f, 1f)]
    [SerializeField] private float stopVolume = 1f;

    [Tooltip("Stopブロックでプレイヤーの停止を解除したときに再生する音です。")]
    [SerializeField] private AudioClip stopReleaseClip;
    [Range(0f, 1f)]
    [SerializeField] private float stopReleaseVolume = 1f;

    [Tooltip("Sizeブロックで%サイズを選択したときに再生する音です。")]
    [SerializeField] private AudioClip sizeSelectionClip;
    [Range(0f, 1f)]
    [SerializeField] private float sizeSelectionVolume = 1f;

    [Tooltip("Sizeブロックの選択肢を展開したときに再生する音です。")]
    [SerializeField] private AudioClip sizeOpenClip;
    [Range(0f, 1f)]
    [SerializeField] private float sizeOpenVolume = 1f;

    [Tooltip("Sizeブロックの選択肢を閉じたときに再生する音です。")]
    [SerializeField] private AudioClip sizeCloseClip;
    [Range(0f, 1f)]
    [SerializeField] private float sizeCloseVolume = 1f;

    [Tooltip("専用SEが再生されなかった通常のUIボタン押下時に再生する音です。")]
    [SerializeField] private AudioClip defaultButtonClip;
    [Range(0f, 1f)]
    [SerializeField] private float defaultButtonVolume = 1f;

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
    private int lastSePlaybackFrame = -1;
    private int defaultButtonScheduledFrame = -1;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        sharedBgmVolume = bgmVolume;
        sharedSeVolume = seVolume;
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
        cursorAudioSource.volume = seVolume;
        cursorAudioSource.priority = 0;
        ApplyAudioSourceSettings();
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (deathClip != null && deathClip.loadState == AudioDataLoadState.Unloaded)
        {
            deathClip.LoadAudioData();
        }
    }

    private void Start()
    {
        RegisterDefaultButtonSounds();
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

        PlaySe(clipToPlay, cursorVolume);
    }

    public void PlayBlockPlacementSound()
    {
        if (cursorAudioSource == null || blockPlacementClip == null)
        {
            return;
        }

        PlaySe(blockPlacementClip, blockPlacementVolume);
    }

    public void PlayBlockPickupSound()
    {
        if (cursorAudioSource == null || blockPickupClip == null)
        {
            return;
        }

        PlaySe(blockPickupClip, blockPickupVolume);
    }

    public void PlayInvalidPlacementSound()
    {
        if (cursorAudioSource == null || invalidPlacementClip == null)
        {
            return;
        }

        PlaySe(invalidPlacementClip, invalidPlacementVolume);
    }

    public void PlayBlockHoverSound()
    {
        if (cursorAudioSource == null || blockHoverClip == null)
        {
            return;
        }

        PlaySe(blockHoverClip, blockHoverVolume);
    }

    public void PlayUpperPanelRaiseSound()
    {
        if (cursorAudioSource == null || upperPanelRaiseClip == null)
        {
            return;
        }

        PlaySe(upperPanelRaiseClip, upperPanelRaiseVolume);
    }

    public void PlayUpperPanelLowerSound()
    {
        if (cursorAudioSource == null || upperPanelLowerClip == null)
        {
            return;
        }

        PlaySe(upperPanelLowerClip, upperPanelLowerVolume);
    }

    public void PlayGameStartSound()
    {
        if (cursorAudioSource == null || gameStartClip == null)
        {
            return;
        }

        PlaySe(gameStartClip, gameStartVolume);
    }

    public void PlayJumpSound()
    {
        if (cursorAudioSource == null || jumpClip == null)
        {
            return;
        }

        PlaySe(jumpClip, jumpVolume);
    }

    public void PlayDeathSound()
    {
        if (cursorAudioSource == null || deathClip == null)
        {
            return;
        }

        PlaySe(deathClip, deathVolume);
    }

    public void PlayKeyCollectSound()
    {
        if (cursorAudioSource == null || keyCollectClip == null)
        {
            return;
        }

        PlaySe(keyCollectClip, keyCollectVolume);
    }

    public void PlayGoalUnlockSound()
    {
        if (cursorAudioSource == null || goalUnlockClip == null)
        {
            return;
        }

        PlaySe(goalUnlockClip, goalUnlockVolume);
    }

    public void PlayGoalSpriteChangeSound()
    {
        if (cursorAudioSource == null || goalSpriteChangeClip == null)
        {
            return;
        }

        PlaySe(goalSpriteChangeClip, goalSpriteChangeVolume);
    }

    public void PlayStageClearSound()
    {
        if (cursorAudioSource == null || stageClearClip == null)
        {
            return;
        }

        PlaySe(stageClearClip, stageClearVolume);
    }

    public void PlaySeVolumeStepSound()
    {
        AudioClip clip = seVolumeStepClip != null ? seVolumeStepClip : blockHoverClip;
        if (cursorAudioSource == null || clip == null)
        {
            return;
        }

        PlaySe(clip, seVolumeStepVolume);
    }

    public void PlaySaveSound() => PlaySe(saveClip, saveVolume);

    public void PlayLoadSound() => PlaySe(loadClip, loadVolume);

    public void PlayStopSound() => PlaySe(stopClip, stopVolume);

    public void PlayStopReleaseSound() => PlaySe(stopReleaseClip, stopReleaseVolume);

    public void PlaySizeSelectionSound() => PlaySe(sizeSelectionClip, sizeSelectionVolume);

    public void PlaySizeOpenSound() => PlaySe(sizeOpenClip, sizeOpenVolume);

    public void PlaySizeCloseSound() => PlaySe(sizeCloseClip, sizeCloseVolume);

    private void PlaySe(AudioClip clip, float volume)
    {
        if (cursorAudioSource == null || clip == null)
        {
            return;
        }

        lastSePlaybackFrame = Time.frameCount;
        cursorAudioSource.PlayOneShot(clip, volume);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => RegisterDefaultButtonSounds();

    private void RegisterDefaultButtonSounds()
    {
        foreach (Button button in FindObjectsByType<Button>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            button.onClick.RemoveListener(ScheduleDefaultButtonSound);
            button.onClick.AddListener(ScheduleDefaultButtonSound);
        }
    }

    private void ScheduleDefaultButtonSound()
    {
        int clickedFrame = Time.frameCount;
        if (defaultButtonScheduledFrame == clickedFrame)
        {
            return;
        }

        defaultButtonScheduledFrame = clickedFrame;
        StartCoroutine(PlayDefaultButtonSoundAfterDedicatedSe(clickedFrame));
    }

    private IEnumerator PlayDefaultButtonSoundAfterDedicatedSe(int clickedFrame)
    {
        yield return null;
        if (lastSePlaybackFrame != clickedFrame)
        {
            PlaySe(defaultButtonClip, defaultButtonVolume);
        }
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

    public void SetSeVolume(float volume)
    {
        seVolume = Mathf.Clamp01(volume);
        sharedSeVolume = seVolume;
        if (cursorAudioSource != null)
        {
            cursorAudioSource.volume = seVolume;
        }

        SeVolumeChanged?.Invoke(seVolume);
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
        seVolume = Mathf.Clamp01(seVolume);
        seVolumeStepVolume = Mathf.Clamp01(seVolumeStepVolume);
        saveVolume = Mathf.Clamp01(saveVolume);
        loadVolume = Mathf.Clamp01(loadVolume);
        stopVolume = Mathf.Clamp01(stopVolume);
        stopReleaseVolume = Mathf.Clamp01(stopReleaseVolume);
        sizeSelectionVolume = Mathf.Clamp01(sizeSelectionVolume);
        sizeOpenVolume = Mathf.Clamp01(sizeOpenVolume);
        sizeCloseVolume = Mathf.Clamp01(sizeCloseVolume);
        defaultButtonVolume = Mathf.Clamp01(defaultButtonVolume);
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
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            instance = null;
        }
    }
}

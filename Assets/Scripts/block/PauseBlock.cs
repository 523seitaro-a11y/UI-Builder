using UnityEngine;

/// <summary>
/// クリックするたびにプレイヤーの物理停止と再開を切り替えます。
/// </summary>
public sealed class PauseBlock : MonoBehaviour,
    BlockManager.IBlockOperationState,
    BlockManager.IPlayModeBlockState,
    BlockManager.IBlockClickState
{
    [SerializeField] private BlockManager blockManager;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite stopSprite;
    [SerializeField] private Sprite playSprite;

    private int lastOperationFrame = -1;

    public bool IsOperating => false;

    public void Configure(
        BlockManager manager,
        SpriteRenderer renderer,
        Sprite stoppedStateSprite,
        Sprite playingStateSprite)
    {
        blockManager = manager;
        targetRenderer = renderer;
        stopSprite = stoppedStateSprite;
        playSprite = playingStateSprite;
        ApplySprite();
    }

    private void Awake()
    {
        if (blockManager == null)
        {
            blockManager = FindFirstObjectByType<BlockManager>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (stopSprite == null && targetRenderer != null)
        {
            stopSprite = targetRenderer.sprite;
        }

        ApplySprite();
    }

    private void Update() => ApplySprite();

    public void Click() => BeginOperation();

    public void BeginOperation()
    {
        if (!BlockInputGuard.AllowsInput(this))
        {
            return;
        }

        if (!isActiveAndEnabled || blockManager == null || blockManager.IsBuildMode)
        {
            return;
        }

        if (lastOperationFrame == Time.frameCount)
        {
            return;
        }

        lastOperationFrame = Time.frameCount;
        bool shouldStop = !blockManager.IsPlayerStopped;
        blockManager.SetPlayerStopped(shouldStop);
        if (shouldStop)
        {
            AudioManager.Instance?.PlayStopSound();
        }
        else
        {
            AudioManager.Instance?.PlayStopReleaseSound();
        }
        ApplySprite();
    }

    // クリックで状態を切り替えるブロックなので、ボタンを離しても状態を維持します。
    public void CancelOperation()
    {
    }

    public void OnPlayModeEntered() => ApplySprite();

    public void OnBuildModeEntered() => ApplySprite();

    private void ApplySprite()
    {
        if (targetRenderer == null)
        {
            return;
        }

        bool isStopped = blockManager != null && !blockManager.IsBuildMode &&
                         blockManager.IsPlayerStopped;
        Sprite sprite = isStopped ? playSprite : stopSprite;
        if (sprite != null && targetRenderer.sprite != sprite)
        {
            targetRenderer.sprite = sprite;
        }
    }
}

using UnityEngine;

/// <summary>
/// クリックするたびにプレイヤーの物理停止と再開を切り替えます。
/// </summary>
public sealed class PauseBlock : MonoBehaviour,
    BlockManager.IBlockOperationState,
    BlockManager.IPlayModeBlockState
{
    [SerializeField] private BlockManager blockManager;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite stopSprite;
    [SerializeField] private Sprite playSprite;

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

    private void OnMouseDown() => BeginOperation();

    public void BeginOperation()
    {
        if (blockManager == null)
        {
            return;
        }

        blockManager.SetPlayerStopped(!blockManager.IsPlayerStopped);
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

        bool isStopped = blockManager != null && blockManager.IsPlayerStopped;
        Sprite sprite = isStopped ? playSprite : stopSprite;
        if (sprite != null && targetRenderer.sprite != sprite)
        {
            targetRenderer.sprite = sprite;
        }
    }
}

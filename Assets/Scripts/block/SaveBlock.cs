using UnityEngine;

/// <summary>
/// プレイヤーの物理状態とアニメーション状態を保存し、次のクリックで復元します。
/// </summary>
public sealed class SaveBlock : MonoBehaviour,
    BlockManager.IBlockOperationState,
    BlockManager.IPlayModeBlockState,
    BlockManager.IBlockClickState
{
    [SerializeField] private Rigidbody2D playerBody;
    [SerializeField] private SpriteRenderer playerRenderer;
    [SerializeField] private Player playerAnimation;
    [SerializeField] private SpriteRenderer blockRenderer;
    [SerializeField] private Sprite saveSprite;
    [SerializeField] private Sprite loadSprite;
    [SerializeField] private Sprite compactSaveSprite;
    [SerializeField] private Sprite compactLoadSprite;
    [SerializeField] private BigUiBlock bigUiBlock;
    [Range(0f, 1f)] [SerializeField] private float ghostOpacity = 0.4f;
    [Min(0f)] [SerializeField] private float ghostOverlapHideDistance = 0.05f;

    private bool hasSavedState;
    private Vector2 savedPosition;
    private float savedRotation;
    private Vector2 savedVelocity;
    private float savedAngularVelocity;
    private Sprite savedPlayerSprite;
    private bool savedFlipX;
    private bool savedFlipY;
    private Player.AnimationSnapshot savedAnimation;
    private GameObject playerGhost;
    private int lastOperationFrame = -1;

    public bool IsOperating => false;

    public void Configure(
        Rigidbody2D targetBody,
        SpriteRenderer targetPlayerRenderer,
        Player targetAnimation,
        SpriteRenderer targetBlockRenderer,
        Sprite saveStateSprite,
        Sprite loadStateSprite,
        Sprite compactSaveStateSprite = null,
        Sprite compactLoadStateSprite = null)
    {
        playerBody = targetBody;
        playerRenderer = targetPlayerRenderer;
        playerAnimation = targetAnimation;
        blockRenderer = targetBlockRenderer;
        saveSprite = saveStateSprite;
        loadSprite = loadStateSprite;
        compactSaveSprite = compactSaveStateSprite;
        compactLoadSprite = compactLoadStateSprite;
        ApplyBlockSprite();
    }

    private void Awake()
    {
        if (playerBody == null)
        {
            GameObject playerObject = GameObject.Find("Player");
            playerBody = playerObject != null ? playerObject.GetComponent<Rigidbody2D>() : null;
        }

        if (playerBody != null)
        {
            playerRenderer ??= playerBody.GetComponent<SpriteRenderer>();
            playerAnimation ??= playerBody.GetComponent<Player>();
        }

        blockRenderer ??= GetComponentInChildren<SpriteRenderer>(true);
        bigUiBlock ??= GetComponent<BigUiBlock>();
        saveSprite ??= blockRenderer != null ? blockRenderer.sprite : null;
        ApplyBlockSprite();
    }

    private void OnMouseDown() => BeginOperation();

    public void Click() => BeginOperation();

    private void LateUpdate()
    {
        if (!hasSavedState)
        {
            return;
        }

        if (playerGhost == null)
        {
            CreatePlayerGhost();
        }
        else if (!playerGhost.activeSelf)
        {
            playerGhost.SetActive(true);
        }

        EnsureGhostBehindPlayer();
    }

    public void BeginOperation()
    {
        if (lastOperationFrame == Time.frameCount)
        {
            return;
        }

        lastOperationFrame = Time.frameCount;
        if (playerBody == null || !playerBody.simulated)
        {
            return;
        }

        if (hasSavedState)
        {
            LoadPlayerState();
            ClearSavedState();
        }
        else
        {
            SavePlayerState();
        }

        ApplyBlockSprite();
    }

    public void CancelOperation()
    {
    }

    public void OnPlayModeEntered()
    {
        ResetSavedState();
    }

    public void OnBuildModeEntered()
    {
        ResetSavedState();
    }

    /// <summary>保存内容と残像を破棄し、ブロックをSave表示に戻します。</summary>
    public void ResetSavedState()
    {
        ClearSavedState();
        ApplyBlockSprite();
    }

    private void SavePlayerState()
    {
        ResolvePlayerVisualReferences();
        savedPosition = playerBody.position;
        savedRotation = playerBody.rotation;
        savedVelocity = playerBody.linearVelocity;
        savedAngularVelocity = playerBody.angularVelocity;
        savedPlayerSprite = playerRenderer != null ? playerRenderer.sprite : null;
        savedFlipX = playerRenderer != null && playerRenderer.flipX;
        savedFlipY = playerRenderer != null && playerRenderer.flipY;
        if (playerAnimation != null)
        {
            savedAnimation = playerAnimation.CaptureAnimationState();
        }

        hasSavedState = true;
        CreatePlayerGhost();
    }

    private void LoadPlayerState()
    {
        playerBody.position = savedPosition;
        playerBody.rotation = savedRotation;
        playerBody.linearVelocity = savedVelocity;
        playerBody.angularVelocity = savedAngularVelocity;

        if (playerAnimation != null)
        {
            playerAnimation.RestoreAnimationState(savedAnimation);
        }
        else if (playerRenderer != null)
        {
            playerRenderer.sprite = savedPlayerSprite;
            playerRenderer.flipX = savedFlipX;
            playerRenderer.flipY = savedFlipY;
        }

        playerBody.WakeUp();
        Physics2D.SyncTransforms();
    }

    private void CreatePlayerGhost()
    {
        DestroyPlayerGhost();

        // シーン切り替えや実行順によってBlockManagerから渡された描画参照が
        // 未設定でも、保存済みのRigidbodyから表示中のPlayerを取り直します。
        ResolvePlayerVisualReferences();

        // 描画参照やSpriteの有無にかかわらず先にGameObjectを生成します。
        // これにより保存成立時はHierarchy上にも必ず残像が存在します。
        playerGhost = new GameObject("Saved Player Ghost");
        playerGhost.layer = playerRenderer != null
            ? playerRenderer.gameObject.layer
            : playerBody.gameObject.layer;
        playerGhost.SetActive(false);

        Transform ghostTransform = playerGhost.transform;
        Vector3 ghostPosition = playerRenderer != null
            ? playerRenderer.transform.position
            : playerBody.transform.position;
        ghostPosition.x = savedPosition.x;
        ghostPosition.y = savedPosition.y;
        ghostTransform.position = ghostPosition;
        ghostTransform.rotation = playerRenderer != null
            ? playerRenderer.transform.rotation
            : playerBody.transform.rotation;
        ghostTransform.localScale = playerRenderer != null
            ? playerRenderer.transform.lossyScale
            : playerBody.transform.lossyScale;

        SpriteRenderer ghostRenderer = playerGhost.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = savedPlayerSprite != null
            ? savedPlayerSprite
            : playerRenderer != null
                ? playerRenderer.sprite
                : savedAnimation.Sprite;
        if (playerRenderer != null)
        {
            // 実際にPlayerを描画できているMaterialを使い、Shader不一致を避けます。
            ghostRenderer.sharedMaterial = playerRenderer.sharedMaterial;
        }

        Color ghostColor = playerRenderer != null ? playerRenderer.color : Color.white;
        ghostColor.a = ghostOpacity;
        ghostRenderer.color = ghostColor;
        ghostRenderer.flipX = savedFlipX;
        ghostRenderer.flipY = savedFlipY;
        if (playerRenderer != null)
        {
            ghostRenderer.drawMode = playerRenderer.drawMode;
            ghostRenderer.size = playerRenderer.size;
            ghostRenderer.maskInteraction = playerRenderer.maskInteraction;
        }
        playerGhost.SetActive(true);
        EnsureGhostBehindPlayer();
    }

    private void EnsureGhostBehindPlayer()
    {
        if (playerGhost == null || playerRenderer == null)
        {
            return;
        }

        SpriteRenderer ghostRenderer = playerGhost.GetComponent<SpriteRenderer>();
        if (ghostRenderer == null)
        {
            return;
        }

        ghostRenderer.sortingLayerID = playerRenderer.sortingLayerID;
        ghostRenderer.sortingOrder = Mathf.Max(
            short.MinValue,
            playerRenderer.sortingOrder - 100);

        // Idle0/Idle1などで輪郭や透明部分が異なる場合、背面の残像が透けて
        // 手前にあるように見えるため、Playerが保存地点に重なる間は隠します。
        bool playerOverlapsSavedPosition = playerBody != null &&
            ((Vector2)playerBody.position - savedPosition).sqrMagnitude <=
            ghostOverlapHideDistance * ghostOverlapHideDistance;
        ghostRenderer.enabled = !playerOverlapsSavedPosition;

        // 同一Sorting Layerを使用する場合にもPlayerが確実に手前になるよう、
        // 通常の2Dカメラから遠い側へ残像を配置します。
        Vector3 ghostPosition = playerGhost.transform.position;
        ghostPosition.z = playerRenderer.transform.position.z + 0.1f;
        playerGhost.transform.position = ghostPosition;
    }

    private void ResolvePlayerVisualReferences()
    {
        if (playerBody == null)
        {
            Player scenePlayer = FindFirstObjectByType<Player>();
            playerBody = scenePlayer != null ? scenePlayer.GetComponent<Rigidbody2D>() : null;
        }

        if (playerBody == null)
        {
            return;
        }

        playerAnimation ??= playerBody.GetComponent<Player>();
        playerRenderer ??= playerBody.GetComponent<SpriteRenderer>();
        playerRenderer ??= playerBody.GetComponentInChildren<SpriteRenderer>(true);
    }

    private void ClearSavedState()
    {
        hasSavedState = false;
        savedPosition = Vector2.zero;
        savedRotation = 0f;
        savedVelocity = Vector2.zero;
        savedAngularVelocity = 0f;
        savedPlayerSprite = null;
        savedFlipX = false;
        savedFlipY = false;
        savedAnimation = default;
        DestroyPlayerGhost();
    }

    private void DestroyPlayerGhost()
    {
        if (playerGhost != null)
        {
            playerGhost.SetActive(false);
            Destroy(playerGhost);
            playerGhost = null;
        }
    }

    private void ApplyBlockSprite()
    {
        if (blockRenderer == null)
        {
            return;
        }

        Sprite sprite = hasSavedState ? loadSprite : saveSprite;
        bigUiBlock ??= GetComponent<BigUiBlock>();
        if (bigUiBlock != null)
        {
            Sprite compactSprite = hasSavedState ? compactLoadSprite : compactSaveSprite;
            bigUiBlock.SetStateSprites(sprite, compactSprite);
            return;
        }

        if (sprite != null)
        {
            blockRenderer.sprite = sprite;
        }
    }

    private void OnDisable()
    {
        ResetSavedState();
    }
}

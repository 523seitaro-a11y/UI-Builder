using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class StageManager : MonoBehaviour
{
    public enum StageMode
    {
        Build,
        Play,
        Result
    }

    [Header("プレイヤー開始位置")]
    [Tooltip("開始位置へ移動するプレイヤーのTransformです。")]
    [SerializeField] private Transform player;

    [Tooltip("開始位置の基準にするTilemapです。")]
    [SerializeField] private Tilemap stageTilemap;

    [Tooltip("プレイヤーを開始させるTilemapセル座標です。")]
    [SerializeField] private Vector2Int playerStartCell = new Vector2Int(-7, -3);

    [Tooltip("開始セル中央からプレイヤー位置へ加える補正値です。")]
    [SerializeField] private Vector2 playerStartOffset;

    [Tooltip("開始位置としてブロック配置を禁止するセル数です。Xが横、Yが縦です。")]
    [SerializeField] private Vector2Int reservedCellSize = Vector2Int.one;

    [Header("ゴール位置")]
    [Tooltip("設定した位置へ移動するゴールのTransformです。")]
    [SerializeField] private Transform goal;

    [Tooltip("ゴールを配置するTilemapセル座標です。")]
    [SerializeField] private Vector2Int goalCell = new Vector2Int(5, 0);

    [Tooltip("ゴールセル中央から実際の位置へ加える補正値です。")]
    [SerializeField] private Vector2 goalOffset;

    [Tooltip("ゴール位置としてブロック配置を禁止するセル数です。Xが横、Yが縦です。")]
    [SerializeField] private Vector2Int goalReservedCellSize = Vector2Int.one;

    [Header("ゴール判定とリザルト表示")]
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private Collider2D goalCollider;

    [Tooltip("画面下から表示するResultBGです。")]
    [SerializeField] private RectTransform resultBackground;

    [Tooltip("ゴールに触れてからResultBGを表示し始めるまでの待機時間（秒）です。")]
    [Min(0f)]
    [SerializeField] private float resultDisplayDelay = 0.5f;

    [Tooltip("ResultBGが表示されたときの座標です。")]
    [SerializeField] private Vector2 resultShownPosition = Vector2.zero;

    [Min(0f)]
    [SerializeField] private float resultMoveDuration = 0.35f;

    [SerializeField] private Ease resultMoveEase = Ease.OutCubic;
    [SerializeField] private bool useUnscaledResultTime = true;

    [Header("モード管理")]
    [SerializeField] private BlockManager blockManager;
    [SerializeField] private CanvasManager canvasManager;
    [SerializeField] private Rigidbody2D playerBody;
    [SerializeField] private GameObject blockBackground;
    [SerializeField] private GameObject gridBackground;
    [SerializeField] private Button gameStartButton;

    [Header("落下リトライ")]
    [Tooltip("プレイ中にプレイヤーのY座標がこの値以下になると、開始位置からリトライします。")]
    [SerializeField] private float retryHeight = -7f;

    [Header("ゲーム開始時の画面遷移")]
    [Tooltip("画面全体を覆う黒いUIです。")]
    [SerializeField] private RectTransform transitionOverlay;

    [Tooltip("画面遷移に使用するImageです。")]
    [SerializeField] private Image transitionImage;

    [Tooltip("画面を覆う色です。透明度も調整できます。")]
    [SerializeField] private Color transitionColor = Color.black;

    [Min(0f)]
    [SerializeField] private float coverDuration = 0.25f;

    [Tooltip("画面全体を遷移色で覆ったまま待つ時間です。")]
    [Min(0f)]
    [SerializeField] private float fullCoverDuration = 0.15f;

    [Min(0f)]
    [SerializeField] private float revealDuration = 0.25f;

    [SerializeField] private Ease transitionEase = Ease.InOutCubic;
    [SerializeField] private bool useUnscaledTransitionTime = true;

    public Vector2Int PlayerStartCell => playerStartCell;
    public Vector2Int GoalCell => goalCell;
    public StageMode CurrentMode { get; private set; }

    private Tweener transitionTween;
    private Tweener resultTween;
    private Vector2 resultHiddenPosition;
    private bool isRetryTransitionPlaying;

    private void Awake()
    {
        FindMissingReferences();
        ResetTransitionOverlay();
        ResetResultBackground();
        EnterBuildMode();
    }

    private void Start() => RunStageAsync(this.GetCancellationTokenOnDestroy()).Forget();

    private async UniTaskVoid RunStageAsync(CancellationToken token)
    {
        if (blockManager == null || gameStartButton == null)
        {
            Debug.LogWarning("StageManager: 必要な参照がありません。", this);
            return;
        }

        while (!token.IsCancellationRequested)
        {
            bool canceled = await UniTask.WaitUntil(
                    () => !isRetryTransitionPlaying,
                    cancellationToken: token)
                .SuppressCancellationThrow();
            if (canceled)
            {
                return;
            }

            canceled = await UniTask.WaitUntil(
                    AreRequiredBlocksPlaced,
                    cancellationToken: token)
                .SuppressCancellationThrow();
            if (canceled)
            {
                return;
            }

            gameStartButton.gameObject.SetActive(true);
            canceled = await gameStartButton.OnClickAsync(token).SuppressCancellationThrow();
            if (canceled)
            {
                return;
            }

            canceled = await PlayGameStartTransitionAsync(token);
            if (canceled)
            {
                return;
            }

            while (CurrentMode == StageMode.Play)
            {
                canceled = await UniTask.WaitUntil(
                        () => CurrentMode != StageMode.Play ||
                              HasPlayerFallen() ||
                              HasReachedGoal(),
                        cancellationToken: token)
                    .SuppressCancellationThrow();
                if (canceled)
                {
                    return;
                }

                if (CurrentMode != StageMode.Play)
                {
                    break;
                }

                if (HasReachedGoal())
                {
                    await EnterResultModeAsync(token);
                    return;
                }

                RetryPlayer();
            }
        }
    }

    private bool HasReachedGoal() =>
        playerCollider != null &&
        goalCollider != null &&
        playerCollider.enabled &&
        goalCollider.enabled &&
        playerCollider.Distance(goalCollider).isOverlapped;

    private bool AreRequiredBlocksPlaced() =>
        canvasManager != null
            ? canvasManager.AreRequiredBlocksPlaced
            : blockManager != null && blockManager.AllBlocksPlaced;

    private async UniTask EnterResultModeAsync(CancellationToken token)
    {
        CurrentMode = StageMode.Result;

        if (blockManager != null)
        {
            blockManager.enabled = false;
        }

        if (resultBackground == null)
        {
            return;
        }

        bool delayCanceled = await UniTask.Delay(
                TimeSpan.FromSeconds(resultDisplayDelay),
                ignoreTimeScale: useUnscaledResultTime,
                cancellationToken: token)
            .SuppressCancellationThrow();
        if (delayCanceled)
        {
            return;
        }

        resultBackground.gameObject.SetActive(true);
        resultBackground.SetAsLastSibling();
        resultBackground.anchoredPosition = resultHiddenPosition;
        resultTween = resultBackground
            .DOAnchorPos(resultShownPosition, resultMoveDuration)
            .SetEase(resultMoveEase)
            .SetUpdate(useUnscaledResultTime);

        bool canceled = await UniTask.WaitUntil(
                () => resultTween == null || !resultTween.IsActive(),
                cancellationToken: token)
            .SuppressCancellationThrow();

        if (canceled)
        {
            resultTween?.Kill();
        }

        resultTween = null;
    }

    private void ResetResultBackground()
    {
        resultTween?.Kill();
        resultTween = null;

        if (resultBackground == null)
        {
            return;
        }

        resultHiddenPosition = resultBackground.anchoredPosition;
        resultBackground.gameObject.SetActive(false);
    }

    private bool HasPlayerFallen()
    {
        if (playerBody != null)
        {
            return playerBody.position.y <= retryHeight;
        }

        return player != null && player.position.y <= retryHeight;
    }

    private void RetryPlayer()
    {
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
        }

        ApplyPlayerStartPosition();
        Physics2D.SyncTransforms();
    }

    private async UniTask<bool> PlayGameStartTransitionAsync(CancellationToken token)
    {
        if (transitionOverlay == null)
        {
            EnterPlayMode();
            return false;
        }

        transitionOverlay.gameObject.SetActive(true);
        transitionOverlay.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();

        float height = transitionOverlay.rect.height;
        transitionOverlay.anchoredPosition = Vector2.up * height;

        if (await MoveTransitionAsync(0f, coverDuration, token))
        {
            return true;
        }

        EnterPlayMode();

        bool canceled = await UniTask.Delay(
                TimeSpan.FromSeconds(fullCoverDuration),
                ignoreTimeScale: useUnscaledTransitionTime,
                cancellationToken: token)
            .SuppressCancellationThrow();
        if (canceled)
        {
            return true;
        }

        if (await MoveTransitionAsync(-height, revealDuration, token))
        {
            return true;
        }

        ResetTransitionOverlay();
        return false;
    }

    private async UniTaskVoid PlayRetryTransitionAsync(CancellationToken token)
    {
        try
        {
            if (transitionOverlay == null)
            {
                EnterBuildMode();
                return;
            }

            transitionOverlay.gameObject.SetActive(true);
            transitionOverlay.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();

            float height = transitionOverlay.rect.height;

            // 画面下から黒いオーバーレイを入れて、下端から画面を覆います。
            transitionOverlay.anchoredPosition = Vector2.down * height;
            if (await MoveTransitionAsync(0f, coverDuration, token))
            {
                return;
            }

            EnterBuildMode();

            bool canceled = await UniTask.Delay(
                    TimeSpan.FromSeconds(fullCoverDuration),
                    ignoreTimeScale: useUnscaledTransitionTime,
                    cancellationToken: token)
                .SuppressCancellationThrow();
            if (canceled)
            {
                return;
            }

            // 同じ向きへ通過させ、画面下側からステージを再表示します。
            if (await MoveTransitionAsync(height, revealDuration, token))
            {
                return;
            }

            ResetTransitionOverlay();
        }
        finally
        {
            isRetryTransitionPlaying = false;
        }
    }

    private async UniTask<bool> MoveTransitionAsync(
        float targetY,
        float duration,
        CancellationToken token)
    {
        transitionTween?.Kill();
        transitionTween = transitionOverlay
            .DOAnchorPosY(targetY, duration)
            .SetEase(transitionEase)
            .SetUpdate(useUnscaledTransitionTime);

        bool canceled = await UniTask.WaitUntil(
                () => transitionTween == null || !transitionTween.IsActive(),
                cancellationToken: token)
            .SuppressCancellationThrow();

        if (canceled)
        {
            transitionTween?.Kill();
        }

        transitionTween = null;
        return canceled;
    }

    private void ResetTransitionOverlay()
    {
        transitionTween?.Kill();
        transitionTween = null;

        if (transitionOverlay == null)
        {
            return;
        }

        transitionOverlay.anchoredPosition = Vector2.zero;

        if (transitionImage != null)
        {
            transitionImage.color = transitionColor;
        }

        transitionOverlay.gameObject.SetActive(false);
    }

    private void EnterBuildMode()
    {
        CurrentMode = StageMode.Build;
        SetBuildObjects(true);
        SetPlayerEnabled(false);
        ApplyPlayerStartPosition();
        ApplyGoalPosition();
    }

    /// <summary>
    /// プレイ中のステージをビルドモードへ戻します。
    /// </summary>
    public void ReturnToBuildMode()
    {
        if (CurrentMode != StageMode.Play ||
            isRetryTransitionPlaying ||
            (transitionOverlay != null && transitionOverlay.gameObject.activeSelf))
        {
            return;
        }

        isRetryTransitionPlaying = true;
        CurrentMode = StageMode.Build;
        SetPlayerEnabled(false);
        PlayRetryTransitionAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void EnterPlayMode()
    {
        CurrentMode = StageMode.Play;
        SetPlayerEnabled(true);
        SetBuildObjects(false);
    }

    private void SetBuildObjects(bool active)
    {
        if (blockManager != null)
        {
            blockManager.SetBuildMode(active);
        }

        blockBackground?.SetActive(active);
        gridBackground?.SetActive(active);
        gameStartButton?.gameObject.SetActive(false);
    }

    private void SetPlayerEnabled(bool enabled)
    {
        if (playerBody == null)
        {
            return;
        }

        playerBody.linearVelocity = Vector2.zero;
        playerBody.angularVelocity = 0f;
        playerBody.simulated = enabled;
    }

    /// <summary>
    /// 指定セルがプレイヤー開始位置の配置禁止範囲に含まれるかを返します。
    /// </summary>
    public bool IsPlayerStartCell(Vector3Int cell)
    {
        int width = Mathf.Max(1, reservedCellSize.x);
        int height = Mathf.Max(1, reservedCellSize.y);

        return cell.x >= playerStartCell.x &&
               cell.y >= playerStartCell.y &&
               cell.x < playerStartCell.x + width &&
               cell.y < playerStartCell.y + height;
    }

    public bool IsGoalCell(Vector3Int cell)
    {
        int width = Mathf.Max(1, goalReservedCellSize.x);
        int height = Mathf.Max(1, goalReservedCellSize.y);

        return cell.x >= goalCell.x &&
               cell.y >= goalCell.y &&
               cell.x < goalCell.x + width &&
               cell.y < goalCell.y + height;
    }

    public bool IsBlockPlacementReservedCell(Vector3Int cell) =>
        IsPlayerStartCell(cell) || IsGoalCell(cell);

    [ContextMenu("プレイヤーを開始位置へ移動")]
    public void ApplyPlayerStartPosition()
    {
        if (player == null || stageTilemap == null)
        {
            return;
        }

        Vector3 position = stageTilemap.GetCellCenterWorld(
            new Vector3Int(playerStartCell.x, playerStartCell.y, 0));
        position.x += playerStartOffset.x;
        position.y += playerStartOffset.y;
        position.z = player.position.z;
        player.position = position;

        if (playerBody != null)
        {
            playerBody.position = position;
        }
    }

    [ContextMenu("ゴールを設定位置へ移動")]
    public void ApplyGoalPosition()
    {
        if (goal == null || stageTilemap == null)
        {
            return;
        }

        Vector3 position = stageTilemap.GetCellCenterWorld(
            new Vector3Int(goalCell.x, goalCell.y, 0));
        position.x += goalOffset.x;
        position.y += goalOffset.y;
        position.z = goal.position.z;
        goal.position = position;
    }

    private void OnValidate()
    {
        reservedCellSize.x = Mathf.Max(1, reservedCellSize.x);
        reservedCellSize.y = Mathf.Max(1, reservedCellSize.y);
        goalReservedCellSize.x = Mathf.Max(1, goalReservedCellSize.x);
        goalReservedCellSize.y = Mathf.Max(1, goalReservedCellSize.y);
        resultDisplayDelay = Mathf.Max(0f, resultDisplayDelay);
        resultMoveDuration = Mathf.Max(0f, resultMoveDuration);
        coverDuration = Mathf.Max(0f, coverDuration);
        fullCoverDuration = Mathf.Max(0f, fullCoverDuration);
        revealDuration = Mathf.Max(0f, revealDuration);
        FindMissingReferences();

        if (!Application.isPlaying)
        {
            if (transitionImage != null)
            {
                transitionImage.color = transitionColor;
            }

            ApplyPlayerStartPosition();
            ApplyGoalPosition();
        }
    }

    private void FindMissingReferences()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.Find("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (goal == null)
        {
            GameObject goalObject = GameObject.Find("Goal");
            if (goalObject != null)
            {
                goal = goalObject.transform;
            }
        }

        if (stageTilemap == null)
        {
            stageTilemap = FindFirstObjectByType<Tilemap>();
        }

        if (blockManager == null)
        {
            blockManager = FindFirstObjectByType<BlockManager>();
        }

        if (canvasManager == null)
        {
            canvasManager = FindFirstObjectByType<CanvasManager>();
        }

        if (playerBody == null && player != null)
        {
            playerBody = player.GetComponent<Rigidbody2D>();
        }

        if (playerCollider == null && player != null)
        {
            playerCollider = player.GetComponent<Collider2D>();
        }

        if (goalCollider == null && goal != null)
        {
            goalCollider = goal.GetComponent<Collider2D>();
        }

        if (goalCollider != null)
        {
            goalCollider.isTrigger = true;
        }

        blockBackground ??= GameObject.Find("BlockBG");
        gridBackground ??= GameObject.Find("GridBG");

        if (gameStartButton == null && blockBackground != null)
        {
            gameStartButton = blockBackground.GetComponentInChildren<Button>(true);
        }

        if (transitionOverlay == null)
        {
            foreach (RectTransform rectTransform in FindObjectsByType<RectTransform>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (rectTransform.name == "StageTransition")
                {
                    transitionOverlay = rectTransform;
                    break;
                }
            }
        }

        if (transitionImage == null && transitionOverlay != null)
        {
            transitionImage = transitionOverlay.GetComponent<Image>();
        }

        if (resultBackground == null)
        {
            foreach (RectTransform rectTransform in FindObjectsByType<RectTransform>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (rectTransform.name == "ResultBG")
                {
                    resultBackground = rectTransform;
                    break;
                }
            }
        }
    }

    private void OnDestroy()
    {
        transitionTween?.Kill();
        resultTween?.Kill();
    }

    private void OnDrawGizmosSelected()
    {
        if (stageTilemap == null)
        {
            return;
        }

        Vector3Int minCell = new Vector3Int(playerStartCell.x, playerStartCell.y, 0);
        Vector3Int maxCell = minCell + new Vector3Int(
            Mathf.Max(1, reservedCellSize.x),
            Mathf.Max(1, reservedCellSize.y),
            0);
        Vector3 min = stageTilemap.CellToWorld(minCell);
        Vector3 max = stageTilemap.CellToWorld(maxCell);

        Gizmos.color = new Color(1f, 0.75f, 0.15f, 0.9f);
        Gizmos.DrawWireCube((min + max) * 0.5f, max - min);

        Vector3Int goalMinCell = new Vector3Int(goalCell.x, goalCell.y, 0);
        Vector3Int goalMaxCell = goalMinCell + new Vector3Int(
            Mathf.Max(1, goalReservedCellSize.x),
            Mathf.Max(1, goalReservedCellSize.y),
            0);
        Vector3 goalMin = stageTilemap.CellToWorld(goalMinCell);
        Vector3 goalMax = stageTilemap.CellToWorld(goalMaxCell);

        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireCube((goalMin + goalMax) * 0.5f, goalMax - goalMin);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawLine(
            new Vector3(min.x, retryHeight, 0f),
            new Vector3(max.x, retryHeight, 0f));
    }
}

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class StageManager : MonoBehaviour
{
    private const float ClosedDeathIrisRadius = -0.01f;

    public enum StageMode
    {
        Build,
        Play,
        GoalReached,
        Result
    }

    [Header("ステージ情報")]
    [Tooltip("HierarchyでStageManagerを選択し、このステージの番号を設定します。")]
    [Min(1)]
    [SerializeField] private int stageNumber = 1;

    [Tooltip("ステージシーン名の先頭文字です。番号2なら Stage2 を読み込みます。")]
    [SerializeField] private string stageSceneNamePrefix = "Stage";

    [SerializeField] private TMP_Text pauseStageText;
    [SerializeField] private TMP_Text resultStageText;
    [SerializeField] private Button nextStageButton;

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

    [Tooltip("ゴール位置としてブロック配置を禁止するセル数です。Xが横、Yが縦です。Yはゴール地点とその1マス上を含むため最低2です。")]
    [SerializeField] private Vector2Int goalReservedCellSize = new Vector2Int(1, 2);

    [Header("鍵ギミック")]
    [Tooltip("有効にすると、鍵を取得するまでゴールをロックします。")]
    [SerializeField] private bool useKeyGimmick;

    [Tooltip("設定した位置へ移動する鍵のTransformです。")]
    [SerializeField] private Transform key;

    [Tooltip("鍵を配置するTilemapセル座標です。")]
    [SerializeField] private Vector2Int keyCell = Vector2Int.zero;

    [Tooltip("鍵セル中央から実際の位置へ加える補正値です。")]
    [SerializeField] private Vector2 keyOffset;

    [Tooltip("鍵位置としてブロック配置を禁止するセル数です。Xが横、Yが縦です。")]
    [SerializeField] private Vector2Int keyReservedCellSize = Vector2Int.one;

    [Tooltip("プレイヤーとの接触判定に使用する鍵のCollider2Dです。")]
    [SerializeField] private Collider2D keyCollider;

    [Tooltip("鍵のフェード表示に使用するSpriteRendererです。")]
    [SerializeField] private SpriteRenderer keySpriteRenderer;

    [Header("鍵アニメーション")]
    [Tooltip("待機中に鍵が上下する片側の距離です。")]
    [Min(0f)]
    [SerializeField] private float keyFloatDistance = 0.12f;

    [Tooltip("鍵が中央から上端へ移動する時間です。")]
    [Min(0.01f)]
    [SerializeField] private float keyFloatHalfDuration = 0.8f;

    [SerializeField] private Ease keyFloatEase = Ease.InOutSine;

    [Tooltip("獲得時に鍵が拡大する倍率です。")]
    [Min(1f)]
    [SerializeField] private float keyCollectScale = 1.5f;

    [Tooltip("獲得時の拡大とフェードアウトにかける時間です。")]
    [Min(0f)]
    [SerializeField] private float keyCollectDuration = 0.25f;

    [SerializeField] private Ease keyCollectEase = Ease.OutCubic;

    [Tooltip("鍵を取得するまでゴールに表示するRock0スプライトです。")]
    [SerializeField] private Sprite goalLockedSprite;

    [Header("ゴール判定とリザルト表示")]
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private Collider2D goalCollider;

    [Header("クリック式ゴール演出")]
    [Tooltip("ゴール到達後、フラッグのクリックを待ってからリザルトを表示します。")]
    [SerializeField] private bool useInteractiveGoalResult;

    [SerializeField] private SpriteRenderer goalSpriteRenderer;
    [SerializeField] private Sprite goalClearedSprite;

    [Min(0f)]
    [SerializeField] private float playerAbsorbDuration = 0.45f;
    [SerializeField] private Ease playerAbsorbEase = Ease.InCubic;

    [Min(1f)]
    [SerializeField] private float goalPopScale = 1.2f;
    [Min(0f)]
    [SerializeField] private float goalPopDuration = 0.25f;
    [SerializeField] private Ease goalPopEase = Ease.OutCubic;
    [SerializeField] private bool useUnscaledGoalTime = true;

    [Header("クリック可能時のゴールHover")]
    [Tooltip("ゴール到達後にGoal本体へ設定するSorting Orderです。")]
    [SerializeField] private int goalReachedSortingOrder = short.MaxValue;

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

    [Header("ポーズ表示")]
    [Tooltip("画面下から表示するPauseBGです。")]
    [SerializeField] private RectTransform pauseBackground;

    [Tooltip("BlockBGの子にあるPauseButtonです。")]
    [SerializeField] private Button pauseButton;

    [Tooltip("PauseBGの子にあるBackButtonです。")]
    [SerializeField] private Button pauseBackButton;

    [Tooltip("PauseBGが表示されたときの座標です。")]
    [SerializeField] private Vector2 pauseShownPosition = Vector2.zero;

    [Min(0f)]
    [SerializeField] private float pauseMoveDuration = 0.35f;

    [SerializeField] private Ease pauseMoveEase = Ease.OutCubic;
    [SerializeField] private bool useUnscaledPauseTime = true;

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

    [Header("接触リトライ")]
    [Tooltip("プレイヤーが触れると開始位置からやり直すTilemapを指定します。複数指定できます。")]
    [SerializeField] private Tilemap[] retryTilemaps = Array.Empty<Tilemap>();

    [Header("死亡アニメーション")]
    [Tooltip("通常時と死亡時の見た目を切り替えるSpriteRendererです。")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    [Tooltip("死亡判定の瞬間に表示するplayer_deadスプライトです。")]
    [SerializeField] private Sprite playerDeadSprite;

    [Tooltip("画面を円形に閉じるUIBuilder/DeathIrisシェーダーです。")]
    [SerializeField] private Shader deathIrisShader;

    [Tooltip("死亡時のアイリスを覆う色です。既定値は#3C3C3Cです。")]
    [SerializeField] private Color deathIrisColor = new Color(60f / 255f, 60f / 255f, 60f / 255f, 1f);

    [Min(0f)]
    [SerializeField] private float deathIrisCloseDuration = 0.45f;

    [Tooltip("アイリスが完全に閉じ、画面全体が#3C3C3Cのまま停止する時間です。")]
    [InspectorName("画面全体が3C3C3Cの時間")]
    [Min(0f)]
    [SerializeField] private float deathIrisClosedDuration = 0.12f;

    [Min(0f)]
    [SerializeField] private float deathIrisOpenDuration = 0.45f;

    [SerializeField] private Ease deathIrisCloseEase = Ease.InCubic;
    [SerializeField] private Ease deathIrisOpenEase = Ease.OutCubic;
    [SerializeField] private bool useUnscaledDeathTime = true;

    [Tooltip("player_deadへ切り替わった瞬間の拡大率です。")]
    [Min(1f)]
    [SerializeField] private float deathPopScale = 1.15f;

    [Tooltip("拡大したプレイヤーが通常サイズへ戻る時間です。")]
    [Min(0f)]
    [SerializeField] private float deathPopDuration = 0.12f;

    [SerializeField] private Ease deathPopEase = Ease.OutCubic;

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

    public int StageNumber => stageNumber;
    public Vector2Int PlayerStartCell => playerStartCell;
    public Vector2Int GoalCell => goalCell;
    public Vector2Int KeyCell => keyCell;
    public StageMode CurrentMode { get; private set; }
    public bool IsPaused { get; private set; }

    private Tweener transitionTween;
    private Tweener deathIrisTween;
    private Tweener deathPopTween;
    private Tweener resultTween;
    private Tweener pauseTween;
    private Collider2D[] retryTilemapColliders = Array.Empty<Collider2D>();
    private Sequence playerAbsorbTween;
    private Sequence goalPopTween;
    private Sequence keyFloatTween;
    private Sequence keyCollectTween;
    private Vector2 resultHiddenPosition;
    private Vector2 pauseHiddenPosition;
    private Vector3 playerDefaultScale;
    private Sprite playerDefaultSprite;
    private Vector3 keyDefaultScale;
    private Color keyDefaultColor = Color.white;
    private Vector3 goalDefaultScale;
    private Sprite goalDefaultSprite;
    private int goalDefaultSortingOrder;
    private Material goalHoverOutlineMaterial;
    private Material deathIrisMaterial;
    private Material transitionDefaultMaterial;
    private SpriteRenderer[] goalHoverOutlineRenderers = Array.Empty<SpriteRenderer>();
    private bool isKeyCollected;
    private bool isRetryTransitionPlaying;
    private bool isGoalClickable;
    private StageMode modeBeforePause;
    private bool blockManagerWasEnabled;
    private bool playerWasSimulated;
    private float timeScaleBeforePause = 1f;

    private static readonly Vector2[] GoalHoverOutlineDirections =
    {
        Vector2.right,
        Vector2.left,
        Vector2.up,
        Vector2.down,
        new Vector2(1f, 1f).normalized,
        new Vector2(1f, -1f).normalized,
        new Vector2(-1f, 1f).normalized,
        new Vector2(-1f, -1f).normalized
    };

    private void Awake()
    {
        FindMissingReferences();
        CacheRetryTilemapColliders();
        ApplyStageInformation();
        ConfigureStageNavigation();
        ConfigurePlayerLandingPhysics();
        CacheDeathPresentation();
        CacheKeyPresentation();
        CacheGoalPresentation();
        CreateGoalHoverOutline();

        ConfigurePauseButtons();
        ResetTransitionOverlay();
        ResetResultBackground();
        ResetPauseBackground();
        EnterBuildMode();
    }

    private void Start() => RunStageAsync(this.GetCancellationTokenOnDestroy()).Forget();

    private void FixedUpdate()
    {
        if (CurrentMode == StageMode.Play && !IsPaused && IsPlayerTouchingKey())
        {
            CollectKey();
        }
    }

    private void ApplyStageInformation()
    {
        string label = $"Stage{stageNumber}";
        if (pauseStageText != null)
        {
            pauseStageText.text = label;
        }

        if (resultStageText != null)
        {
            resultStageText.text = label;
        }

        if (nextStageButton != null)
        {
            nextStageButton.interactable = Application.CanStreamedLevelBeLoaded(
                GetStageSceneName(stageNumber + 1));
        }
    }

    private string GetStageSceneName(int number) => $"{stageSceneNamePrefix}{number}";

    private void ConfigureStageNavigation()
    {
        nextStageButton?.onClick.RemoveListener(LoadNextStage);
        nextStageButton?.onClick.AddListener(LoadNextStage);
    }

    public void LoadNextStage()
    {
        string nextSceneName = GetStageSceneName(stageNumber + 1);
        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogWarning(
                $"StageManager: 次のステージ '{nextSceneName}' がBuild Settingsにありません。",
                this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(nextSceneName);
    }

    private void CacheRetryTilemapColliders()
    {
        if (retryTilemaps == null || retryTilemaps.Length == 0)
        {
            retryTilemapColliders = Array.Empty<Collider2D>();
            return;
        }

        retryTilemapColliders = new Collider2D[retryTilemaps.Length];

        for (int i = 0; i < retryTilemaps.Length; i++)
        {
            Tilemap retryTilemap = retryTilemaps[i];
            if (retryTilemap == null)
            {
                continue;
            }

            Collider2D retryCollider = retryTilemap.GetComponent<CompositeCollider2D>();
            retryCollider ??= retryTilemap.GetComponent<TilemapCollider2D>();
            retryTilemapColliders[i] = retryCollider;
        }
    }

    /// <summary>
    /// 高速落下時も接触面を通り越さず、描画フレーム間も滑らかに表示します。
    /// </summary>
    private void ConfigurePlayerLandingPhysics()
    {
        if (playerBody == null)
        {
            return;
        }

        playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        playerBody.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        if (!isGoalClickable)
        {
            return;
        }

        bool hovered = Input.touchCount == 0 &&
                       !IsPointerOverUi() &&
                       IsPointerOverGoal(Input.mousePosition);
        SetGoalHoverOutline(true, hovered);
    }

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
            AudioManager.Instance?.PlayGameStartSound();

            canceled = await PlayGameStartTransitionAsync(token);
            if (canceled)
            {
                return;
            }

            while (CurrentMode == StageMode.Play)
            {
                canceled = await UniTask.WaitUntil(
                        () => CurrentMode != StageMode.Play ||
                              (!IsPaused &&
                               (HasPlayerFallen() || HasTouchedRetryTilemap() || HasReachedGoal())),
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
                    SetGoalReachedDepth(true);
                    if (useInteractiveGoalResult)
                    {
                        await PlayInteractiveGoalSequenceAsync(token);
                    }
                    else
                    {
                        await EnterResultModeAsync(token);
                    }

                    return;
                }

                if (await PlayDeathAndRestartAsync(token))
                {
                    return;
                }
            }
        }
    }

    private bool HasReachedGoal() =>
        (!useKeyGimmick || isKeyCollected) &&
        playerCollider != null &&
        goalCollider != null &&
        playerCollider.enabled &&
        goalCollider.enabled &&
        playerCollider.Distance(goalCollider).isOverlapped;


    private bool AreRequiredBlocksPlaced() =>
        canvasManager != null
            ? canvasManager.AreRequiredBlocksPlaced
            : blockManager != null && blockManager.AllBlocksPlaced;

    /// <summary>
    /// 吸い込み、フラッグ変化、クリック待機を順番に実行します。
    /// </summary>
    private async UniTask PlayInteractiveGoalSequenceAsync(CancellationToken token)
    {
        CurrentMode = StageMode.GoalReached;

        if (blockManager != null)
        {
            blockManager.enabled = false;
        }

        StopPlayerForGoalAnimation();
        if (await PlayPlayerAbsorbAsync(token))
        {
            return;
        }

        ChangeToClearedGoalSprite();
        if (await PlayGoalPopAsync(token))
        {
            return;
        }

        SetGoalClickable(true);
        bool clickWaitCanceled = await UniTask.WaitUntil(
                WasGoalClicked,
                cancellationToken: token)
            .SuppressCancellationThrow();
        SetGoalClickable(false);

        if (!clickWaitCanceled)
        {
            await EnterResultModeAsync(token, skipDelay: true);
        }
    }

    private void StopPlayerForGoalAnimation()
    {
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            playerBody.simulated = false;
        }

        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }
    }

    /// <returns>キャンセルされた場合はtrue。</returns>
    private async UniTask<bool> PlayPlayerAbsorbAsync(CancellationToken token)
    {
        if (player == null)
        {
            return false;
        }

        Vector3 targetPosition = GetGoalVisualCenter();
        targetPosition.z = player.position.z;

        playerAbsorbTween?.Kill();
        playerAbsorbTween = DOTween.Sequence()
            .Join(player.DOMove(targetPosition, playerAbsorbDuration).SetEase(playerAbsorbEase))
            .Join(player.DOScale(Vector3.zero, playerAbsorbDuration).SetEase(playerAbsorbEase))
            .SetUpdate(useUnscaledGoalTime);

        bool canceled = await WaitForTweenAsync(playerAbsorbTween, token);
        playerAbsorbTween = null;
        if (!canceled)
        {
            player.gameObject.SetActive(false);
        }

        return canceled;
    }

    private Vector3 GetGoalVisualCenter()
    {
        if (goalSpriteRenderer != null)
        {
            return goalSpriteRenderer.bounds.center;
        }

        return goal != null ? goal.position : Vector3.zero;
    }

    private void ChangeToClearedGoalSprite()
    {
        if (goalSpriteRenderer != null && goalClearedSprite != null)
        {
            goalSpriteRenderer.sprite = goalClearedSprite;
        }

    }

    /// <returns>キャンセルされた場合はtrue。</returns>
    private async UniTask<bool> PlayGoalPopAsync(CancellationToken token)
    {
        if (goal == null)
        {
            return false;
        }

        goalPopTween?.Kill();
        goal.localScale = goalDefaultScale;
        goalPopTween = DOTween.Sequence()
            .Append(goal.DOScale(goalDefaultScale * goalPopScale, goalPopDuration * 0.5f)
                .SetEase(goalPopEase))
            .Append(goal.DOScale(goalDefaultScale, goalPopDuration * 0.5f)
                .SetEase(Ease.InOutSine))
            .SetUpdate(useUnscaledGoalTime);

        bool canceled = await WaitForTweenAsync(goalPopTween, token);
        goalPopTween = null;
        return canceled;
    }

    private static async UniTask<bool> WaitForTweenAsync(
        Tween tween,
        CancellationToken token)
    {
        bool canceled = await UniTask.WaitUntil(
                () => tween == null || !tween.IsActive() || tween.IsComplete(),
                cancellationToken: token)
            .SuppressCancellationThrow();

        if (canceled)
        {
            tween?.Kill();
        }

        return canceled;
    }

    private bool WasGoalClicked() =>
        Input.GetMouseButtonDown(0) &&
        !IsPointerOverUi() &&
        IsPointerOverGoal(Input.mousePosition);

    private async UniTask EnterResultModeAsync(
        CancellationToken token,
        bool skipDelay = false)
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

        if (!skipDelay)
        {
            bool delayCanceled = await UniTask.Delay(
                    TimeSpan.FromSeconds(resultDisplayDelay),
                    ignoreTimeScale: useUnscaledResultTime,
                    cancellationToken: token)
                .SuppressCancellationThrow();
            if (delayCanceled)
            {
                return;
            }
        }

        blockManager?.ResetBgmScrollBarVolume();

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

    /// <summary>
    /// 現在の画面を停止してPauseBGを表示します。
    /// </summary>
    public void ShowPause()
    {
        if (IsPaused ||
            CurrentMode == StageMode.GoalReached ||
            CurrentMode == StageMode.Result ||
            isRetryTransitionPlaying ||
            (transitionOverlay != null && transitionOverlay.gameObject.activeSelf))
        {
            return;
        }

        IsPaused = true;
        modeBeforePause = CurrentMode;
        timeScaleBeforePause = Time.timeScale;

        if (blockManager != null)
        {
            blockManagerWasEnabled = blockManager.enabled;
            blockManager.enabled = false;
        }

        if (canvasManager != null)
        {
            canvasManager.SetPaused(true);
        }

        if (playerBody != null)
        {
            playerWasSimulated = playerBody.simulated;
            playerBody.simulated = false;
        }

        Time.timeScale = 0f;

        if (pauseBackground == null)
        {
            return;
        }

        pauseTween?.Kill();
        pauseBackground.gameObject.SetActive(true);
        pauseBackground.SetAsLastSibling();
        pauseBackground.anchoredPosition = pauseHiddenPosition;
        pauseTween = pauseBackground
            .DOAnchorPos(pauseShownPosition, pauseMoveDuration)
            .SetEase(pauseMoveEase)
            .SetUpdate(useUnscaledPauseTime)
            .OnComplete(() => pauseTween = null);
    }

    /// <summary>
    /// PauseBGを閉じて、ポーズ前の画面へ戻します。
    /// </summary>
    public void HidePause()
    {
        if (!IsPaused || pauseTween != null && pauseTween.IsActive())
        {
            return;
        }

        if (pauseBackground == null)
        {
            CompletePauseClose();
            return;
        }

        pauseTween = pauseBackground
            .DOAnchorPos(pauseHiddenPosition, pauseMoveDuration)
            .SetEase(pauseMoveEase)
            .SetUpdate(useUnscaledPauseTime)
            .OnComplete(CompletePauseClose);
    }

    private void CompletePauseClose()
    {
        pauseTween = null;
        pauseBackground?.gameObject.SetActive(false);
        Time.timeScale = timeScaleBeforePause;

        if (blockManager != null)
        {
            blockManager.enabled = blockManagerWasEnabled;
        }

        if (canvasManager != null)
        {
            canvasManager.SetPaused(false);
        }

        if (playerBody != null && CurrentMode == modeBeforePause)
        {
            playerBody.simulated = playerWasSimulated;
        }

        IsPaused = false;
    }

    private void ResetPauseBackground()
    {
        pauseTween?.Kill();
        pauseTween = null;

        if (pauseBackground == null)
        {
            return;
        }

        pauseHiddenPosition = pauseBackground.anchoredPosition;
        pauseBackground.gameObject.SetActive(false);
    }

    private bool HasPlayerFallen()
    {
        if (playerBody != null)
        {
            return playerBody.position.y <= retryHeight;
        }

        return player != null && player.position.y <= retryHeight;
    }

    private bool HasTouchedRetryTilemap()
    {
        if (playerCollider == null || !playerCollider.enabled)
        {
            return false;
        }

        foreach (Collider2D tilemapCollider in retryTilemapColliders)
        {
            if (tilemapCollider != null &&
                tilemapCollider.enabled &&
                tilemapCollider.gameObject.activeInHierarchy &&
                (playerCollider.IsTouching(tilemapCollider) ||
                 playerCollider.Distance(tilemapCollider).isOverlapped))
            {
                return true;
            }
        }

        return false;
    }

    private void RetryPlayer()
    {
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
        }

        blockManager?.ResetBgmScrollBarVolume();
        ResetKeyGimmick();
        ApplyPlayerStartPosition();
        Physics2D.SyncTransforms();
    }

    /// <summary>
    /// プレイヤーを停止して死亡表示へ切り替え、暗転中に開始位置へ戻してから再開します。
    /// trueは処理がキャンセルされたことを表します。
    /// </summary>
    private async UniTask<bool> PlayDeathAndRestartAsync(CancellationToken token)
    {
        AudioManager.Instance?.PlayDeathSound();
        StopPlayerForDeath();
        SetPlayerSprite(playerDeadSprite);
        PlayDeathPopAnimation();

        if (!PrepareDeathIris(GetPlayerIrisCenter(), out float closeStartRadius))
        {
            RetryPlayer();
            RestorePlayerAfterDeath();
            return false;
        }

        try
        {
            SetDeathIrisRadius(closeStartRadius);
            if (await TweenDeathIrisRadiusAsync(
                    ClosedDeathIrisRadius,
                    deathIrisCloseDuration,
                    deathIrisCloseEase,
                    token))
            {
                return true;
            }

            bool canceled = await UniTask.Delay(
                    TimeSpan.FromSeconds(deathIrisClosedDuration),
                    ignoreTimeScale: useUnscaledDeathTime,
                    cancellationToken: token)
                .SuppressCancellationThrow();
            if (canceled)
            {
                return true;
            }

            // 完全に暗くなっている間に開始位置へ戻します。
            RetryPlayer();
            SetPlayerSprite(playerDefaultSprite);
            SetDeathIrisCenter(GetPlayerIrisCenter());

            if (await TweenDeathIrisRadiusAsync(
                    GetDeathIrisMaxRadius(),
                    deathIrisOpenDuration,
                    deathIrisOpenEase,
                    token))
            {
                return true;
            }

            return false;
        }
        finally
        {
            deathIrisTween?.Kill();
            deathIrisTween = null;
            RestoreTransitionAfterDeath();
            RestorePlayerAfterDeath();
        }
    }

    private void StopPlayerForDeath()
    {
        if (playerBody == null)
        {
            return;
        }

        playerBody.linearVelocity = Vector2.zero;
        playerBody.angularVelocity = 0f;
        playerBody.simulated = false;
    }

    private void RestorePlayerAfterDeath()
    {
        deathPopTween?.Kill();
        deathPopTween = null;
        SetPlayerSprite(playerDefaultSprite);

        if (player != null)
        {
            player.localScale = playerDefaultScale;
        }

        if (playerBody != null && CurrentMode == StageMode.Play)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            playerBody.simulated = true;
        }
    }

    private void SetPlayerSprite(Sprite sprite)
    {
        if (playerSpriteRenderer != null && sprite != null)
        {
            playerSpriteRenderer.sprite = sprite;
        }
    }

    private void PlayDeathPopAnimation()
    {
        if (player == null)
        {
            return;
        }

        deathPopTween?.Kill();
        player.localScale = playerDefaultScale * deathPopScale;

        if (deathPopDuration <= 0f)
        {
            player.localScale = playerDefaultScale;
            return;
        }

        deathPopTween = player
            .DOScale(playerDefaultScale, deathPopDuration)
            .SetEase(deathPopEase)
            .SetUpdate(useUnscaledDeathTime)
            .OnComplete(() => deathPopTween = null);
    }

    private void CacheDeathPresentation()
    {
        playerDefaultSprite = playerSpriteRenderer != null
            ? playerSpriteRenderer.sprite
            : null;
        transitionDefaultMaterial = transitionImage != null
            ? transitionImage.material
            : null;

        deathIrisShader ??= Shader.Find("UIBuilder/DeathIris");
        if (deathIrisShader != null)
        {
            deathIrisMaterial = new Material(deathIrisShader)
            {
                name = "Death Iris (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }

    private bool PrepareDeathIris(Vector2 center, out float maxRadius)
    {
        maxRadius = 0f;
        if (transitionOverlay == null || transitionImage == null || deathIrisMaterial == null)
        {
            return false;
        }

        transitionOverlay.anchoredPosition = Vector2.zero;
        transitionOverlay.gameObject.SetActive(true);
        transitionOverlay.SetAsLastSibling();
        transitionImage.material = deathIrisMaterial;
        transitionImage.color = Color.white;
        Canvas.ForceUpdateCanvases();

        Rect rect = transitionOverlay.rect;
        deathIrisMaterial.SetColor("_Color", deathIrisColor);
        deathIrisMaterial.SetFloat(
            "_Aspect",
            rect.height > 0f ? rect.width / rect.height : 1f);
        SetDeathIrisCenter(center);
        maxRadius = GetDeathIrisMaxRadius();
        return true;
    }

    private Vector2 GetPlayerIrisCenter()
    {
        if (player == null || transitionOverlay == null)
        {
            return new Vector2(0.5f, 0.5f);
        }

        Camera worldCamera = Camera.main != null
            ? Camera.main
            : FindFirstObjectByType<Camera>();
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            worldCamera,
            player.position);

        Canvas canvas = transitionOverlay.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transitionOverlay,
                screenPoint,
                uiCamera,
                out Vector2 localPoint))
        {
            return new Vector2(0.5f, 0.5f);
        }

        Rect rect = transitionOverlay.rect;
        return new Vector2(
            Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x),
            Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y));
    }

    private void SetDeathIrisCenter(Vector2 center)
    {
        deathIrisMaterial?.SetVector("_Center", center);
    }

    private void SetDeathIrisRadius(float radius)
    {
        deathIrisMaterial?.SetFloat("_Radius", radius);
    }

    private float GetDeathIrisMaxRadius()
    {
        if (deathIrisMaterial == null)
        {
            return 1f;
        }

        Vector4 centerValue = deathIrisMaterial.GetVector("_Center");
        Vector2 center = new Vector2(centerValue.x, centerValue.y);
        float aspect = Mathf.Max(
            0.0001f,
            deathIrisMaterial.GetFloat("_Aspect"));
        float maxRadius = 0f;

        Vector2[] corners =
        {
            Vector2.zero,
            Vector2.right,
            Vector2.up,
            Vector2.one
        };

        foreach (Vector2 corner in corners)
        {
            Vector2 delta = corner - center;
            delta.x *= aspect;
            maxRadius = Mathf.Max(maxRadius, delta.magnitude);
        }

        return maxRadius + 0.01f;
    }

    private async UniTask<bool> TweenDeathIrisRadiusAsync(
        float targetRadius,
        float duration,
        Ease ease,
        CancellationToken token)
    {
        deathIrisTween?.Kill();

        if (duration <= 0f)
        {
            SetDeathIrisRadius(targetRadius);
            return false;
        }

        deathIrisTween = DOTween.To(
                () => deathIrisMaterial.GetFloat("_Radius"),
                SetDeathIrisRadius,
                targetRadius,
                duration)
            .SetEase(ease)
            .SetUpdate(useUnscaledDeathTime);

        bool canceled = await UniTask.WaitUntil(
                () => deathIrisTween == null || !deathIrisTween.IsActive(),
                cancellationToken: token)
            .SuppressCancellationThrow();

        if (canceled)
        {
            deathIrisTween?.Kill();
        }

        deathIrisTween = null;
        return canceled;
    }

    private void RestoreTransitionAfterDeath()
    {
        if (transitionImage != null)
        {
            transitionImage.material = transitionDefaultMaterial;
            transitionImage.color = transitionColor;
        }

        if (transitionOverlay != null)
        {
            transitionOverlay.anchoredPosition = Vector2.zero;
            transitionOverlay.gameObject.SetActive(false);
        }
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
        ResetInteractiveGoalPresentation();
        ResetKeyGimmick();
        CurrentMode = StageMode.Build;
        SetBuildObjects(true);
        SetPlayerEnabled(false);
        ApplyPlayerStartPosition();
        ApplyGoalPosition();
        ApplyKeyPosition();
    }

    /// <summary>
    /// プレイ中のステージをビルドモードへ戻します。
    /// </summary>
    public void ReturnToBuildMode()
    {
        if (IsPaused ||
            CurrentMode != StageMode.Play ||
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
        if (player != null)
        {
            player.gameObject.SetActive(true);
            player.localScale = playerDefaultScale;
        }

        if (playerCollider != null)
        {
            playerCollider.enabled = true;
        }

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
        int height = Mathf.Max(2, goalReservedCellSize.y);

        return cell.x >= goalCell.x &&
               cell.y >= goalCell.y &&
               cell.x < goalCell.x + width &&
               cell.y < goalCell.y + height;
    }

    public bool IsKeyCell(Vector3Int cell)
    {
        if (!useKeyGimmick)
        {
            return false;
        }

        int width = Mathf.Max(1, keyReservedCellSize.x);
        int height = Mathf.Max(1, keyReservedCellSize.y);

        return cell.x >= keyCell.x &&
               cell.y >= keyCell.y &&
               cell.x < keyCell.x + width &&
               cell.y < keyCell.y + height;
    }

    public bool IsBlockPlacementReservedCell(Vector3Int cell) =>
        IsPlayerStartCell(cell) || IsGoalCell(cell) || IsKeyCell(cell) || IsRetryTilemapCell(cell);

    private bool IsRetryTilemapCell(Vector3Int cell)
    {
        if (retryTilemaps == null || retryTilemaps.Length == 0)
        {
            return false;
        }

        Vector3 worldPosition = stageTilemap != null
            ? stageTilemap.GetCellCenterWorld(cell)
            : (Vector3)cell;

        foreach (Tilemap retryTilemap in retryTilemaps)
        {
            if (retryTilemap != null &&
                retryTilemap.HasTile(retryTilemap.WorldToCell(worldPosition)))
            {
                return true;
            }
        }

        return false;
    }

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

    [ContextMenu("鍵を設定位置へ移動")]
    public void ApplyKeyPosition()
    {
        if (key == null || stageTilemap == null)
        {
            return;
        }

        Vector3 position = stageTilemap.GetCellCenterWorld(
            new Vector3Int(keyCell.x, keyCell.y, 0));
        position.x += keyOffset.x;
        position.y += keyOffset.y;
        position.z = key.position.z;
        key.position = position;

        if (Application.isPlaying && useKeyGimmick && !isKeyCollected)
        {
            StartKeyFloatAnimation();
        }
    }

    private bool IsPlayerTouchingKey() =>
        useKeyGimmick &&
        !isKeyCollected &&
        key != null &&
        key.gameObject.activeInHierarchy &&
        playerCollider != null &&
        keyCollider != null &&
        playerCollider.enabled &&
        keyCollider.enabled &&
        playerCollider.Distance(keyCollider).isOverlapped;

    private void CollectKey()
    {
        isKeyCollected = true;

        if (keyCollider != null)
        {
            keyCollider.enabled = false;
        }

        PlayKeyCollectAnimation();
        RefreshGoalLockPresentation();
    }

    private void ResetKeyGimmick()
    {
        StopKeyAnimations();
        isKeyCollected = !useKeyGimmick;
        if (key != null)
        {
            key.gameObject.SetActive(useKeyGimmick);
            key.localScale = keyDefaultScale;
        }

        if (keySpriteRenderer != null)
        {
            keySpriteRenderer.color = keyDefaultColor;
        }

        if (keyCollider != null)
        {
            keyCollider.enabled = useKeyGimmick;
        }

        if (useKeyGimmick)
        {
            ApplyKeyPosition();
        }

        RefreshGoalLockPresentation();
    }

    private void CacheKeyPresentation()
    {
        keyDefaultScale = key != null ? key.localScale : Vector3.one;
        keyDefaultColor = keySpriteRenderer != null
            ? keySpriteRenderer.color
            : Color.white;
    }

    private void StartKeyFloatAnimation()
    {
        if (key == null || keyFloatDistance <= 0f)
        {
            return;
        }

        keyFloatTween?.Kill();
        keyFloatTween = null;

        float centerY = key.position.y;
        float halfDuration = Mathf.Max(0.01f, keyFloatHalfDuration);

        keyFloatTween = DOTween.Sequence()
            .Append(key.DOMoveY(centerY + keyFloatDistance, halfDuration)
                .SetEase(keyFloatEase))
            .Append(key.DOMoveY(centerY - keyFloatDistance, halfDuration * 2f)
                .SetEase(keyFloatEase))
            .Append(key.DOMoveY(centerY, halfDuration)
                .SetEase(keyFloatEase))
            .SetLoops(-1, LoopType.Restart);
    }

    private void PlayKeyCollectAnimation()
    {
        keyFloatTween?.Kill();
        keyFloatTween = null;
        keyCollectTween?.Kill();
        keyCollectTween = null;

        if (key == null)
        {
            return;
        }

        if (keyCollectDuration <= 0f)
        {
            key.gameObject.SetActive(false);
            return;
        }

        keyCollectTween = DOTween.Sequence()
            .Join(key.DOScale(
                    keyDefaultScale * keyCollectScale,
                    keyCollectDuration)
                .SetEase(keyCollectEase));

        if (keySpriteRenderer != null)
        {
            keyCollectTween.Join(
                keySpriteRenderer.DOFade(0f, keyCollectDuration)
                    .SetEase(keyCollectEase));
        }

        keyCollectTween.OnComplete(() =>
        {
            keyCollectTween = null;
            if (key != null)
            {
                key.gameObject.SetActive(false);
            }
        });
    }

    private void StopKeyAnimations()
    {
        keyFloatTween?.Kill();
        keyFloatTween = null;
        keyCollectTween?.Kill();
        keyCollectTween = null;
    }

    private void RefreshGoalLockPresentation()
    {
        if (goalSpriteRenderer == null)
        {
            return;
        }

        goalSpriteRenderer.sprite = useKeyGimmick && !isKeyCollected && goalLockedSprite != null
            ? goalLockedSprite
            : goalDefaultSprite;
    }

    private void CacheGoalPresentation()
    {
        playerDefaultScale = player != null ? player.localScale : Vector3.one;
        goalDefaultScale = goal != null ? goal.localScale : Vector3.one;
        goalDefaultSprite = goalSpriteRenderer != null ? goalSpriteRenderer.sprite : null;
        goalDefaultSortingOrder = goalSpriteRenderer != null
            ? goalSpriteRenderer.sortingOrder
            : 0;
    }

    private void ResetInteractiveGoalPresentation()
    {
        SetGoalClickable(false);
        playerAbsorbTween?.Kill();
        playerAbsorbTween = null;
        goalPopTween?.Kill();
        goalPopTween = null;

        if (player != null)
        {
            player.gameObject.SetActive(true);
            player.localScale = playerDefaultScale;
        }

        if (playerCollider != null)
        {
            playerCollider.enabled = true;
        }

        if (goal != null)
        {
            goal.localScale = goalDefaultScale;
        }

        if (goalSpriteRenderer != null)
        {
            SetGoalReachedDepth(false);
            RefreshGoalLockPresentation();
        }
    }

    private void SetGoalClickable(bool clickable)
    {
        isGoalClickable = clickable;
        SetGoalHoverOutline(clickable, false);
    }

    private bool IsPointerOverGoal(Vector2 screenPosition)
    {
        if (!isGoalClickable || goalSpriteRenderer == null)
        {
            return false;
        }

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            return false;
        }

        float depth = Mathf.Abs(
            worldCamera.transform.position.z - goalSpriteRenderer.transform.position.z);
        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, depth));
        worldPosition.z = goalSpriteRenderer.bounds.center.z;
        return goalSpriteRenderer.bounds.Contains(worldPosition);
    }

    private static bool IsPointerOverUi() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private void SetGoalReachedDepth(bool reached)
    {
        if (goalSpriteRenderer == null)
        {
            return;
        }

        goalSpriteRenderer.sortingOrder = reached
            ? goalReachedSortingOrder
            : goalDefaultSortingOrder;
    }

    private void CreateGoalHoverOutline()
    {
        DestroyGoalHoverOutline();
        if (goalSpriteRenderer == null)
        {
            return;
        }

        Shader outlineShader = blockManager != null
            ? blockManager.PlayModeHoverOutlineShader
            : null;
        outlineShader ??= Shader.Find("UIBuilder/SpriteSolidColor");
        if (outlineShader != null)
        {
            goalHoverOutlineMaterial = new Material(outlineShader)
            {
                name = "Goal Hover Outline",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        goalHoverOutlineRenderers = new SpriteRenderer[GoalHoverOutlineDirections.Length];
        for (int i = 0; i < goalHoverOutlineRenderers.Length; i++)
        {
            GameObject outlineObject = new GameObject($"Goal Hover Outline {i + 1}");
            outlineObject.layer = goalSpriteRenderer.gameObject.layer;
            SpriteRenderer outline = outlineObject.AddComponent<SpriteRenderer>();
            outline.sharedMaterial = goalHoverOutlineMaterial != null
                ? goalHoverOutlineMaterial
                : goalSpriteRenderer.sharedMaterial;
            outlineObject.SetActive(false);
            goalHoverOutlineRenderers[i] = outline;
        }
    }

    private void SetGoalHoverOutline(bool visible, bool hovered)
    {
        if (goalSpriteRenderer == null)
        {
            return;
        }

        float outlineWidth = blockManager != null
            ? blockManager.PlayModeHoverOutlineWidth
            : 0.06f;
        Color outlineColor = hovered
            ? blockManager != null
                ? blockManager.PlayModeHoverOutlineColor
                : new Color(1f, 0.5764706f, 0.5803922f, 1f)
            : Color.white;

        for (int i = 0; i < goalHoverOutlineRenderers.Length; i++)
        {
            SpriteRenderer outline = goalHoverOutlineRenderers[i];
            if (outline == null)
            {
                continue;
            }

            outline.sprite = goalSpriteRenderer.sprite;
            outline.color = outlineColor;
            outline.flipX = goalSpriteRenderer.flipX;
            outline.flipY = goalSpriteRenderer.flipY;
            outline.drawMode = goalSpriteRenderer.drawMode;
            outline.size = goalSpriteRenderer.size;
            outline.maskInteraction = goalSpriteRenderer.maskInteraction;
            outline.sortingLayerID = goalSpriteRenderer.sortingLayerID;
            outline.sortingOrder = goalSpriteRenderer.sortingOrder - 1;

            if (visible)
            {
                Vector2 offset = GoalHoverOutlineDirections[i] * outlineWidth;
                Transform sourceTransform = goalSpriteRenderer.transform;
                outline.transform.position = sourceTransform.position +
                                             new Vector3(offset.x, offset.y, 0f);
                outline.transform.rotation = sourceTransform.rotation;
                outline.transform.localScale = sourceTransform.lossyScale;
            }

            outline.gameObject.SetActive(
                visible &&
                goalSpriteRenderer.enabled &&
                goalSpriteRenderer.gameObject.activeInHierarchy);
        }
    }

    private void DestroyGoalHoverOutline()
    {
        foreach (SpriteRenderer outline in goalHoverOutlineRenderers)
        {
            if (outline != null)
            {
                Destroy(outline.gameObject);
            }
        }

        goalHoverOutlineRenderers = Array.Empty<SpriteRenderer>();
        if (goalHoverOutlineMaterial != null)
        {
            Destroy(goalHoverOutlineMaterial);
            goalHoverOutlineMaterial = null;
        }
    }

    private void OnValidate()
    {
        stageNumber = Mathf.Max(1, stageNumber);
        if (string.IsNullOrWhiteSpace(stageSceneNamePrefix))
        {
            stageSceneNamePrefix = "Stage";
        }

        reservedCellSize.x = Mathf.Max(1, reservedCellSize.x);
        reservedCellSize.y = Mathf.Max(1, reservedCellSize.y);
        goalReservedCellSize.x = Mathf.Max(1, goalReservedCellSize.x);
        goalReservedCellSize.y = Mathf.Max(2, goalReservedCellSize.y);
        keyReservedCellSize.x = Mathf.Max(1, keyReservedCellSize.x);
        keyReservedCellSize.y = Mathf.Max(1, keyReservedCellSize.y);
        keyFloatDistance = Mathf.Max(0f, keyFloatDistance);
        keyFloatHalfDuration = Mathf.Max(0.01f, keyFloatHalfDuration);
        keyCollectScale = Mathf.Max(1f, keyCollectScale);
        keyCollectDuration = Mathf.Max(0f, keyCollectDuration);
        playerAbsorbDuration = Mathf.Max(0f, playerAbsorbDuration);
        goalPopScale = Mathf.Max(1f, goalPopScale);
        goalPopDuration = Mathf.Max(0f, goalPopDuration);
        goalReachedSortingOrder = Mathf.Clamp(
            goalReachedSortingOrder,
            short.MinValue + 1,
            short.MaxValue);
        resultDisplayDelay = Mathf.Max(0f, resultDisplayDelay);
        resultMoveDuration = Mathf.Max(0f, resultMoveDuration);
        pauseMoveDuration = Mathf.Max(0f, pauseMoveDuration);
        deathIrisCloseDuration = Mathf.Max(0f, deathIrisCloseDuration);
        deathIrisClosedDuration = Mathf.Max(0f, deathIrisClosedDuration);
        deathIrisOpenDuration = Mathf.Max(0f, deathIrisOpenDuration);
        deathPopScale = Mathf.Max(1f, deathPopScale);
        deathPopDuration = Mathf.Max(0f, deathPopDuration);
        coverDuration = Mathf.Max(0f, coverDuration);
        fullCoverDuration = Mathf.Max(0f, fullCoverDuration);
        revealDuration = Mathf.Max(0f, revealDuration);
        FindMissingReferences(false);
        CacheRetryTilemapColliders();
        ApplyStageInformation();

        if (!Application.isPlaying)
        {
            if (transitionImage != null)
            {
                transitionImage.color = transitionColor;
            }

            ApplyPlayerStartPosition();
            ApplyGoalPosition();
            ApplyKeyPosition();
        }
    }

    private void FindMissingReferences(bool allowComponentCreation = true)
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

        if (key == null)
        {
            GameObject keyObject = GameObject.Find("Key");
            if (keyObject != null)
            {
                key = keyObject.transform;
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

        if (playerSpriteRenderer == null && player != null)
        {
            playerSpriteRenderer = player.GetComponent<SpriteRenderer>();
        }

        if (goalCollider == null && goal != null)
        {
            goalCollider = goal.GetComponent<Collider2D>();
        }

        if (goalSpriteRenderer == null && goal != null)
        {
            goalSpriteRenderer = goal.GetComponent<SpriteRenderer>();
        }

        if (keyCollider == null && key != null)
        {
            keyCollider = key.GetComponent<Collider2D>();
        }

        if (keySpriteRenderer == null && key != null)
        {
            keySpriteRenderer = key.GetComponent<SpriteRenderer>();
        }

        if (goalCollider != null)
        {
            goalCollider.isTrigger = true;
        }

        if (keyCollider != null)
        {
            keyCollider.isTrigger = true;
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
            resultBackground = FindRectTransformByName("ResultBG");
        }

        if (pauseBackground == null)
        {
            pauseBackground = FindRectTransformByName("PauseBG");
        }

        if (pauseStageText == null && pauseBackground != null)
        {
            pauseStageText = FindTextByName(pauseBackground, "Stage");
        }

        if (resultStageText == null && resultBackground != null)
        {
            resultStageText = FindTextByName(resultBackground, "Stage");
        }

        if (nextStageButton == null)
        {
            nextStageButton = FindButtonByName("NextButton");
        }

        if (pauseButton == null)
        {
            pauseButton = EnsureButton(FindRectTransformByName("PauseButton"), allowComponentCreation);
        }

        if (pauseBackButton == null)
        {
            pauseBackButton = EnsureButton(FindRectTransformByName("BackButton"), allowComponentCreation);
        }
    }

    private static RectTransform FindRectTransformByName(string objectName)
    {
        foreach (RectTransform rectTransform in FindObjectsByType<RectTransform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (rectTransform.name == objectName)
            {
                return rectTransform;
            }
        }

        return null;
    }

    private static TMP_Text FindTextByName(RectTransform parent, string objectName)
    {
        foreach (TMP_Text text in parent.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == objectName)
            {
                return text;
            }
        }

        return null;
    }

    private static Button FindButtonByName(string objectName)
    {
        foreach (Button button in FindObjectsByType<Button>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (button.name == objectName)
            {
                return button;
            }
        }

        return null;
    }

    private static Button EnsureButton(RectTransform rectTransform, bool allowComponentCreation)
    {
        if (rectTransform == null)
        {
            return null;
        }

        Button button = rectTransform.GetComponent<Button>();
        if (button == null && allowComponentCreation && Application.isPlaying)
        {
            button = rectTransform.gameObject.AddComponent<Button>();
            button.targetGraphic = rectTransform.GetComponent<Graphic>();
        }

        return button;
    }

    private void ConfigurePauseButtons()
    {
        pauseButton?.onClick.RemoveListener(ShowPause);
        pauseButton?.onClick.AddListener(ShowPause);
        pauseBackButton?.onClick.RemoveListener(HidePause);
        pauseBackButton?.onClick.AddListener(HidePause);
    }

    private void OnDestroy()
    {
        pauseButton?.onClick.RemoveListener(ShowPause);
        pauseBackButton?.onClick.RemoveListener(HidePause);
        nextStageButton?.onClick.RemoveListener(LoadNextStage);
        transitionTween?.Kill();
        deathIrisTween?.Kill();
        deathPopTween?.Kill();
        resultTween?.Kill();
        pauseTween?.Kill();
        playerAbsorbTween?.Kill();
        goalPopTween?.Kill();
        StopKeyAnimations();
        DestroyGoalHoverOutline();

        if (deathIrisMaterial != null)
        {
            Destroy(deathIrisMaterial);
            deathIrisMaterial = null;
        }

        if (IsPaused)
        {
            canvasManager?.SetPaused(false);
            Time.timeScale = timeScaleBeforePause;
        }
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
            Mathf.Max(2, goalReservedCellSize.y),
            0);
        Vector3 goalMin = stageTilemap.CellToWorld(goalMinCell);
        Vector3 goalMax = stageTilemap.CellToWorld(goalMaxCell);

        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireCube((goalMin + goalMax) * 0.5f, goalMax - goalMin);

        if (useKeyGimmick)
        {
            Vector3Int keyMinCell = new Vector3Int(keyCell.x, keyCell.y, 0);
            Vector3Int keyMaxCell = keyMinCell + new Vector3Int(
                Mathf.Max(1, keyReservedCellSize.x),
                Mathf.Max(1, keyReservedCellSize.y),
                0);
            Vector3 keyMin = stageTilemap.CellToWorld(keyMinCell);
            Vector3 keyMax = stageTilemap.CellToWorld(keyMaxCell);

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube((keyMin + keyMax) * 0.5f, keyMax - keyMin);
        }

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawLine(
            new Vector3(min.x, retryHeight, 0f),
            new Vector3(max.x, retryHeight, 0f));
    }
}

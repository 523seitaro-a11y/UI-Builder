using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ブロックのドラッグ状態に合わせて、画面上部のブロック一覧を移動させます。
/// UIアニメーションはこのクラスに集約します。
/// </summary>
public class CanvasManager : MonoBehaviour
{
    private const float GameStartPanelPositionY = 1000f;

    [Header("参照")]
    [Tooltip("ドラッグ状態を監視するBlockManagerです。")]
    [SerializeField] private BlockManager blockManager;

    [Header("ステージの配置完了条件")]
    [Tooltip("このステージで配置完了と判定するブロック数です。")]
    [Min(1)]
    [SerializeField] private int requiredPlacedBlockCount = 1;

    public bool AreRequiredBlocksPlaced =>
        blockManager != null &&
        blockManager.PlacedBlockCount >= Mathf.Max(1, requiredPlacedBlockCount);

    [Tooltip("ドラッグ中またはSwitch操作で上へ移動させる、画面上部のRectTransformです。")]
    [SerializeField] private RectTransform upperBlockPanel;

    [Tooltip("上部パネルの表示位置を切り替えるSwitchのRectTransformです。")]
    [SerializeField] private RectTransform panelSwitch;

    [Header("Switchの見た目")]
    [Tooltip("Switchに表示するImageです。")]
    [SerializeField] private Image panelSwitchImage;

    [Tooltip("パネルが下にあるとき、および上へ移動中に表示する画像です。")]
    [SerializeField] private Sprite upSprite;

    [Tooltip("パネルが上にあるとき、および下へ移動中に表示する画像です。")]
    [SerializeField] private Sprite downSprite;

    [Header("GameStartButtonのホバー表示")]
    [SerializeField] private Button gameStartButton;
    [SerializeField] private Image gameStartButtonImage;
    [SerializeField] private TMP_Text gameStartButtonText;
    [SerializeField] private CanvasGroup blockListGroup;
    [SerializeField] private CanvasGroup gameStartGroup;
    [SerializeField] private Color gameStartHoverColor = Color.black;
    [SerializeField] private Color gameStartHoverTextColor = Color.white;

    [Tooltip("カーソルを合わせたときのGameStartButtonのY方向拡大倍率です。")]
    [Min(1f)]
    [SerializeField] private float gameStartHoverScaleY = 1.08f;

    [Tooltip("GameStartButtonのYスケールが変化する時間です。")]
    [Min(0f)]
    [SerializeField] private float gameStartHoverScaleDuration = 0.12f;

    [SerializeField] private Ease gameStartHoverScaleEase = Ease.OutCubic;

    [Header("上部パネルの移動")]
    [Tooltip("ブロック一覧を表示し、ドラッグできるDown状態のAnchored Position Yです。ステージごとに調整できます。")]
    [SerializeField] private float downPanelPositionY = 1000f;

    [Tooltip("ブロック一覧を上へ退避したUp状態のAnchored Position Yです。")]
    [SerializeField] private float upPanelPositionY = 1180f;

    [Tooltip("上へ移動するときの時間です。短いほど素早く移動します。")]
    [Min(0f)]
    [SerializeField] private float moveUpDuration = 0.15f;

    [Tooltip("元の位置へ戻るときの時間です。")]
    [Min(0f)]
    [SerializeField] private float returnDuration = 0.18f;

    [SerializeField] private Ease moveUpEase = Ease.OutCubic;
    [SerializeField] private Ease returnEase = Ease.OutCubic;

    [Tooltip("ゲームのTime Scaleが0でもUIを動かします。")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("配置完了時のSwitchアニメーション")]
    [SerializeField] private bool animateSwitchWhenReady = true;

    [Min(1f)]
    [SerializeField] private float readySwitchPulseScale = 1.12f;

    [Min(0f)]
    [SerializeField] private float readySwitchPulseUpDuration = 0.08f;

    [Min(0f)]
    [SerializeField] private float readySwitchPulseDownDuration = 0.08f;

    [Min(0f)]
    [SerializeField] private float readySwitchPulseGap = 0.04f;

    [Min(0f)]
    [SerializeField] private float readySwitchRepeatDelay = 0.45f;

    [SerializeField] private Ease readySwitchPulseEase = Ease.OutCubic;

    private Vector2 originalAnchoredPosition;
    private Tweener panelTween;
    private bool hasOriginalPosition;
    private bool isDragging;
    private bool isManuallyRaised;
    private bool isBlockListVisible = true;
    private bool wereRequiredBlocksPlaced;
    private Color gameStartNormalColor;
    private Color gameStartNormalTextColor;
    private bool hasGameStartColors;
    private Vector3 gameStartNormalScale;
    private Vector3 gameStartTextNormalScale;
    private bool hasGameStartScale;
    private bool hasGameStartTextScale;
    private bool isGameStartHovered;
    private Tweener gameStartScaleTween;
    private bool isPaused;
    private Vector3 panelSwitchNormalScale;
    private Vector2 panelSwitchNormalAnchoredPosition;
    private bool hasPanelSwitchTransform;
    private Sequence readySwitchSequence;
    private bool isReadySwitchAnimating;

    private void Awake()
    {
        FindMissingReferences();
        CacheOriginalPosition();
        CacheGameStartColors();
        CacheGameStartScale();
        CachePanelSwitchTransform();
    }

    private void OnEnable()
    {
        FindMissingReferences();
        CacheOriginalPosition();
        CacheGameStartColors();
        CacheGameStartScale();
        CachePanelSwitchTransform();

        if (blockManager != null)
        {
            blockManager.DragStateChanged += HandleDragStateChanged;
            isDragging = blockManager.IsDragging;
            wereRequiredBlocksPlaced = AreRequiredBlocksPlaced;

            SetPanelPosition(ShouldRaisePanel, false);
            ApplyPanelContentVisibility();
        }
    }

    private void Update()
    {
        if (isPaused)
        {
            return;
        }

        UpdateRequiredBlocksPlacedState();
        UpdateGameStartButtonHover(Input.mousePosition, Input.touchCount == 0);
        UpdateReadySwitchAnimation();

        if (panelSwitch == null || !TryGetPointerDownPosition(out Vector2 screenPosition))
        {
            return;
        }

        if (RectTransformUtility.RectangleContainsScreenPoint(
                panelSwitch,
                screenPosition,
                GetUiCamera(panelSwitch)))
        {
            ToggleUpperPanel();
        }
    }

    private void OnDisable()
    {
        if (blockManager != null)
        {
            blockManager.DragStateChanged -= HandleDragStateChanged;
        }

        panelTween?.Kill();
        panelTween = null;

        if (upperBlockPanel != null && hasOriginalPosition)
        {
            upperBlockPanel.anchoredPosition = originalAnchoredPosition;
        }

        ResetGameStartHover();
        StopReadySwitchAnimation();
    }

    private void HandleDragStateChanged(bool isDragging)
    {
        this.isDragging = isDragging;

        if (isPaused)
        {
            return;
        }

        SetPanelPosition(ShouldRaisePanel, true);
    }

    private void UpdateRequiredBlocksPlacedState()
    {
        if (blockManager == null)
        {
            return;
        }

        bool placementComplete = AreRequiredBlocksPlaced;
        if (wereRequiredBlocksPlaced == placementComplete)
        {
            return;
        }

        wereRequiredBlocksPlaced = placementComplete;
        if (placementComplete)
        {
            // 表示内容だけをSTARTへ切り替える。手動で選んだup/down状態は維持する。
            isBlockListVisible = false;
        }
        else
        {
            isBlockListVisible = true;
        }

        SetPanelPosition(ShouldRaisePanel, true);
        ApplyPanelContentVisibility();
    }

    /// <summary>
    /// Switch操作で上部パネルの表示位置を切り替えます。
    /// </summary>
    public void ToggleUpperPanel()
    {
        bool wasRaised = ShouldRaisePanel;
        isManuallyRaised = !isManuallyRaised;

        bool raisePanel = ShouldRaisePanel;

        if (wasRaised != raisePanel)
        {
            if (raisePanel)
            {
                AudioManager.Instance?.PlayUpperPanelRaiseSound();
            }
            else
            {
                AudioManager.Instance?.PlayUpperPanelLowerSound();
            }
        }
        SetPanelPosition(ShouldRaisePanel, true);
        ApplyPanelContentVisibility();
    }

    /// <summary>
    /// 現在のパネル位置を維持したまま、CanvasManagerの入力処理だけを停止します。
    /// </summary>
    public void SetPaused(bool paused)
    {
        isPaused = paused;

        if (paused)
        {
            ResetGameStartHover();
            StopReadySwitchAnimation();
        }
    }

    private bool ShouldRaisePanel => isDragging || isManuallyRaised;

    private void SetPanelPosition(bool raisePanel, bool animate)
    {
        if (upperBlockPanel == null || !hasOriginalPosition)
        {
            return;
        }

        panelTween?.Kill();
        Vector2 targetPosition = originalAnchoredPosition;
        bool showsGameStart = AreRequiredBlocksPlaced && !isBlockListVisible;
        if (raisePanel)
        {
            targetPosition.y = upPanelPositionY;
        }
        else if (showsGameStart)
        {
            targetPosition.y = GameStartPanelPositionY;
        }
        else
        {
            targetPosition.y = downPanelPositionY;
        }

        ApplyPanelContentVisibility();

        if (!animate || Vector2.SqrMagnitude(upperBlockPanel.anchoredPosition - targetPosition) < 0.01f)
        {
            upperBlockPanel.anchoredPosition = targetPosition;
            ApplySwitchSprite(useUpSprite: !raisePanel);
            return;
        }

        // 下にいる間と上昇中はUp、それ以外（上にいる間と下降中）はDownです。
        ApplySwitchSprite(useUpSprite: targetPosition.y > upperBlockPanel.anchoredPosition.y);

        float duration = raisePanel ? moveUpDuration : returnDuration;
        Ease ease = raisePanel ? moveUpEase : returnEase;
        panelTween = upperBlockPanel
            .DOAnchorPos(targetPosition, duration)
            .SetEase(ease)
            .SetUpdate(useUnscaledTime)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                ApplySwitchSprite(useUpSprite: !raisePanel);
            });
    }

    private void ApplySwitchSprite(bool useUpSprite)
    {
        if (panelSwitchImage == null)
        {
            return;
        }

        Sprite targetSprite = useUpSprite ? upSprite : downSprite;
        if (targetSprite != null)
        {
            panelSwitchImage.sprite = targetSprite;
        }
    }

    private void ApplyPanelContentVisibility()
    {
        bool placementComplete = AreRequiredBlocksPlaced;
        bool showBlockList = !placementComplete || isBlockListVisible;
        bool showGameStart = placementComplete && !isBlockListVisible;
        SetCanvasGroupVisible(blockListGroup, showBlockList);
        SetCanvasGroupVisible(gameStartGroup, showGameStart);
        if (!showGameStart)
        {
            ResetGameStartHover();
        }
    }

    private void UpdateReadySwitchAnimation()
    {
        bool shouldAnimate =
            !isPaused &&
            animateSwitchWhenReady &&
            blockManager != null &&
            blockManager.IsBuildMode &&
            AreRequiredBlocksPlaced &&
            ShouldRaisePanel &&
            panelSwitch != null &&
            panelSwitch.gameObject.activeInHierarchy;

        if (shouldAnimate)
        {
            StartReadySwitchAnimation();
        }
        else
        {
            StopReadySwitchAnimation();
        }
    }

    private void StartReadySwitchAnimation()
    {
        if (isReadySwitchAnimating)
        {
            return;
        }

        CachePanelSwitchTransform();
        if (!hasPanelSwitchTransform || panelSwitch == null)
        {
            return;
        }

        isReadySwitchAnimating = true;
        Vector3 pulseScale = panelSwitchNormalScale * Mathf.Max(1f, readySwitchPulseScale);

        readySwitchSequence?.Kill();
        readySwitchSequence = DOTween.Sequence()
            .SetUpdate(useUnscaledTime)
            .SetLink(gameObject)
            .Append(panelSwitch.DOScale(pulseScale, readySwitchPulseUpDuration)
                .SetEase(readySwitchPulseEase))
            .Append(panelSwitch.DOScale(panelSwitchNormalScale, readySwitchPulseDownDuration)
                .SetEase(readySwitchPulseEase))
            .AppendInterval(readySwitchPulseGap)
            .Append(panelSwitch.DOScale(pulseScale, readySwitchPulseUpDuration)
                .SetEase(readySwitchPulseEase))
            .Append(panelSwitch.DOScale(panelSwitchNormalScale, readySwitchPulseDownDuration)
                .SetEase(readySwitchPulseEase))
            .AppendInterval(readySwitchRepeatDelay)
            .SetLoops(-1)
            .OnUpdate(KeepPanelSwitchTopEdgeFixed);
    }

    private void StopReadySwitchAnimation()
    {
        if (!isReadySwitchAnimating && readySwitchSequence == null)
        {
            return;
        }

        readySwitchSequence?.Kill();
        readySwitchSequence = null;
        isReadySwitchAnimating = false;
        RestorePanelSwitchTransform();
    }

    private void CachePanelSwitchTransform()
    {
        if (hasPanelSwitchTransform || panelSwitch == null)
        {
            return;
        }

        panelSwitchNormalScale = panelSwitch.localScale;
        panelSwitchNormalAnchoredPosition = panelSwitch.anchoredPosition;
        hasPanelSwitchTransform = true;
    }

    private void RestorePanelSwitchTransform()
    {
        if (!hasPanelSwitchTransform || panelSwitch == null)
        {
            return;
        }

        panelSwitch.localScale = panelSwitchNormalScale;
        panelSwitch.anchoredPosition = panelSwitchNormalAnchoredPosition;
    }

    private void KeepPanelSwitchTopEdgeFixed()
    {
        if (!hasPanelSwitchTransform || panelSwitch == null)
        {
            return;
        }

        float height = panelSwitch.rect.height;
        float topOffsetAtBase =
            (1f - panelSwitch.pivot.y) * height * panelSwitchNormalScale.y;
        float topOffsetNow =
            (1f - panelSwitch.pivot.y) * height * panelSwitch.localScale.y;

        Vector2 anchoredPosition = panelSwitchNormalAnchoredPosition;
        anchoredPosition.y -= topOffsetNow - topOffsetAtBase;
        panelSwitch.anchoredPosition = anchoredPosition;
    }

    private static void SetCanvasGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    private void UpdateGameStartButtonHover(Vector2 screenPosition, bool allowHover)
    {
        bool hovered = allowHover &&
                       gameStartButton != null &&
                       gameStartButton.gameObject.activeInHierarchy &&
                       gameStartButton.interactable &&
                       RectTransformUtility.RectangleContainsScreenPoint(
                           gameStartButton.transform as RectTransform,
                           screenPosition,
                           GetUiCamera(gameStartButton.transform as RectTransform));
        ApplyGameStartHover(hovered);
    }

    private void CacheGameStartColors()
    {
        if (hasGameStartColors ||
            gameStartButton == null ||
            gameStartButtonImage == null ||
            gameStartButtonText == null)
        {
            return;
        }

        gameStartNormalColor = gameStartButtonImage.color;
        gameStartNormalTextColor = gameStartButtonText.color;
        gameStartButton.transition = Selectable.Transition.None;
        hasGameStartColors = true;
    }

    private void ApplyGameStartColors(bool hovered)
    {
        if (!hasGameStartColors)
        {
            return;
        }

        gameStartButtonImage.color = hovered ? gameStartHoverColor : gameStartNormalColor;
        gameStartButtonText.color = hovered ? gameStartHoverTextColor : gameStartNormalTextColor;
    }

    private void CacheGameStartScale()
    {
        if (!hasGameStartScale && gameStartButton != null)
        {
            gameStartNormalScale = gameStartButton.transform.localScale;
            hasGameStartScale = true;
        }

        if (!hasGameStartTextScale && gameStartButtonText != null)
        {
            gameStartTextNormalScale = gameStartButtonText.transform.localScale;
            hasGameStartTextScale = true;
        }
    }

    private void ApplyGameStartHover(bool hovered)
    {
        ApplyGameStartColors(hovered);

        if (!hasGameStartScale ||
            gameStartButton == null ||
            isGameStartHovered == hovered)
        {
            return;
        }

        isGameStartHovered = hovered;
        Vector3 targetScale = gameStartNormalScale;
        if (hovered)
        {
            targetScale.y *= Mathf.Max(1f, gameStartHoverScaleY);
        }

        gameStartScaleTween?.Kill();
        gameStartScaleTween = null;

        if (gameStartHoverScaleDuration <= 0f)
        {
            gameStartButton.transform.localScale = targetScale;
            ApplyGameStartTextCounterScale();
            return;
        }

        gameStartScaleTween = gameStartButton.transform
            .DOScale(targetScale, gameStartHoverScaleDuration)
            .SetEase(gameStartHoverScaleEase)
            .SetUpdate(useUnscaledTime)
            .SetLink(gameObject)
            .OnUpdate(ApplyGameStartTextCounterScale)
            .OnComplete(() =>
            {
                ApplyGameStartTextCounterScale();
                gameStartScaleTween = null;
            });
    }

    private void ApplyGameStartTextCounterScale()
    {
        if (!hasGameStartScale ||
            !hasGameStartTextScale ||
            gameStartButton == null ||
            gameStartButtonText == null)
        {
            return;
        }

        float currentButtonScaleY = gameStartButton.transform.localScale.y;
        float inverseButtonScaleY = Mathf.Approximately(currentButtonScaleY, 0f)
            ? 1f
            : gameStartNormalScale.y / currentButtonScaleY;

        Vector3 textScale = gameStartTextNormalScale;
        textScale.y *= inverseButtonScaleY;
        gameStartButtonText.transform.localScale = textScale;
    }

    private void ResetGameStartHover()
    {
        gameStartScaleTween?.Kill();
        gameStartScaleTween = null;
        isGameStartHovered = false;
        ApplyGameStartColors(false);

        if (hasGameStartScale && gameStartButton != null)
        {
            gameStartButton.transform.localScale = gameStartNormalScale;
        }

        if (hasGameStartTextScale && gameStartButtonText != null)
        {
            gameStartButtonText.transform.localScale = gameStartTextNormalScale;
        }
    }

    private void CacheOriginalPosition()
    {
        if (upperBlockPanel == null || hasOriginalPosition)
        {
            return;
        }

        originalAnchoredPosition = upperBlockPanel.anchoredPosition;
        hasOriginalPosition = true;
    }

    private void FindMissingReferences()
    {
        if (blockManager == null)
        {
            blockManager = FindFirstObjectByType<BlockManager>();
        }

        if (upperBlockPanel == null)
        {
            GameObject panelObject = GameObject.Find("BlockBG");
            if (panelObject != null)
            {
                upperBlockPanel = panelObject.GetComponent<RectTransform>();
            }
        }

        if (panelSwitch == null)
        {
            GameObject switchObject = GameObject.Find("Switch");
            if (switchObject != null)
            {
                panelSwitch = switchObject.GetComponent<RectTransform>();
            }
        }

        if (panelSwitchImage == null && panelSwitch != null)
        {
            panelSwitchImage = panelSwitch.GetComponent<Image>();
        }

        if (gameStartButton == null && upperBlockPanel != null)
        {
            gameStartButton = upperBlockPanel.GetComponentInChildren<Button>(true);
        }

        if (gameStartButtonImage == null && gameStartButton != null)
        {
            gameStartButtonImage = gameStartButton.GetComponent<Image>();
        }

        if (gameStartButtonText == null && gameStartButton != null)
        {
            gameStartButtonText = gameStartButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (blockListGroup == null)
        {
            GameObject blockListObject = GameObject.Find("BlockListRoot");
            if (blockListObject != null)
            {
                blockListGroup = blockListObject.GetComponent<CanvasGroup>();
            }
        }

        if (gameStartGroup == null && gameStartButton != null)
        {
            gameStartGroup = gameStartButton.GetComponent<CanvasGroup>();
        }
    }

    private static bool TryGetPointerDownPosition(out Vector2 screenPosition)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            screenPosition = touch.position;
            return touch.phase == TouchPhase.Began;
        }

        screenPosition = Input.mousePosition;
        return Input.GetMouseButtonDown(0);
    }

    private static Camera GetUiCamera(RectTransform target)
    {
        Canvas canvas = target.GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
    }

    private void OnValidate()
    {
        moveUpDuration = Mathf.Max(0f, moveUpDuration);
        returnDuration = Mathf.Max(0f, returnDuration);
        gameStartHoverScaleY = Mathf.Max(1f, gameStartHoverScaleY);
        gameStartHoverScaleDuration = Mathf.Max(0f, gameStartHoverScaleDuration);
        requiredPlacedBlockCount = Mathf.Max(1, requiredPlacedBlockCount);
        readySwitchPulseScale = Mathf.Max(1f, readySwitchPulseScale);
        readySwitchPulseUpDuration = Mathf.Max(0f, readySwitchPulseUpDuration);
        readySwitchPulseDownDuration = Mathf.Max(0f, readySwitchPulseDownDuration);
        readySwitchPulseGap = Mathf.Max(0f, readySwitchPulseGap);
        readySwitchRepeatDelay = Mathf.Max(0f, readySwitchRepeatDelay);
    }
}

using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>配置はステージ共通のBlockManager/CanvasManagerに任せ、タイトルの開始だけを制御します。</summary>
public sealed class TitleBuildManager : MonoBehaviour
{
    private static BlockManager.PlacementSnapshot[] startedLayout;

    // Domain Reloadを無効にしたEditorでも、新しいゲーム起動はビルドモードから始めます。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => startedLayout = null;

    [SerializeField] private BlockManager blockManager;
    [SerializeField] private CanvasManager canvasManager;
    [SerializeField] private GameObject blockBackground;
    [SerializeField] private GameObject gridBackground;
    [SerializeField] private GameObject dragHint;
    [SerializeField] private Sprite dropHintSprite;
    private RectTransform dropHint;
    [SerializeField] private Button gameStartButton;
    [SerializeField] private RectTransform transitionOverlay;
    [Header("タイトルロゴ")]
    [SerializeField] private SpriteRenderer titleLogo;
    [SerializeField, Range(0f, 1f)] private float buildLogoOpacity = 0.25f;
    [Tooltip("左からA=1、上から1のセル範囲。幅・高さはセル数です。")]
    [SerializeField] private RectInt[] logoOverlapCells =
    {
        new RectInt(4, 1, 3, 1), // D1-F1
        new RectInt(4, 2, 10, 2), // D2-M3
        new RectInt(4, 4, 4, 1) // D4-G4
    };
    private Sequence startTransition;
    [Header("タイトルのプレイヤー")]
    [SerializeField] private Rigidbody2D titlePlayer;
    [SerializeField] private Camera titleCamera;
    private Tween playerDropDelay;
    [Header("タイトルとステージ選択の横スライド")]
    [SerializeField] private RectTransform stageSelectCanvas;
    [SerializeField] private RectTransform titleUiCanvas;
    [SerializeField, Min(0.01f)] private float pageSlideDuration = 0.6f;
    [SerializeField] private ButtonHoverOutline backButtonOutline;
    private CanvasGroup stageSelectInput;
    private RectTransform[] stageItems;
    private Vector2[] stagePositions;
    private RectTransform[] titleItems;
    private Vector2[] titlePositions;
    private Vector3 titleCameraPosition;
    private Tween pageSlide;
    private float pageOffset;
    private bool stageSelectOpen;

    private void InitializeStageSelect()
    {
        if (stageSelectCanvas == null || titleCamera == null) return;
        titleCameraPosition = titleCamera.transform.position;
        stageSelectInput = stageSelectCanvas.gameObject.AddComponent<CanvasGroup>();
        // Canvas直下には装飾用SpriteRendererの通常Transformも置けます。
        // UIだけをスライド補正し、ワールドの装飾はカメラ移動に任せます。
        stageItems = GetDirectUiChildren(stageSelectCanvas);
        stagePositions = new Vector2[stageItems.Length];
        for (int i = 0; i < stageItems.Length; i++)
            stagePositions[i] = stageItems[i].anchoredPosition + Vector2.right * 1920f;
        titleItems = GetDirectUiChildren(titleUiCanvas);
        titlePositions = new Vector2[titleItems.Length];
        for (int i = 0; i < titleItems.Length; i++)
            titlePositions[i] = titleItems[i].anchoredPosition;
        SetStageSelectInput(false);
        Canvas.ForceUpdateCanvases();
        UpdatePagePositions();
    }

    private static RectTransform[] GetDirectUiChildren(RectTransform parent)
    {
        var items = new List<RectTransform>();
        foreach (Transform child in parent)
            if (child is RectTransform rect) items.Add(rect);
        return items.ToArray();
    }

    public void OpenStageSelect()
    {
        if (blockManager.IsBuildMode || stageSelectOpen || pageSlide != null ||
            ScreenTransition.IsTransitioning || stageSelectCanvas == null) return;
        for (int i = 0; i < titleItems.Length; i++)
            titlePositions[i] = titleItems[i].anchoredPosition;
        stageSelectOpen = true;
        blockManager.enabled = false;
        blockManager.ShowTitlePlayButtonOutline();
        SlidePage(1f);
    }

    public void CloseStageSelect()
    {
        if (!stageSelectOpen || pageSlide != null || ScreenTransition.IsTransitioning) return;
        stageSelectOpen = false;
        backButtonOutline?.HoldOutlineUntilTransitionEnds(true);
        SlidePage(0f);
    }

    private void SetStageSelectInput(bool enabled)
    {
        stageSelectInput.interactable = enabled;
        stageSelectInput.blocksRaycasts = enabled;
    }

    private void SlidePage(float destination)
    {
        SetStageSelectInput(false);
        pageSlide = DOTween.To(() => pageOffset, value => pageOffset = value,
                destination, pageSlideDuration)
            .SetEase(Ease.InOutCubic).SetUpdate(true).SetLink(gameObject)
            .OnComplete(() =>
            {
                UpdatePagePositions();
                pageSlide = null;
                SetStageSelectInput(stageSelectOpen);
                if (stageSelectOpen) blockManager.HideTitlePlayButtonOutline();
                else backButtonOutline?.HoldOutlineUntilTransitionEnds(false);
                blockManager.enabled = !stageSelectOpen;
            });
    }

    private void UpdatePagePositions()
    {
        if (stageItems == null) return;
        float stageShift = -(1f - pageOffset) * stageSelectCanvas.rect.width;
        for (int i = 0; i < stageItems.Length; i++)
            stageItems[i].anchoredPosition = stagePositions[i] + Vector2.right * stageShift;
        // タイトルのUIは配置モードでも動くため、移動中と選択画面表示中だけ補正します。
        if (pageSlide != null || pageOffset > 0f)
            for (int i = 0; i < titleItems.Length; i++)
                titleItems[i].anchoredPosition = titlePositions[i]
                    + Vector2.right * (pageOffset * titleUiCanvas.rect.width);
        titleCamera.transform.position = titleCameraPosition
            - Vector3.right * (pageOffset * titleCamera.orthographicSize * 2f * titleCamera.aspect);
    }
    private Sequence dragHintPulse;
    private Sequence dropHintPulse;
    private Vector3 dragHintBaseScale;
    private bool placementCompleted;
    private bool starting;

    private void Awake()
    {
        if (titlePlayer != null)
        {
            titlePlayer.simulated = false;
            titlePlayer.gameObject.SetActive(false);
        }
        if (dragHint != null)
        {
            dragHintBaseScale = dragHint.transform.localScale;
            dragHint.SetActive(false);
        }
        if (titleLogo != null)
        {
            Color color = titleLogo.color;
            color.a = buildLogoOpacity;
            titleLogo.color = color;
            SpriteRenderer gridRenderer = gridBackground != null
                ? gridBackground.GetComponent<SpriteRenderer>() : null;
            titleLogo.sortingLayerID = gridRenderer != null ? gridRenderer.sortingLayerID : 0;
            titleLogo.sortingOrder = gridRenderer != null ? gridRenderer.sortingOrder - 1 : -2;
        }
        if (startedLayout == null) AudioManager.Instance?.StopBgm();
        transitionOverlay.gameObject.SetActive(false);
        gameStartButton.onClick.AddListener(StartTitle);
        InitializeStageSelect();
    }

    private void Start()
    {
        if (startedLayout == null)
        {
            // Sceneビューで調整した実体の位置を初期配置として使います。
            blockManager.RestoreScenePlacements();
            return;
        }

        // 他コンポーネントのAwakeでテンプレート生成が完了してから復元します。
        blockManager.RestorePlacementLayout(startedLayout);
        starting = true;
        canvasManager.SetPaused(true);
        EnterPlayMode();
    }

    private void LateUpdate()
    {
        UpdatePagePositions();
        UpdateDragHint();
        UpdateDropHint();
        if (titleLogo == null || blockManager == null) return;
        Color color = titleLogo.color;
        color.a = blockManager.IsBuildMode ? buildLogoOpacity
            : blockManager.HasForegroundBlockOutlineInCells(logoOverlapCells) ? 0.25f : 1f;
        titleLogo.color = color;
    }

    private void UpdateDragHint()
    {
        if (dragHint == null || blockManager == null || canvasManager == null) return;
        placementCompleted |= canvasManager.AreRequiredBlocksPlaced;
        bool visible = !starting && blockManager.IsBuildMode &&
                       !blockManager.IsDragging && !placementCompleted;
        if (!visible)
        {
            StopDragHint();
            return;
        }
        if (dragHintPulse != null) return;

        dragHint.SetActive(true);
        dragHintPulse = CreateHintPulse(dragHint.transform, dragHintBaseScale);
    }

    private Sequence CreateHintPulse(Transform hint, Vector3 baseScale)
    {
        Vector3 pulseScale = baseScale * 1.12f;
        // Switchと同じ、2回の拡大・縮小と短い待機を繰り返します。
        return DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Append(hint.DOScale(pulseScale, 0.08f).SetEase(Ease.OutCubic))
            .Append(hint.DOScale(baseScale, 0.08f).SetEase(Ease.OutCubic))
            .AppendInterval(0.04f)
            .Append(hint.DOScale(pulseScale, 0.08f).SetEase(Ease.OutCubic))
            .Append(hint.DOScale(baseScale, 0.08f).SetEase(Ease.OutCubic))
            .AppendInterval(0.45f)
            .SetLoops(-1);
    }

    private void StopDragHint()
    {
        dragHintPulse?.Kill();
        dragHintPulse = null;
        if (dragHint == null) return;
        dragHint.transform.localScale = dragHintBaseScale;
        dragHint.SetActive(false);
    }

    private void UpdateDropHint()
    {
        if (starting || blockManager == null || !blockManager.isActiveAndEnabled ||
            !blockManager.TryGetDraggedBlockBottomScreenPosition(out Vector2 screenPosition))
        {
            StopDropHint();
            return;
        }
        if (dropHint == null)
        {
            if (dragHint == null || dropHintSprite == null) return;
            // Dragと同じ最前面Canvas設定を使い、入力を妨げずに表示します。
            GameObject hint = Instantiate(dragHint, dragHint.transform.parent);
            hint.name = "Drop";
            dropHint = hint.GetComponent<RectTransform>();
            dropHint.localScale = Vector3.one;
            dropHint.anchorMin = dropHint.anchorMax = new Vector2(0.5f, 0.5f);
            dropHint.pivot = new Vector2(0.5f, 1f);
            dropHint.sizeDelta = new Vector2(240f, 120f);
            Image image = hint.GetComponent<Image>();
            image.sprite = dropHintSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
        RectTransform parent = (RectTransform)dropHint.parent;
        Canvas canvas = parent.GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, camera, out Vector2 local);
        dropHint.anchoredPosition = local - parent.rect.center + Vector2.down * 12f;
        dropHint.gameObject.SetActive(true);
        if (dropHintPulse == null)
            dropHintPulse = CreateHintPulse(dropHint, Vector3.one);
    }

    private void StopDropHint()
    {
        dropHintPulse?.Kill();
        dropHintPulse = null;
        if (dropHint == null) return;
        dropHint.localScale = Vector3.one;
        dropHint.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        StopDragHint();
        StopDropHint();
    }

    private void EnterPlayMode()
    {
        StopDragHint();
        StopDropHint();
        if (titleLogo != null)
        {
            Color color = titleLogo.color;
            color.a = 1f;
            titleLogo.color = color;
            titleLogo.sortingLayerName = "UIForeground";
            titleLogo.sortingOrder = 5;
        }
        blockManager.SetBuildMode(false);
        blockBackground.SetActive(false);
        gridBackground.SetActive(false);
        AudioManager.Instance?.PlayBgm();
        playerDropDelay?.Kill();
        playerDropDelay = DOVirtual.DelayedCall(1f, DropPlayer, false).SetLink(gameObject);
    }

    private void DropPlayer()
    {
        if (titlePlayer == null || titleCamera == null || blockManager.IsBuildMode) return;
        // 非アクティブ時のRigidbody2D.positionは、再有効化時に保存済みTransformで
        // 上書きされるため、Awake/OnEnableと物理ボディ生成の後で出現位置を設定します。
        titlePlayer.simulated = false;
        titlePlayer.gameObject.SetActive(true);
        float playerZ = titlePlayer.transform.position.z;
        float depth = Mathf.Abs(titleCamera.transform.position.z - playerZ);
        Vector3 topCenter = titleCamera.ViewportToWorldPoint(new Vector3(0.5f, 1f, depth));
        SpriteRenderer renderer = titlePlayer.GetComponent<SpriteRenderer>();
        if (renderer != null && titleLogo != null)
        {
            renderer.sortingLayerID = titleLogo.sortingLayerID;
            renderer.sortingOrder = titleLogo.sortingOrder - 1;
        }
        float bottomOffset = renderer != null
            ? titlePlayer.transform.position.y - renderer.bounds.min.y : 0.5f;
        Vector3 spawnPosition = new Vector3(titleCameraPosition.x, topCenter.y + bottomOffset + 0.1f, playerZ);
        titlePlayer.transform.position = spawnPosition;
        titlePlayer.position = spawnPosition;
        titlePlayer.linearVelocity = Vector2.zero;
        titlePlayer.angularVelocity = 0f;
        Physics2D.SyncTransforms();
        titlePlayer.simulated = true;
    }

    private void StartTitle()
    {
        if (starting || !blockManager.IsBuildMode || blockManager.IsDragging ||
            !canvasManager.AreRequiredBlocksPlaced) return;
        starting = true;
        canvasManager.SetPaused(true);
        blockManager.enabled = false;
        AudioManager.Instance?.PlayGameStartSound();
        transitionOverlay.gameObject.SetActive(true);
        transitionOverlay.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        float height = transitionOverlay.rect.height;
        transitionOverlay.anchoredPosition = Vector2.up * height;
        startTransition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Append(transitionOverlay.DOAnchorPosY(0f, 0.25f).SetEase(Ease.InOutCubic))
            .AppendCallback(() =>
            {
                startedLayout = blockManager.CapturePlacementLayout();
                EnterPlayMode();
            })
            .AppendInterval(0.2f)
            .Append(transitionOverlay.DOAnchorPosY(-height, 0.25f).SetEase(Ease.InOutCubic))
            .OnComplete(() =>
            {
                transitionOverlay.gameObject.SetActive(false);
                blockManager.enabled = true;
            });
    }

    private void OnDestroy()
    {
        backButtonOutline?.HoldOutlineUntilTransitionEnds(false);
        pageSlide?.Kill();
        playerDropDelay?.Kill();
        StopDropHint();
        if (dropHint != null) Destroy(dropHint.gameObject);
        StopDragHint();
        startTransition?.Kill();
        if (gameStartButton != null) gameStartButton.onClick.RemoveListener(StartTitle);
    }
}

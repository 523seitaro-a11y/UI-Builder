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
    [Header("参照")]
    [Tooltip("ドラッグ状態を監視するBlockManagerです。")]
    [SerializeField] private BlockManager blockManager;

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
    [SerializeField] private Color gameStartHoverColor = Color.black;
    [SerializeField] private Color gameStartHoverTextColor = Color.white;

    [Header("上部パネルの移動")]
    [Tooltip("ドラッグ中に上方向へ移動する距離です。")]
    [Min(0f)]
    [SerializeField] private float upwardMoveDistance = 220f;

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

    private Vector2 originalAnchoredPosition;
    private Tweener panelTween;
    private bool hasOriginalPosition;
    private bool isDragging;
    private bool isManuallyRaised;
    private Color gameStartNormalColor;
    private Color gameStartNormalTextColor;
    private bool hasGameStartColors;

    private void Awake()
    {
        FindMissingReferences();
        CacheOriginalPosition();
        CacheGameStartColors();
    }

    private void OnEnable()
    {
        FindMissingReferences();
        CacheOriginalPosition();
        CacheGameStartColors();

        if (blockManager != null)
        {
            blockManager.DragStateChanged += HandleDragStateChanged;
            isDragging = blockManager.IsDragging;
            SetPanelPosition(ShouldRaisePanel, false);
        }
    }

    private void Update()
    {
        UpdateGameStartButtonHover(Input.mousePosition, Input.touchCount == 0);

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

        ApplyGameStartColors(false);
    }

    private void HandleDragStateChanged(bool isDragging)
    {
        this.isDragging = isDragging;
        SetPanelPosition(ShouldRaisePanel, true);
    }

    /// <summary>
    /// Switch操作で上部パネルの表示位置を切り替えます。
    /// </summary>
    public void ToggleUpperPanel()
    {
        isManuallyRaised = !isManuallyRaised;
        SetPanelPosition(ShouldRaisePanel, true);
    }

    private bool ShouldRaisePanel => isDragging || isManuallyRaised;

    private void SetPanelPosition(bool raisePanel, bool animate)
    {
        if (upperBlockPanel == null || !hasOriginalPosition)
        {
            return;
        }

        panelTween?.Kill();
        Vector2 targetPosition = originalAnchoredPosition +
                                 (raisePanel ? Vector2.up * upwardMoveDistance : Vector2.zero);

        if (!animate || Vector2.SqrMagnitude(upperBlockPanel.anchoredPosition - targetPosition) < 0.01f)
        {
            upperBlockPanel.anchoredPosition = targetPosition;
            ApplySwitchSprite(useUpSprite: !raisePanel);
            return;
        }

        // 下にいる間と上昇中はUp、それ以外（上にいる間と下降中）はDownです。
        ApplySwitchSprite(useUpSprite: raisePanel);

        float duration = raisePanel ? moveUpDuration : returnDuration;
        Ease ease = raisePanel ? moveUpEase : returnEase;
        panelTween = upperBlockPanel
            .DOAnchorPos(targetPosition, duration)
            .SetEase(ease)
            .SetUpdate(useUnscaledTime)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                if (ShouldRaisePanel == raisePanel)
                {
                    ApplySwitchSprite(useUpSprite: !raisePanel);
                }
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
        ApplyGameStartColors(hovered);
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
        upwardMoveDistance = Mathf.Max(0f, upwardMoveDistance);
        moveUpDuration = Mathf.Max(0f, moveUpDuration);
        returnDuration = Mathf.Max(0f, returnDuration);
    }
}

using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class MainManager : MonoBehaviour
{
    [Header("リトライ入力")]
    [Tooltip("プレイモードからビルドモードへ戻るキーです。")]
    [SerializeField] private KeyCode retryKey = KeyCode.R;

    [SerializeField] private StageManager stageManager;
    [SerializeField] private BlockManager blockManager;

    [Tooltip("空白を押し続けてからリトライするまでの秒数です。")]
    [Min(0f)]
    [SerializeField] private float longPressDuration = 0.8f;

    [Header("長押し中のRetry表示")]
    [SerializeField] private Sprite retryCursorSprite;

    [Tooltip("画面座標をワールド座標へ変換するカメラです。未設定ならMain Cameraを使用します。")]
    [SerializeField] private Camera pointerCamera;

    [Tooltip("カーソル位置からRetry表示へ加えるワールド座標の補正です。")]
    [SerializeField] private Vector2 retryCursorOffset;

    [Tooltip("Retry表示のワールド座標上のZ値です。")]
    [SerializeField] private float retryCursorZ = -5f;

    [SerializeField] private Vector3 retryCursorScale = Vector3.one;
    [SerializeField] private string retryCursorSortingLayer = "Default";
    [SerializeField] private int retryCursorSortingOrder = 100;

    [Header("長押しアニメーション")]
    [Tooltip("長押し開始時のRetry画像の透明度です。")]
    [Range(0f, 1f)]
    [SerializeField] private float retryFadedAlpha = 0.2f;

    [Tooltip("円状の透過解除に使用するEaseです。Linearなら時間と表示量が一定になります。")]
    [SerializeField] private Ease retryRevealEase = Ease.Linear;

    [SerializeField] private Shader retryRevealShader;

    [Header("長押し開始時のスケールアニメーション")]
    [Tooltip("スケール0からRetry Cursor Scaleへ拡大する時間です。")]
    [Min(0f)]
    [SerializeField] private float retryScaleInDuration = 0.1f;

    [SerializeField] private Ease retryScaleInEase = Ease.OutBack;

    [Tooltip("長押し終了時に現在のサイズからスケール0へ縮小する時間です。")]
    [Min(0f)]
    [SerializeField] private float retryScaleOutDuration = 0.08f;

    [SerializeField] private Ease retryScaleOutEase = Ease.InCubic;

    private static readonly int FadedAlphaId = Shader.PropertyToID("_FadedAlpha");
    private static readonly int FillId = Shader.PropertyToID("_Fill");
    private static readonly int UvRectId = Shader.PropertyToID("_UvRect");

    private SpriteRenderer retryCursorRenderer;
    private Material retryCursorMaterial;
    private Tween retryHoldTween;
    private Tween retryScaleTween;
    private bool isRetryHoldActive;
    private bool hasTransferredToBlock;
    private bool isRetryCursorHiding;

    private void Awake()
    {
        FindMissingReferences();
        CreateRetryCursor();
    }

    private void Update()
    {
        if (stageManager != null && Input.GetKeyDown(retryKey))
        {
            stageManager.ReturnToBuildMode();
        }

        UpdateLongPressRetry();
    }

    private void UpdateLongPressRetry()
    {
        if (stageManager == null || stageManager.CurrentMode != StageManager.StageMode.Play)
        {
            CancelPointerOperation();
            return;
        }

        if (!TryGetPointerState(out Vector2 screenPosition, out PointerPhase phase))
        {
            if (hasTransferredToBlock)
            {
                CancelPointerOperation();
            }

            return;
        }

        if (hasTransferredToBlock)
        {
            if (phase == PointerPhase.Ended)
            {
                CancelPointerOperation();
            }

            return;
        }

        if (phase == PointerPhase.Began)
        {
            if (IsPointerOverUi() ||
                blockManager == null ||
                blockManager.HasPlacedBlockAtScreenPosition(screenPosition))
            {
                return;
            }

            BeginRetryHold(screenPosition);
        }

        if (!isRetryHoldActive)
        {
            return;
        }

        UpdateRetryCursorPosition(screenPosition);

        if (phase == PointerPhase.Ended)
        {
            CancelRetryHold();
            return;
        }

        if (blockManager.HasPlacedBlockAtScreenPosition(screenPosition))
        {
            CancelRetryHold();
            hasTransferredToBlock = blockManager.TryBeginPlacedBlockOperation(screenPosition);
            return;
        }

    }

    private void BeginRetryHold(Vector2 screenPosition)
    {
        isRetryHoldActive = true;
        UpdateRetryCursorPosition(screenPosition);
        SetRetryFillProgress(0f);

        if (retryCursorRenderer != null)
        {
            retryScaleTween?.Kill();
            retryScaleTween = null;
            isRetryCursorHiding = false;
            retryCursorRenderer.transform.localScale = Vector3.zero;
            retryCursorRenderer.gameObject.SetActive(true);

            if (retryScaleInDuration <= 0f)
            {
                retryCursorRenderer.transform.localScale = retryCursorScale;
            }
            else
            {
                retryScaleTween = retryCursorRenderer.transform
                    .DOScale(retryCursorScale, retryScaleInDuration)
                    .SetEase(retryScaleInEase)
                    .SetUpdate(true)
                    .OnComplete(() => retryScaleTween = null);
            }
        }

        retryHoldTween?.Kill();
        if (longPressDuration <= 0f)
        {
            CompleteRetryHold();
            return;
        }

        retryHoldTween = DOVirtual
            .Float(0f, 1f, longPressDuration, SetRetryFillProgress)
            .SetEase(retryRevealEase)
            .SetUpdate(true)
            .OnComplete(CompleteRetryHold);
    }

    private void CancelRetryHold()
    {
        isRetryHoldActive = false;
        retryHoldTween?.Kill();
        retryHoldTween = null;

        HideRetryCursorQuickly();
    }

    private void CompleteRetryHold()
    {
        retryHoldTween = null;
        if (!isRetryHoldActive)
        {
            return;
        }

        SetRetryFillProgress(1f);
        isRetryHoldActive = false;

        HideRetryCursorQuickly();

        stageManager?.ReturnToBuildMode();
    }

    private void HideRetryCursorQuickly()
    {
        if (retryCursorRenderer == null)
        {
            return;
        }

        if (!retryCursorRenderer.gameObject.activeSelf)
        {
            retryCursorRenderer.transform.localScale = Vector3.zero;
            return;
        }

        if (isRetryCursorHiding)
        {
            return;
        }

        retryScaleTween?.Kill();
        retryScaleTween = null;
        isRetryCursorHiding = true;

        if (retryScaleOutDuration <= 0f)
        {
            CompleteRetryCursorHide();
            return;
        }

        retryScaleTween = retryCursorRenderer.transform
            .DOScale(Vector3.zero, retryScaleOutDuration)
            .SetEase(retryScaleOutEase)
            .SetUpdate(true)
            .OnComplete(CompleteRetryCursorHide);
    }

    private void CompleteRetryCursorHide()
    {
        retryScaleTween = null;
        isRetryCursorHiding = false;

        if (retryCursorRenderer != null)
        {
            retryCursorRenderer.transform.localScale = Vector3.zero;
            retryCursorRenderer.gameObject.SetActive(false);
        }
    }

    private void CancelPointerOperation()
    {
        CancelRetryHold();

        if (hasTransferredToBlock && blockManager != null)
        {
            blockManager.EndPlacedBlockOperation();
        }

        hasTransferredToBlock = false;
    }

    private void CreateRetryCursor()
    {
        if (retryCursorSprite == null)
        {
            return;
        }

        GameObject retryCursor = new GameObject("Retry Cursor");
        retryCursor.transform.SetParent(transform);
        retryCursor.transform.localScale = Vector3.zero;

        retryCursorRenderer = retryCursor.AddComponent<SpriteRenderer>();
        retryCursorRenderer.sprite = retryCursorSprite;
        retryCursorRenderer.sortingLayerName = retryCursorSortingLayer;
        retryCursorRenderer.sortingOrder = retryCursorSortingOrder;

        if (retryRevealShader != null)
        {
            retryCursorMaterial = new Material(retryRevealShader)
            {
                name = "Retry Radial Reveal (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            retryCursorMaterial.SetFloat(FadedAlphaId, retryFadedAlpha);
            SetSpriteUvRect(retryCursorSprite);
            retryCursorRenderer.sharedMaterial = retryCursorMaterial;
        }

        retryCursor.SetActive(false);
    }

    private void SetRetryFillProgress(float progress)
    {
        if (retryCursorMaterial != null)
        {
            retryCursorMaterial.SetFloat(FillId, Mathf.Clamp01(progress));
        }
    }

    private void SetSpriteUvRect(Sprite sprite)
    {
        if (retryCursorMaterial == null || sprite == null || sprite.texture == null)
        {
            return;
        }

        Rect textureRect = sprite.textureRect;
        retryCursorMaterial.SetVector(UvRectId, new Vector4(
            textureRect.x / sprite.texture.width,
            textureRect.y / sprite.texture.height,
            textureRect.width / sprite.texture.width,
            textureRect.height / sprite.texture.height));
    }

    private void UpdateRetryCursorPosition(Vector2 screenPosition)
    {
        if (retryCursorRenderer == null || pointerCamera == null)
        {
            return;
        }

        float depth = Mathf.Abs(pointerCamera.transform.position.z - retryCursorZ);
        Vector3 position = pointerCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, depth));
        position.x += retryCursorOffset.x;
        position.y += retryCursorOffset.y;
        position.z = retryCursorZ;
        retryCursorRenderer.transform.position = position;
    }

    private void OnValidate()
    {
        longPressDuration = Mathf.Max(0f, longPressDuration);
        retryFadedAlpha = Mathf.Clamp01(retryFadedAlpha);
        retryScaleInDuration = Mathf.Max(0f, retryScaleInDuration);
        retryScaleOutDuration = Mathf.Max(0f, retryScaleOutDuration);
        FindMissingReferences();
    }

    private void FindMissingReferences()
    {
        if (stageManager == null)
        {
            stageManager = FindFirstObjectByType<StageManager>();
        }

        if (blockManager == null)
        {
            blockManager = FindFirstObjectByType<BlockManager>();
        }

        if (pointerCamera == null)
        {
            pointerCamera = Camera.main;
        }
    }

    private void OnDisable()
    {
        CancelPointerOperation();
    }

    private void OnDestroy()
    {
        retryHoldTween?.Kill();
        retryScaleTween?.Kill();

        if (retryCursorMaterial != null)
        {
            Destroy(retryCursorMaterial);
        }
    }

    private static bool TryGetPointerState(out Vector2 position, out PointerPhase phase)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            position = touch.position;
            phase = touch.phase == TouchPhase.Began
                ? PointerPhase.Began
                : touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled
                    ? PointerPhase.Ended
                    : PointerPhase.Held;
            return true;
        }

        position = Input.mousePosition;
        if (Input.GetMouseButtonDown(0))
        {
            phase = PointerPhase.Began;
            return true;
        }

        if (Input.GetMouseButtonUp(0))
        {
            phase = PointerPhase.Ended;
            return true;
        }

        if (Input.GetMouseButton(0))
        {
            phase = PointerPhase.Held;
            return true;
        }

        phase = PointerPhase.None;
        return false;
    }

    private static bool IsPointerOverUi()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        return Input.touchCount > 0
            ? EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)
            : EventSystem.current.IsPointerOverGameObject();
    }

    private enum PointerPhase
    {
        None,
        Began,
        Held,
        Ended
    }
}

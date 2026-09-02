using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BigUiBlock : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private BoxCollider2D targetCollider;
    [SerializeField] private Sprite fullSprite;
    [SerializeField] private Sprite compactSprite;
    [SerializeField] private Vector2 fullSize;
    [SerializeField] private Vector2 compactSize;

    private BlockManager.BigUiSizeMode currentMode = BlockManager.BigUiSizeMode.Full;
    private Coroutine invalidPreviewCoroutine;
    private SpriteRenderer invalidPreviewRenderer;

    public void Configure(
        SpriteRenderer renderer,
        BoxCollider2D collider,
        Sprite initialFullSprite,
        Sprite initialCompactSprite,
        Vector2 initialFullSize,
        Vector2 initialCompactSize)
    {
        targetRenderer = renderer;
        targetCollider = collider;
        fullSprite = initialFullSprite;
        compactSprite = initialCompactSprite;
        fullSize = initialFullSize;
        compactSize = initialCompactSize;
        ApplySizeMode(BlockManager.BigUiSizeMode.Full);
    }

    public void ApplySizeMode(BlockManager.BigUiSizeMode mode)
    {
        if (invalidPreviewCoroutine != null)
        {
            StopCoroutine(invalidPreviewCoroutine);
            invalidPreviewCoroutine = null;
        }
        SetInvalidPreviewVisible(false);

        currentMode = mode;
        ApplyVisual(mode, true);
    }

    public void SetStateSprites(Sprite stateFullSprite, Sprite stateCompactSprite)
    {
        if (stateFullSprite != null)
        {
            fullSprite = stateFullSprite;
        }

        if (stateCompactSprite != null)
        {
            compactSprite = stateCompactSprite;
        }

        ApplyVisual(currentMode, false);
    }

    public bool WouldOverlap(Collider2D otherCollider, BlockManager.BigUiSizeMode targetMode)
    {
        if (targetCollider == null || otherCollider == null ||
            targetMode == BlockManager.BigUiSizeMode.Hidden)
        {
            return false;
        }

        Vector2 size = GetSize(targetMode);
        Vector2 halfSize = size * 0.5f;
        Vector2 offset = targetCollider.offset;
        Transform colliderTransform = targetCollider.transform;
        Vector3 firstCorner = colliderTransform.TransformPoint(offset - halfSize);
        Bounds targetBounds = new Bounds(firstCorner, Vector3.zero);
        targetBounds.Encapsulate(colliderTransform.TransformPoint(
            offset + new Vector2(halfSize.x, -halfSize.y)));
        targetBounds.Encapsulate(colliderTransform.TransformPoint(
            offset + new Vector2(-halfSize.x, halfSize.y)));
        targetBounds.Encapsulate(colliderTransform.TransformPoint(offset + halfSize));

        Bounds otherBounds = otherCollider.bounds;
        return otherBounds.max.x > targetBounds.min.x &&
               otherBounds.min.x < targetBounds.max.x &&
               otherBounds.max.y > targetBounds.min.y &&
               otherBounds.min.y < targetBounds.max.y;
    }

    public void ShowInvalidSizePreview(
        BlockManager.BigUiSizeMode targetMode,
        Color invalidTint,
        float duration)
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (invalidPreviewCoroutine != null)
        {
            StopCoroutine(invalidPreviewCoroutine);
        }

        EnsureInvalidPreviewRenderer();
        if (invalidPreviewRenderer == null)
        {
            return;
        }

        CopyRendererSettings(targetRenderer, invalidPreviewRenderer);
        ApplyRendererVisual(invalidPreviewRenderer, targetMode);
        Color color = targetRenderer.color;
        invalidPreviewRenderer.color = new Color(
            color.r * invalidTint.r,
            color.g * invalidTint.g,
            color.b * invalidTint.b,
            color.a * invalidTint.a);
        invalidPreviewRenderer.sortingOrder = targetRenderer.sortingOrder - 1;
        invalidPreviewRenderer.enabled = true;
        invalidPreviewCoroutine = StartCoroutine(RestoreAfterInvalidPreview(duration));
    }

    private IEnumerator RestoreAfterInvalidPreview(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        SetInvalidPreviewVisible(false);
        invalidPreviewCoroutine = null;
    }

    private void ApplyVisual(BlockManager.BigUiSizeMode mode, bool updateCollider)
    {
        bool visible = mode != BlockManager.BigUiSizeMode.Hidden;
        if (targetRenderer != null)
        {
            targetRenderer.enabled = visible;
        }

        if (updateCollider && targetCollider != null)
        {
            targetCollider.enabled = visible;
        }

        if (!visible)
        {
            return;
        }

        Vector2 size = GetSize(mode);

        if (updateCollider && targetCollider != null)
        {
            targetCollider.size = size;
            targetCollider.offset = Vector2.zero;
        }

        if (targetRenderer == null)
        {
            return;
        }

        ApplyRendererVisual(targetRenderer, mode);
    }

    private void ApplyRendererVisual(
        SpriteRenderer renderer,
        BlockManager.BigUiSizeMode mode)
    {
        bool compact = mode == BlockManager.BigUiSizeMode.Compact;
        Sprite sprite = compact && compactSprite != null ? compactSprite : fullSprite;
        Vector2 size = GetSize(mode);
        renderer.sprite = sprite;
        Transform visual = renderer.transform;
        visual.localPosition = Vector3.zero;
        if (sprite == null)
        {
            visual.localScale = new Vector3(size.x, size.y, 1f);
            return;
        }

        Vector2 spriteSize = sprite.bounds.size;
        float scaleX = spriteSize.x > Mathf.Epsilon ? size.x / spriteSize.x : 1f;
        float scaleY = spriteSize.y > Mathf.Epsilon ? size.y / spriteSize.y : 1f;
        visual.localScale = new Vector3(scaleX, scaleY, 1f);
        Vector3 spriteCenter = sprite.bounds.center;
        visual.localPosition = new Vector3(
            -spriteCenter.x * scaleX,
            -spriteCenter.y * scaleY,
            0f);
    }

    private void EnsureInvalidPreviewRenderer()
    {
        if (invalidPreviewRenderer != null || targetRenderer == null)
        {
            return;
        }

        GameObject previewObject = new GameObject("Invalid Size Preview");
        previewObject.layer = targetRenderer.gameObject.layer;
        previewObject.transform.SetParent(targetRenderer.transform.parent, false);
        invalidPreviewRenderer = previewObject.AddComponent<SpriteRenderer>();
        invalidPreviewRenderer.enabled = false;
    }

    private static void CopyRendererSettings(SpriteRenderer source, SpriteRenderer destination)
    {
        destination.flipX = source.flipX;
        destination.flipY = source.flipY;
        destination.drawMode = source.drawMode;
        destination.size = source.size;
        destination.maskInteraction = source.maskInteraction;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sharedMaterial = source.sharedMaterial;
    }

    private void SetInvalidPreviewVisible(bool visible)
    {
        if (invalidPreviewRenderer != null)
        {
            invalidPreviewRenderer.enabled = visible;
        }
    }

    private Vector2 GetSize(BlockManager.BigUiSizeMode mode) =>
        mode == BlockManager.BigUiSizeMode.Compact ? compactSize : fullSize;
}

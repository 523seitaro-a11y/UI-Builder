using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>Draws only a sprite's outer border above the darkness Canvas, without physics or input.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class BlockOutlineGraphic : MaskableGraphic
{
    private SpriteRenderer source;
    private Camera worldCamera;
    private Material outlineMaterial;
    private float worldWidth;

    public override Texture mainTexture => source != null && source.sprite != null
        ? source.sprite.texture : Texture2D.whiteTexture;

    public static BlockOutlineGraphic Create(
        RectTransform parent, SpriteRenderer renderer, Camera camera)
    {
        Shader shader = Resources.Load<Shader>("BlockOutlineOverlay");
        if (shader == null || camera == null)
        {
            return null;
        }

        // Construct a graphic, never clone the block or its collider hierarchy.
        GameObject root = new GameObject(renderer.name + " (Overlay Outline)",
            typeof(RectTransform), typeof(CanvasRenderer));
        root.transform.SetParent(parent, false);
        BlockOutlineGraphic graphic = root.AddComponent<BlockOutlineGraphic>();
        graphic.source = renderer;
        graphic.worldCamera = camera;
        graphic.raycastTarget = false;
        graphic.outlineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        graphic.material = graphic.outlineMaterial;
        graphic.rectTransform.anchorMin = graphic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        graphic.rectTransform.sizeDelta = Vector2.zero;
        root.SetActive(false);
        return graphic;
    }

    public void SetOutline(bool visible, Color tint, float width)
    {
        color = tint;
        worldWidth = Mathf.Max(0f, width);
        gameObject.SetActive(visible && worldWidth > 0f);
    }

    private void LateUpdate()
    {
        // Brightness track copies may be created later in the frame.
        transform.SetAsLastSibling();
        SetVerticesDirty();
        SetMaterialDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (source == null || source.sprite == null || !source.enabled ||
            !source.gameObject.activeInHierarchy || worldCamera == null || outlineMaterial == null)
        {
            return;
        }

        Bounds bounds = source.localBounds;
        Vector3 scale = source.transform.lossyScale;
        Vector2 size = bounds.size;
        if (size.x <= 0f || size.y <= 0f || Mathf.Abs(scale.x * scale.y) < Mathf.Epsilon)
        {
            return;
        }

        Vector2 padding = new Vector2(worldWidth / Mathf.Abs(scale.x), worldWidth / Mathf.Abs(scale.y));
        Vector2 uvPadding = new Vector2(padding.x / size.x, padding.y / size.y);
        Sprite sprite = source.sprite;
        Vector4 border = source.drawMode == SpriteDrawMode.Simple ? Vector4.zero : sprite.border;
        Vector4 shapeBorder = new Vector4(border.x / (sprite.pixelsPerUnit * size.x),
            border.y / (sprite.pixelsPerUnit * size.y), border.z / (sprite.pixelsPerUnit * size.x),
            border.w / (sprite.pixelsPerUnit * size.y));
        // Match the border compression of sliced sprites smaller than their border sum.
        float borderScaleX = Mathf.Max(1f, shapeBorder.x + shapeBorder.z);
        float borderScaleY = Mathf.Max(1f, shapeBorder.y + shapeBorder.w);
        shapeBorder = new Vector4(shapeBorder.x / borderScaleX, shapeBorder.y / borderScaleY,
            shapeBorder.z / borderScaleX, shapeBorder.w / borderScaleY);
        outlineMaterial.SetVector("_OuterUV", DataUtility.GetOuterUV(sprite));
        outlineMaterial.SetVector("_ShapeBorder", shapeBorder);
        outlineMaterial.SetVector("_TextureBorder", new Vector4(border.x / sprite.rect.width,
            border.y / sprite.rect.height, border.z / sprite.rect.width, border.w / sprite.rect.height));
        outlineMaterial.SetVector("_OutlineStep", uvPadding);
        outlineMaterial.SetVector("_SpriteFlip", new Vector4(source.flipX ? 1f : 0f, source.flipY ? 1f : 0f));

        Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
        Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera : null;
        for (int i = 0; i < 4; i++)
        {
            bool right = i == 2 || i == 3;
            bool top = i == 1 || i == 2;
            Vector3 local = new Vector3(right ? bounds.max.x + padding.x : bounds.min.x - padding.x,
                top ? bounds.max.y + padding.y : bounds.min.y - padding.y, bounds.center.z);
            Vector3 screen = worldCamera.WorldToScreenPoint(source.transform.TransformPoint(local));
            if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screen, uiCamera, out Vector2 position))
            {
                mesh.Clear();
                return;
            }
            mesh.AddVert(position, color, new Vector2(right ? 1f + uvPadding.x : -uvPadding.x,
                top ? 1f + uvPadding.y : -uvPadding.y));
        }
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(2, 3, 0);
    }

    protected override void OnDestroy()
    {
        if (outlineMaterial != null)
        {
            Destroy(outlineMaterial);
        }
        base.OnDestroy();
    }
}

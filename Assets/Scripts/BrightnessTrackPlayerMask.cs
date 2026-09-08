using UnityEngine;
using UnityEngine.UI;

/// <summary>Supplies player sprite coordinates before Canvas batching changes UI vertex space.</summary>
[DisallowMultipleComponent]
public sealed class BrightnessTrackPlayerMask : BaseMeshEffect
{
    private SpriteRenderer playerRenderer;
    private Camera playerCamera;
    private Sprite cachedSprite;
    private Vector2 vertexA, vertexB, vertexC;
    private Vector2 uvA, uvB, uvC;
    private Vector4 spriteUVRect;
    private bool hasSpriteTriangle;

    public void Configure(SpriteRenderer renderer, Camera camera)
    {
        playerRenderer = renderer;
        playerCamera = camera;
        Canvas canvas = graphic.canvas;
        if (canvas != null)
        {
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            canvas.rootCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }
        graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive())
        {
            return;
        }

        if (playerRenderer == null || playerRenderer.sprite == null || playerCamera == null ||
            !playerRenderer.enabled || !playerRenderer.gameObject.activeInHierarchy)
        {
            UpdateMaskMaterial(0f);
            return;
        }

        if (cachedSprite != playerRenderer.sprite)
        {
            CacheSpriteTriangle(playerRenderer.sprite);
        }
        if (!hasSpriteTriangle)
        {
            UpdateMaskMaterial(0f);
            return;
        }

        // Sprite mesh positions and UVs are a matched pair. Renderer bounds and an
        // outer UV rectangle can have different padding, especially for tight meshes.
        Vector3 a = PlayerVertexToScreen(vertexA);
        Vector3 b = PlayerVertexToScreen(vertexB);
        Vector3 c = PlayerVertexToScreen(vertexC);
        Vector2 origin = a;
        Vector2 right = (Vector2)(b - a);
        Vector2 up = (Vector2)(c - a);
        float determinant = right.x * up.y - right.y * up.x;
        if (a.z <= 0f || b.z <= 0f || c.z <= 0f || Mathf.Abs(determinant) <= Mathf.Epsilon)
        {
            UpdateMaskMaterial(0f);
            return;
        }

        Canvas canvas = graphic.canvas != null ? graphic.canvas.rootCanvas : null;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        UIVertex vertex = default;
        for (int i = 0; i < mesh.currentVertCount; i++)
        {
            mesh.PopulateUIVertex(ref vertex, i);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera,
                graphic.rectTransform.TransformPoint(vertex.position));
            Vector2 offset = screen - origin;
            float weightB = (offset.x * up.y - offset.y * up.x) / determinant;
            float weightC = (right.x * offset.y - right.y * offset.x) / determinant;
            Vector2 playerUV = uvA + weightB * (uvB - uvA) + weightC * (uvC - uvA);
            vertex.uv1 = new Vector4(playerUV.x, playerUV.y, 0f, 0f);
            mesh.SetUIVertex(vertex, i);
        }

        // Bind the same animation frame used for the geometry, not an earlier LateUpdate frame.
        UpdateMaskMaterial(playerRenderer.color.a);
    }

    private Vector3 PlayerVertexToScreen(Vector2 vertex)
    {
        vertex.x *= playerRenderer.flipX ? -1f : 1f;
        vertex.y *= playerRenderer.flipY ? -1f : 1f;
        return playerCamera.WorldToScreenPoint(playerRenderer.localToWorldMatrix.MultiplyPoint3x4(vertex));
    }

    private void CacheSpriteTriangle(Sprite sprite)
    {
        cachedSprite = sprite;
        hasSpriteTriangle = false;
        Vector2[] vertices = sprite.vertices;
        Vector2[] uvs = sprite.uv;
        ushort[] triangles = sprite.triangles;
        spriteUVRect = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
        float largestArea = 0f;
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            Vector2 ab = vertices[b] - vertices[a];
            Vector2 ac = vertices[c] - vertices[a];
            float area = Mathf.Abs(ab.x * ac.y - ab.y * ac.x);
            if (area <= largestArea)
            {
                continue;
            }
            largestArea = area;
            vertexA = vertices[a];
            vertexB = vertices[b];
            vertexC = vertices[c];
            uvA = uvs[a];
            uvB = uvs[b];
            uvC = uvs[c];
            hasSpriteTriangle = true;
        }
    }

    private void UpdateMaskMaterial(float opacity)
    {
        Material source = graphic.material;
        if (source == null)
        {
            return;
        }
        source.SetFloat("_PlayerMaskEnabled", opacity);
        if (cachedSprite != null)
        {
            source.SetTexture("_PlayerTex", cachedSprite.texture);
            source.SetVector("_PlayerUVRect", spriteUVRect);
        }

        // A parent UI mask can cache a separate stencil material.
        Material rendered = graphic.materialForRendering;
        if (rendered != null && rendered != source)
        {
            rendered.SetTexture("_PlayerTex", source.GetTexture("_PlayerTex"));
            rendered.SetVector("_PlayerUVRect", source.GetVector("_PlayerUVRect"));
            rendered.SetFloat("_PlayerMaskEnabled", opacity);
        }
    }
}

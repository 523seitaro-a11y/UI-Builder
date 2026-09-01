using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ゲーム世界とUIの間に置くSpriteRendererの透明度を使って、ステージの明るさを制御します。
/// 1が通常の明るさ、0が最も暗い状態です。
/// </summary>
[DisallowMultipleComponent]
public sealed class ScreenBrightnessController : MonoBehaviour
{
    private const string OverlaySortingLayer = "BrightnessOverlay";

    [Tooltip("旧Canvas暗転Imageです。実行中は無効化し、ワールド側の専用レイヤーを使用します。")]
    [SerializeField] private Image darknessOverlay;

    [Tooltip("旧Canvas暗転用の前面表示レイヤーです。ワールド側暗転では使用しません。")]
    [SerializeField] private RectTransform visibilityLayer;

    [Range(0f, 1f)]
    [SerializeField] private float defaultBrightness = 1f;

    [Tooltip("ハンドルを左端からこの割合まで動かしても、完全暗転を維持します。")]
    [Range(0f, 0.5f)]
    [SerializeField] private float fullDarknessHandleRange = 0.2f;

    public float Brightness { get; private set; } = 1f;

    public RectTransform VisibilityLayer => visibilityLayer;
    public bool UsesWorldOverlay => true;

    private Texture2D worldOverlayTexture;
    private Sprite worldOverlaySprite;
    private SpriteRenderer worldOverlayRenderer;
    private Camera targetCamera;
    private bool overlayHiddenForSceneView;

    private void OnEnable()
    {
        Camera.onPreCull -= HandleCameraPreCull;
        Camera.onPostRender -= HandleCameraPostRender;
        Camera.onPreCull += HandleCameraPreCull;
        Camera.onPostRender += HandleCameraPostRender;
    }

    private void OnDisable()
    {
        Camera.onPreCull -= HandleCameraPreCull;
        Camera.onPostRender -= HandleCameraPostRender;
        if (worldOverlayRenderer != null && overlayHiddenForSceneView)
        {
            worldOverlayRenderer.enabled = true;
        }
        overlayHiddenForSceneView = false;
    }

    private void Awake()
    {
        if (darknessOverlay != null)
        {
            darknessOverlay.enabled = false;
            darknessOverlay.raycastTarget = false;
        }

        EnsureWorldOverlay();
        ResetBrightness();
    }

    private void LateUpdate() => SyncWorldOverlayToCamera();

    private void HandleCameraPreCull(Camera renderingCamera)
    {
        if (worldOverlayRenderer == null)
        {
            return;
        }

        if (renderingCamera != null && renderingCamera.cameraType == CameraType.SceneView)
        {
            overlayHiddenForSceneView = worldOverlayRenderer.enabled;
            worldOverlayRenderer.enabled = false;
            return;
        }

        // Sceneビューの描画が中断された場合でもGameカメラでは必ず復帰させます。
        if (overlayHiddenForSceneView)
        {
            worldOverlayRenderer.enabled = true;
            overlayHiddenForSceneView = false;
        }
    }

    private void HandleCameraPostRender(Camera renderingCamera)
    {
        if (worldOverlayRenderer == null ||
            renderingCamera == null ||
            renderingCamera.cameraType != CameraType.SceneView ||
            !overlayHiddenForSceneView)
        {
            return;
        }

        worldOverlayRenderer.enabled = true;
        overlayHiddenForSceneView = false;
    }

    public void SetBrightness(float brightness)
    {
        float handleValue = Mathf.Clamp01(brightness);
        Brightness = Mathf.InverseLerp(fullDarknessHandleRange, 1f, handleValue);
        ApplyOverlay();
    }

    public void ResetBrightness() => SetBrightness(defaultBrightness);

    private void ApplyOverlay()
    {
        Color color = new Color32(0x3C, 0x3C, 0x3C, 0xFF);
        color.a = 1f - Brightness;

        if (worldOverlayRenderer != null)
        {
            worldOverlayRenderer.color = color;
        }

        if (!Application.isPlaying && darknessOverlay != null)
        {
            darknessOverlay.color = color;
            darknessOverlay.raycastTarget = false;
        }
    }

    private void EnsureWorldOverlay()
    {
        if (!Application.isPlaying || worldOverlayRenderer != null)
        {
            return;
        }

        targetCamera = Camera.main;
        worldOverlayTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "Brightness Overlay Texture",
            hideFlags = HideFlags.HideAndDontSave
        };
        worldOverlayTexture.SetPixel(0, 0, Color.white);
        worldOverlayTexture.Apply();
        worldOverlaySprite = Sprite.Create(
            worldOverlayTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        worldOverlaySprite.name = "Brightness Overlay Sprite";
        worldOverlaySprite.hideFlags = HideFlags.HideAndDontSave;

        GameObject overlayObject = new GameObject("Brightness World Overlay");
        overlayObject.transform.SetParent(transform, false);
        worldOverlayRenderer = overlayObject.AddComponent<SpriteRenderer>();
        worldOverlayRenderer.sprite = worldOverlaySprite;
        worldOverlayRenderer.sortingLayerName = OverlaySortingLayer;
        worldOverlayRenderer.sortingOrder = 0;
        SyncWorldOverlayToCamera();
    }

    private void SyncWorldOverlayToCamera()
    {
        if (worldOverlayRenderer == null)
        {
            return;
        }

        targetCamera ??= Camera.main;
        if (targetCamera == null || !targetCamera.orthographic)
        {
            return;
        }

        float height = targetCamera.orthographicSize * 2f;
        float width = height * targetCamera.aspect;
        Transform overlay = worldOverlayRenderer.transform;
        Vector3 cameraPosition = targetCamera.transform.position;
        overlay.position = new Vector3(cameraPosition.x, cameraPosition.y, cameraPosition.z + 1f);
        overlay.localScale = new Vector3(width, height, 1f);
    }

    private void OnValidate()
    {
        defaultBrightness = Mathf.Clamp01(defaultBrightness);
        fullDarknessHandleRange = Mathf.Clamp(fullDarknessHandleRange, 0f, 0.5f);

        if (!Application.isPlaying)
        {
            Brightness = defaultBrightness;
            ApplyOverlay();
        }
    }

    private void OnDestroy()
    {
        Camera.onPreCull -= HandleCameraPreCull;
        Camera.onPostRender -= HandleCameraPostRender;

        if (worldOverlaySprite != null)
        {
            Destroy(worldOverlaySprite);
        }
        if (worldOverlayTexture != null)
        {
            Destroy(worldOverlayTexture);
        }
    }
}

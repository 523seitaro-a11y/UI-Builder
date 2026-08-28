using UnityEngine;

/// <summary>
/// 明るい間だけ進路を塞ぎ、画面を十分暗くすると開く壁です。
/// ゴール判定そのものは変更せず、暗くする操作をルート攻略の一部にします。
/// </summary>
[DisallowMultipleComponent]
public sealed class BrightnessRouteBarrier : MonoBehaviour
{
    [SerializeField] private ScreenBrightnessController brightnessController;

    [Range(0f, 1f)]
    [SerializeField] private float openingBrightness = 0.25f;

    [SerializeField] private Vector2 barrierPosition = new Vector2(-4f, -0.5f);
    [SerializeField] private Vector2 barrierSize = new Vector2(1f, 8f);
    [SerializeField] private Color barrierColor = new Color(1f, 0.86f, 0.25f, 1f);

    private GameObject barrier;
    private Texture2D barrierTexture;

    private void Awake()
    {
        if (brightnessController == null)
        {
            brightnessController = FindFirstObjectByType<ScreenBrightnessController>();
        }

        CreateBarrier();
    }

    private void OnEnable()
    {
        if (brightnessController != null)
        {
            brightnessController.BrightnessChanged -= OnBrightnessChanged;
            brightnessController.BrightnessChanged += OnBrightnessChanged;
        }
    }

    private void Start() => RefreshBarrier();

    private void OnDisable()
    {
        if (brightnessController != null)
        {
            brightnessController.BrightnessChanged -= OnBrightnessChanged;
        }
    }

    private void OnDestroy()
    {
        if (barrierTexture != null)
        {
            Destroy(barrierTexture);
        }
    }

    private void CreateBarrier()
    {
        if (barrier != null)
        {
            return;
        }

        barrier = new GameObject("Light Route Barrier");
        barrier.transform.SetParent(transform, false);
        barrier.transform.position = barrierPosition;

        BoxCollider2D collider = barrier.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        barrierTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "Light Route Barrier Texture",
            filterMode = FilterMode.Point
        };
        barrierTexture.SetPixel(0, 0, Color.white);
        barrierTexture.Apply();

        Sprite sprite = Sprite.Create(
            barrierTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        sprite.name = "Light Route Barrier Sprite";

        SpriteRenderer renderer = barrier.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = barrierColor;
        renderer.sortingOrder = 4;
        barrier.transform.localScale = new Vector3(barrierSize.x, barrierSize.y, 1f);
    }

    private void OnBrightnessChanged(float _) => RefreshBarrier();

    private void RefreshBarrier()
    {
        if (barrier == null || brightnessController == null)
        {
            return;
        }

        bool isOpen = brightnessController.IsAtOrBelow(openingBrightness);
        barrier.SetActive(!isOpen);
    }

    private void OnValidate()
    {
        openingBrightness = Mathf.Clamp01(openingBrightness);
        barrierSize.x = Mathf.Max(0.1f, barrierSize.x);
        barrierSize.y = Mathf.Max(0.1f, barrierSize.y);
    }
}

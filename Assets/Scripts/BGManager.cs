using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 半透明の正方形をワールド空間に生成し、タイルマップの背面で下から上へ流します。
/// 通常のMonoBehaviourなので、再生モード中のみ動作します。
/// </summary>
[DisallowMultipleComponent]
public class BGManager : MonoBehaviour
{
    [Header("表示範囲")]
    [Tooltip("表示範囲の基準にするカメラ。未設定ならMain Cameraを使用します。")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Canvas単位をワールド単位へ換算する基準です。現在のBGManagerを設定しておけば、既存のサイズ設定をそのまま使えます。")]
    [SerializeField] private RectTransform animationArea;

    [Tooltip("Animation Areaを設定しない場合に使用する基準画面高です。")]
    [Min(1f)]
    [SerializeField] private float referenceCanvasHeight = 1080f;

    [Header("タイルマップより背面に表示")]
    [Tooltip("タイルマップと同じSorting Layerを指定してください。")]
    [SerializeField] private string sortingLayerName = "Default";

    [Tooltip("タイルマップのOrderより小さい値にします。タイルマップが0なら-10で背面になります。")]
    [SerializeField] private int sortingOrder = -10;

    [Tooltip("カメラから正方形までの距離です。通常は変更不要です。")]
    [Min(0.01f)]
    [SerializeField] private float distanceFromCamera = 10f;

    [Header("生成設定")]
    [Tooltip("開始時から画面内に表示しておく正方形の数です。")]
    [Min(0)]
    [SerializeField] private int initialSquareCount = 8;

    [Tooltip("同時に存在できる正方形の最大数です。")]
    [Min(1)]
    [SerializeField] private int maxSquareCount = 20;

    [Tooltip("正方形を生成する間隔の最小値と最大値（秒）です。")]
    [SerializeField] private Vector2 spawnIntervalRange = new Vector2(1f, 2f);

    [Tooltip("正方形の一辺の長さの最小値と最大値（Canvas単位）です。")]
    [SerializeField] private Vector2 squareSizeRange = new Vector2(100f, 200f);

    [Header("移動設定")]
    [Tooltip("上昇速度の最小値と最大値（Canvas単位/秒）です。")]
    [SerializeField] private Vector2 moveSpeedRange = new Vector2(25f, 80f);

    [Tooltip("画面上下端から生成・削除位置まで追加する余白（Canvas単位）です。")]
    [Min(0f)]
    [SerializeField] private float verticalMargin = 20f;

    [Tooltip("Time.timeScaleが0でも背景を動かす場合はオンにします。")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("見た目")]
    [Tooltip("正方形の色です。")]
    [SerializeField] private Color squareColor = new Color(0.9622642f, 0.9622642f, 0.9622642f, 1f);

    [Tooltip("正方形ごとの回転角度の最小値と最大値です。0, 0なら回転しません。")]
    [SerializeField] private Vector2 rotationRange = Vector2.zero;

    private readonly List<FloatingSquare> activeSquares = new List<FloatingSquare>();
    private Transform runtimeContainer;
    private Texture2D squareTexture;
    private Sprite squareSprite;
    private float nextSpawnTime;
    private bool hasStarted;

    private sealed class FloatingSquare
    {
        public Transform Transform;
        public float Speed;
        public float HalfSize;
    }

    private void Start()
    {
        hasStarted = true;
        InitializeAnimation();
    }

    private void OnEnable()
    {
        if (hasStarted && Application.isPlaying)
        {
            InitializeAnimation();
        }
    }

    private void InitializeAnimation()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null || !targetCamera.orthographic)
        {
            Debug.LogWarning("BGManagerには2D用のOrthographic Cameraを設定してください。", this);
            enabled = false;
            return;
        }

        CreateRuntimeResources();

        int count = Mathf.Min(initialSquareCount, maxSquareCount);
        for (int i = 0; i < count; i++)
        {
            SpawnSquare(fillVisibleArea: true);
        }

        ScheduleNextSpawn();
    }

    private void Update()
    {
        if (runtimeContainer == null)
        {
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        GetCameraArea(out _, out float halfHeight);
        float margin = verticalMargin * GetCanvasToWorldScale();

        for (int i = activeSquares.Count - 1; i >= 0; i--)
        {
            FloatingSquare square = activeSquares[i];
            square.Transform.localPosition += Vector3.up * (square.Speed * deltaTime);

            if (square.Transform.localPosition.y > halfHeight + margin + square.HalfSize)
            {
                Destroy(square.Transform.gameObject);
                activeSquares.RemoveAt(i);
            }
        }

        float currentTime = useUnscaledTime ? Time.unscaledTime : Time.time;
        if (currentTime >= nextSpawnTime)
        {
            if (activeSquares.Count < maxSquareCount)
            {
                SpawnSquare(fillVisibleArea: false);
            }

            ScheduleNextSpawn();
        }
    }

    private void CreateRuntimeResources()
    {
        GameObject containerObject = new GameObject("BG Animation (Runtime)");
        runtimeContainer = containerObject.transform;
        runtimeContainer.SetParent(targetCamera.transform, false);
        runtimeContainer.localPosition = new Vector3(0f, 0f, distanceFromCamera);
        runtimeContainer.localRotation = Quaternion.identity;
        runtimeContainer.localScale = Vector3.one;

        squareTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "BG Square Texture (Runtime)",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        squareTexture.SetPixel(0, 0, Color.white);
        squareTexture.Apply();

        squareSprite = Sprite.Create(
            squareTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        squareSprite.name = "BG Square Sprite (Runtime)";
    }

    private void SpawnSquare(bool fillVisibleArea)
    {
        float canvasToWorld = GetCanvasToWorldScale();
        float size = Random.Range(Min(squareSizeRange), Max(squareSizeRange)) * canvasToWorld;
        float halfSize = size * 0.5f;
        float speed = Random.Range(Min(moveSpeedRange), Max(moveSpeedRange)) * canvasToWorld;
        float margin = verticalMargin * canvasToWorld;
        GetCameraArea(out float halfWidth, out float halfHeight);

        GameObject squareObject = new GameObject("Floating Square", typeof(SpriteRenderer));
        squareObject.transform.SetParent(runtimeContainer, false);

        float minX = -halfWidth + halfSize;
        float maxX = halfWidth - halfSize;
        float x = minX <= maxX ? Random.Range(minX, maxX) : 0f;
        float y = fillVisibleArea
            ? Random.Range(-halfHeight - halfSize, halfHeight + halfSize)
            : -halfHeight - margin - halfSize;

        squareObject.transform.localPosition = new Vector3(x, y, 0f);
        squareObject.transform.localRotation = Quaternion.Euler(
            0f,
            0f,
            Random.Range(Min(rotationRange), Max(rotationRange)));
        squareObject.transform.localScale = new Vector3(size, size, 1f);

        SpriteRenderer spriteRenderer = squareObject.GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = squareSprite;
        spriteRenderer.color = squareColor;
        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrder;

        activeSquares.Add(new FloatingSquare
        {
            Transform = squareObject.transform,
            Speed = speed,
            HalfSize = halfSize
        });
    }

    private void GetCameraArea(out float halfWidth, out float halfHeight)
    {
        halfHeight = targetCamera.orthographicSize;
        halfWidth = halfHeight * targetCamera.aspect;
    }

    private float GetCanvasToWorldScale()
    {
        float canvasHeight = referenceCanvasHeight;
        if (animationArea != null && animationArea.rect.height > 0.01f)
        {
            canvasHeight = animationArea.rect.height;
        }

        return targetCamera.orthographicSize * 2f / Mathf.Max(1f, canvasHeight);
    }

    private void ScheduleNextSpawn()
    {
        float delay = Mathf.Max(0.01f, Random.Range(
            Min(spawnIntervalRange),
            Max(spawnIntervalRange)));
        float currentTime = useUnscaledTime ? Time.unscaledTime : Time.time;
        nextSpawnTime = currentTime + delay;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        activeSquares.Clear();

        if (runtimeContainer != null)
        {
            Destroy(runtimeContainer.gameObject);
            runtimeContainer = null;
        }

        if (squareSprite != null)
        {
            Destroy(squareSprite);
            squareSprite = null;
        }

        if (squareTexture != null)
        {
            Destroy(squareTexture);
            squareTexture = null;
        }
    }

    private void OnValidate()
    {
        initialSquareCount = Mathf.Max(0, initialSquareCount);
        maxSquareCount = Mathf.Max(1, maxSquareCount);
        referenceCanvasHeight = Mathf.Max(1f, referenceCanvasHeight);
        distanceFromCamera = Mathf.Max(0.01f, distanceFromCamera);
        verticalMargin = Mathf.Max(0f, verticalMargin);
        squareColor.a = Mathf.Clamp01(squareColor.a);
    }

    private static float Min(Vector2 range)
    {
        return Mathf.Min(range.x, range.y);
    }

    private static float Max(Vector2 range)
    {
        return Mathf.Max(range.x, range.y);
    }
}

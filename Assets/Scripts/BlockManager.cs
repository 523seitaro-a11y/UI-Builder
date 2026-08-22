using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

/// <summary>
/// 画面上部のブロックをフィールドへドラッグし、Tilemap のセルに沿って配置します。
/// ドラッグ元ごとの見た目、生成元、占有セル数などは Inspector から設定できます。
/// </summary>
public class BlockManager : MonoBehaviour
{
    public interface IBlockOperationState
    {
        bool IsOperating { get; }
        void BeginOperation();
        void CancelOperation();
    }

    public event Action<bool> DragStateChanged;

    public bool IsDragging => activePreview != null;
    public bool IsBuildMode { get; private set; } = true;
    public int PlacedBlockCount => placedBlocks.Count;
    public bool AllBlocksPlaced
    {
        get
        {
            if (blocks.Length == 0)
            {
                return false;
            }

            foreach (BlockDefinition block in blocks)
            {
                if (block == null || block.availableCount < 0 || block.usedCount < block.availableCount)
                {
                    return false;
                }
            }

            return true;
        }
    }

    [Serializable]
    private sealed class BlockDefinition
    {
        [Tooltip("Inspector上で識別するための名前です。")]
        public string displayName;

        [Tooltip("画面上部に表示されているドラッグ元のRectTransformです。")]
        public RectTransform dragSource;

        [Tooltip("フィールドへ複製するワールド側のGameObjectです。SpriteRendererや動作用スクリプトを含められます。")]
        public GameObject worldTemplate;

        [Tooltip("このブロックが横・縦に占有するTilemapセル数です。")]
        public Vector2Int footprint = Vector2Int.one;

        [Tooltip("占有セル全体の中心から生成物のTransform位置へ加える補正値です。")]
        public Vector3 placementOffset;

        [Tooltip("配置できる個数です。-1なら無制限です。")]
        [Min(-1)] public int availableCount = -1;

        [Tooltip("個数を使い切ったとき、画面上部のドラッグ元を非表示にします。")]
        public bool hideSourceWhenExhausted = true;

        [NonSerialized] public int usedCount;
        [NonSerialized] public Vector3 sourceBaseScale;
    }

    private sealed class PlacedBlock
    {
        public GameObject instance;
        public BlockDefinition definition;
        public Vector3Int cell;
        public Vector3 baseScale;
        public SpriteRenderer[] renderers;
        public Color[] baseColors;
        public Material[] baseMaterials;
        public int[] baseSortingOrders;
        public IBlockOperationState[] operationStates;
        public SpriteRenderer[] hoverOutlineRenderers;
        public bool isColorInverted;
    }

    [Header("必須参照")]
    [Tooltip("画面座標をワールド座標へ変換するカメラです。未設定ならMain Cameraを使用します。")]
    [SerializeField] private Camera placementCamera;

    [Tooltip("配置グリッド、配置可能範囲、既存Tileの占有判定に使うTilemapです。")]
    [SerializeField] private Tilemap placementTilemap;

    [Tooltip("配置後のブロックをまとめる親Transformです。未設定ならTilemapと同じ親を使用します。")]
    [SerializeField] private Transform placedBlockParent;

    [Tooltip("プレイヤー開始位置とゴール位置を配置禁止にするためのStageManagerです。")]
    [SerializeField] private StageManager stageManager;

    [Header("配置するブロック")]
    [SerializeField] private BlockDefinition[] blocks = Array.Empty<BlockDefinition>();

    [Header("配置範囲")]
    [Tooltip("配置グリッド左下のTilemapセル座標です。")]
    [SerializeField] private Vector2Int placementGridOrigin = new Vector2Int(-9, -4);

    [Tooltip("配置できるグリッドの大きさです。Xが横、Yが縦です（例: X=8、Y=16で8×16）。")]
    [SerializeField] private Vector2Int placementGridSize = new Vector2Int(18, 16);

    [Header("配置ブロックのTilemap複合")]
    [Tooltip("配置セルへ透明なCollider Tileを追加し、TilemapのCompositeCollider2Dへ統合します。")]
    [SerializeField] private bool mergePlacedBlocksIntoTilemap = true;

    [Header("ドラッグ中の表示")]
    [SerializeField] private Color validPreviewColor = new Color(0.55f, 1f, 0.55f, 0.8f);
    [SerializeField] private Color invalidPreviewColor = new Color(1f, 0.35f, 0.35f, 0.8f);

    [Tooltip("ドラッグ中、マウス位置からプレビュー表示へ加える補正値です。")]
    [SerializeField] private Vector3 dragPreviewOffset;

    [Tooltip("配置ブロックのZ座標です。Tilemapより手前になる値を指定してください。")]
    [SerializeField] private float placedBlockZ = -1f;

    [Tooltip("配置ブロックのSpriteRendererに設定する描画順です。")]
    [SerializeField] private int placedSortingOrder = 1;

    [Tooltip("ドラッグ中のブロック本体の描画順です。色付きグリッド表示より大きい値にします。")]
    [SerializeField] private int draggedBlockSortingOrder = 3;

    [Tooltip("配置予定セルに表示する色付きブロックの描画順です。")]
    [SerializeField] private int gridPreviewSortingOrder = 2;

    [Tooltip("シーン内の生成元GameObjectをプレイ開始時に非表示にします。通常はONにします。")]
    [SerializeField] private bool hideWorldTemplatesAtRuntime = true;

    [Header("上部ブロックのホバー表示")]
    [Tooltip("カーソルを合わせたときの拡大倍率です。1で元のサイズ、1.2で120%になります。")]
    [Min(1f)]
    [SerializeField] private float sourceHoverScaleMultiplier = 1.15f;

    [Tooltip("ホバー時にサイズが変化する速さです。0なら瞬時に切り替わります。")]
    [Min(0f)]
    [SerializeField] private float sourceHoverScaleSpeed;

    [Header("配置済みブロックのホバー表示")]
    [Tooltip("カーソルを合わせた配置済みブロックの拡大倍率です。")]
    [Min(1f)]
    [SerializeField] private float placedHoverScaleMultiplier = 1.15f;

    [Tooltip("配置済みブロックのサイズが変化する速さです。0なら瞬時に切り替わります。")]
    [Min(0f)]
    [SerializeField] private float placedHoverScaleSpeed;

    [Header("プレイ中の操作表示")]
    [Tooltip("操作中の配置ブロックの色を反転します。")]
    [SerializeField] private bool invertOperatingBlockColors = true;

    [Tooltip("色反転に使用するSprite用Shaderです。")]
    [SerializeField] private Shader operationInversionShader;

    [Header("プレイ中のブロックホバー縁取り")]
    [SerializeField] private bool showPlayModeHoverOutline = true;

    [SerializeField] private Color playModeHoverOutlineColor = Color.white;

    [SerializeField] private Shader playModeHoverOutlineShader;

    [Tooltip("縁取りを元スプライトから外側へずらすワールド座標上の距離です。")]
    [Min(0f)]
    [SerializeField] private float playModeHoverOutlineWidth = 0.06f;

    [Tooltip("縁取りのSorting Orderです。ホバー対象本体はこの値より1つ手前に表示されます。")]
    [SerializeField] private int playModeHoverOutlineSortingOrder = 10;

    private readonly HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
    private readonly List<PlacedBlock> placedBlocks = new List<PlacedBlock>();
    private readonly List<SpriteRenderer> previewRenderers = new List<SpriteRenderer>();
    private readonly List<Color> previewOriginalColors = new List<Color>();
    private readonly List<Collider2D> previewColliders = new List<Collider2D>();
    private readonly List<bool> previewColliderStates = new List<bool>();
    private readonly List<MonoBehaviour> previewBehaviours = new List<MonoBehaviour>();
    private readonly List<bool> previewBehaviourStates = new List<bool>();
    private readonly List<SpriteRenderer> gridPreviewRenderers = new List<SpriteRenderer>();
    private readonly List<Color> gridPreviewOriginalColors = new List<Color>();
    private readonly List<IBlockOperationState> pointerOperationStates = new List<IBlockOperationState>();

    private Tile runtimeColliderTile;
    private TilemapCollider2D placementTilemapCollider;
    private Material operationInversionMaterial;
    private Material playModeHoverOutlineMaterial;

    private static readonly Vector2[] HoverOutlineDirections =
    {
        Vector2.right,
        Vector2.left,
        Vector2.up,
        Vector2.down,
        new Vector2(1f, 1f).normalized,
        new Vector2(1f, -1f).normalized,
        new Vector2(-1f, 1f).normalized,
        new Vector2(-1f, -1f).normalized
    };

    private BlockDefinition activeDefinition;
    private GameObject activePreview;
    private GameObject activeGridPreview;
    private PlacedBlock activePlacedBlock;
    private Vector3Int activeCell;
    private bool activeCellIsValid;

    private void Awake()
    {
        if (placementCamera == null)
        {
            placementCamera = Camera.main;
        }

        if (placementTilemap == null)
        {
            placementTilemap = FindFirstObjectByType<Tilemap>();
        }

        if (placedBlockParent == null && placementTilemap != null)
        {
            placedBlockParent = placementTilemap.transform.parent;
        }

        if (mergePlacedBlocksIntoTilemap && placementTilemap != null)
        {
            placementTilemapCollider = placementTilemap.GetComponent<TilemapCollider2D>();
            runtimeColliderTile = ScriptableObject.CreateInstance<Tile>();
            runtimeColliderTile.name = "Placed Block Collider Tile";
            runtimeColliderTile.colliderType = Tile.ColliderType.Grid;
            runtimeColliderTile.hideFlags = HideFlags.HideAndDontSave;
        }

        if (operationInversionShader != null)
        {
            operationInversionMaterial = new Material(operationInversionShader)
            {
                name = "Block Operation Inversion",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (playModeHoverOutlineShader == null)
        {
            playModeHoverOutlineShader = Shader.Find("UIBuilder/SpriteSolidColor");
        }

        if (playModeHoverOutlineShader != null)
        {
            playModeHoverOutlineMaterial = new Material(playModeHoverOutlineShader)
            {
                name = "Block Hover Outline",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (stageManager == null)
        {
            stageManager = FindFirstObjectByType<StageManager>();
        }

        if (hideWorldTemplatesAtRuntime)
        {
            foreach (BlockDefinition block in blocks)
            {
                if (block != null && block.worldTemplate != null)
                {
                    block.worldTemplate.SetActive(false);
                }
            }
        }

        foreach (BlockDefinition block in blocks)
        {
            if (block != null && block.dragSource != null)
            {
                block.sourceBaseScale = block.dragSource.localScale;
            }
        }
    }

    private void Update()
    {
        if (!IsBuildMode)
        {
            UpdateOperationColors();
            UpdatePlayModeHoverOutline();
            return;
        }

        HideAllPlayModeHoverOutlines();
        UpdateSourceHover(Input.mousePosition, Input.touchCount == 0);
        UpdatePlacedBlockHover(Input.mousePosition, Input.touchCount == 0 && !IsPointerOverUi());

        if (!TryGetPointerState(out Vector2 screenPosition, out PointerPhase phase))
        {
            return;
        }

        if (phase == PointerPhase.Began)
        {
            if (!TryBeginDrag(screenPosition) && !IsPointerOverUi())
            {
                TryBeginPlacedBlockDrag(screenPosition);
            }
        }

        if (activePreview == null)
        {
            return;
        }

        UpdatePreview(screenPosition);

        if (phase == PointerPhase.Ended)
        {
            EndDrag();
        }
    }

    private void UpdatePlacedBlockHover(Vector2 screenPosition, bool allowHover)
    {
        PlacedBlock hovered = allowHover && activePreview == null
            ? FindPlacedBlock(screenPosition)
            : null;
        float interpolation = placedHoverScaleSpeed <= 0f
            ? 1f
            : 1f - Mathf.Exp(-placedHoverScaleSpeed * Time.unscaledDeltaTime);

        foreach (PlacedBlock block in placedBlocks)
        {
            if (block.instance == null)
            {
                continue;
            }

            Vector3 target = block.baseScale *
                             (block == hovered ? Mathf.Max(1f, placedHoverScaleMultiplier) : 1f);
            block.instance.transform.localScale = Vector3.Lerp(
                block.instance.transform.localScale,
                target,
                interpolation);
        }
    }

    private void UpdateSourceHover(Vector2 screenPosition, bool allowHover)
    {
        float interpolation = sourceHoverScaleSpeed <= 0f
            ? 1f
            : 1f - Mathf.Exp(-sourceHoverScaleSpeed * Time.unscaledDeltaTime);

        foreach (BlockDefinition block in blocks)
        {
            if (block == null || block.dragSource == null)
            {
                continue;
            }

            bool isHovered = allowHover &&
                             block.dragSource.gameObject.activeInHierarchy &&
                             RectTransformUtility.RectangleContainsScreenPoint(
                                 block.dragSource,
                                 screenPosition,
                                 GetUiCamera(block.dragSource));
            Vector3 targetScale = block.sourceBaseScale *
                                  (isHovered ? Mathf.Max(1f, sourceHoverScaleMultiplier) : 1f);
            block.dragSource.localScale = Vector3.Lerp(
                block.dragSource.localScale,
                targetScale,
                interpolation);
        }
    }

    private bool TryBeginDrag(Vector2 screenPosition)
    {
        if (placementCamera == null || placementTilemap == null)
        {
            Debug.LogWarning("BlockManager: CameraまたはTilemapが設定されていません。", this);
            return false;
        }

        foreach (BlockDefinition block in blocks)
        {
            if (!CanCreate(block) ||
                !RectTransformUtility.RectangleContainsScreenPoint(block.dragSource, screenPosition, GetUiCamera(block.dragSource)))
            {
                continue;
            }

            GameObject preview = Instantiate(block.worldTemplate, placedBlockParent);
            preview.name = string.IsNullOrWhiteSpace(block.displayName)
                ? block.worldTemplate.name
                : block.displayName;
            BeginWorldDrag(block, preview);

            block.dragSource.gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    private bool TryBeginPlacedBlockDrag(Vector2 screenPosition)
    {
        PlacedBlock block = FindPlacedBlock(screenPosition);
        if (block == null)
        {
            return false;
        }

        ApplyOperationColor(block, false);
        DestroyPlayModeHoverOutlines(block);
        activePlacedBlock = block;
        block.instance.transform.localScale = block.baseScale;
        placedBlocks.Remove(block);
        RemoveOccupiedCells(block.definition, block.cell);
        BeginWorldDrag(block.definition, block.instance);
        return true;
    }

    private void BeginWorldDrag(BlockDefinition definition, GameObject preview)
    {
        activeDefinition = definition;
        activePreview = preview;
        activeGridPreview = Instantiate(definition.worldTemplate, placedBlockParent);
        activeGridPreview.name = $"{activePreview.name} (Grid Preview)";

        CacheAndDisablePreviewComponents();
        CachePreviewRenderers();
        PrepareGridPreview();
        activePreview.SetActive(true);
        activeGridPreview.SetActive(true);
        DragStateChanged?.Invoke(true);
    }

    private PlacedBlock FindPlacedBlock(Vector2 screenPosition)
    {
        Vector3 point = ScreenToWorld(screenPosition);
        for (int i = placedBlocks.Count - 1; i >= 0; i--)
        {
            PlacedBlock block = placedBlocks[i];
            if (block.instance == null)
            {
                continue;
            }

            Collider2D[] colliders = block.instance.GetComponentsInChildren<Collider2D>();
            foreach (Collider2D collider in colliders)
            {
                if (collider.enabled && collider.OverlapPoint(point))
                {
                    return block;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 指定した画面座標に配置済みブロックがあるかを返します。
    /// </summary>
    public bool HasPlacedBlockAtScreenPosition(Vector2 screenPosition) =>
        !IsBuildMode &&
        placementCamera != null &&
        placementTilemap != null &&
        FindPlacedBlock(screenPosition) != null;

    /// <summary>
    /// 長押し候補中にカーソルへ入った配置済みブロックの動作を開始します。
    /// </summary>
    public bool TryBeginPlacedBlockOperation(Vector2 screenPosition)
    {
        if (IsBuildMode || placementCamera == null || placementTilemap == null)
        {
            return false;
        }

        PlacedBlock block = FindPlacedBlock(screenPosition);
        if (block == null)
        {
            return false;
        }

        EndPlacedBlockOperation();
        foreach (IBlockOperationState state in block.operationStates)
        {
            state.BeginOperation();
            pointerOperationStates.Add(state);
        }

        return true;
    }

    /// <summary>
    /// MainManagerから開始したブロック動作を終了します。
    /// </summary>
    public void EndPlacedBlockOperation()
    {
        foreach (IBlockOperationState state in pointerOperationStates)
        {
            state.CancelOperation();
        }

        pointerOperationStates.Clear();
    }

    private Vector3 ScreenToWorld(Vector2 screenPosition)
    {
        float depth = Mathf.Abs(placementCamera.transform.position.z - placementTilemap.transform.position.z);
        return placementCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
    }

    private void UpdatePreview(Vector2 screenPosition)
    {
        Vector3 worldPoint = ScreenToWorld(screenPosition);

        Vector2Int footprint = GetValidFootprint(activeDefinition);
        Vector3 footprintSelectionOffset =
            (placementTilemap.CellToWorld(new Vector3Int(footprint.x - 1, footprint.y - 1, 0)) -
             placementTilemap.CellToWorld(Vector3Int.zero)) * 0.5f;
        activeCell = placementTilemap.WorldToCell(worldPoint - footprintSelectionOffset);

        // ドラッグ中はグリッドへ吸着させず、カーソルへ滑らかに追従させます。
        Vector3 previewPosition = worldPoint + dragPreviewOffset;
        previewPosition.z = placedBlockZ;
        activePreview.transform.position = previewPosition;

        activeCellIsValid = CanPlace(activeDefinition, activeCell);
        activeGridPreview.transform.position = GetSnappedPosition(activeDefinition, activeCell);
        ApplyGridPreviewColor(activeCellIsValid ? validPreviewColor : invalidPreviewColor);
    }

    private void EndDrag()
    {
        if (activeCellIsValid)
        {
            PlaceActiveBlock(activeCell);
        }
        else if (activePlacedBlock != null)
        {
            PlaceActiveBlock(activePlacedBlock.cell);
        }
        else
        {
            Destroy(activePreview);
            Destroy(activeGridPreview);
            activeDefinition.dragSource.gameObject.SetActive(true);
        }

        ClearDragState();
    }

    private void PlaceActiveBlock(Vector3Int cell)
    {
        bool isNew = activePlacedBlock == null;
        activePreview.transform.position = GetSnappedPosition(activeDefinition, cell);
        RestorePreviewAppearance();
        RestorePreviewComponents();
        SetPlacedCollidersAsTriggers();
        Destroy(activeGridPreview);
        RegisterOccupiedCells(activeDefinition, cell);

        PlacedBlock block = activePlacedBlock ?? CreatePlacedBlock();
        block.cell = cell;
        block.instance.transform.localScale = block.baseScale;
        placedBlocks.Add(block);
        CreatePlayModeHoverOutlines(block);

        if (!isNew)
        {
            return;
        }

        activeDefinition.usedCount++;
        if (activeDefinition.availableCount >= 0 &&
            activeDefinition.usedCount >= activeDefinition.availableCount &&
            activeDefinition.hideSourceWhenExhausted)
        {
            activeDefinition.dragSource.gameObject.SetActive(false);
        }
    }

    public void SetBuildMode(bool isBuildMode)
    {
        EndPlacedBlockOperation();
        HideAllPlayModeHoverOutlines();
        IsBuildMode = isBuildMode;

        foreach (PlacedBlock block in placedBlocks)
        {
            ApplyOperationColor(block, false);

            if (!isBuildMode)
            {
                continue;
            }

            foreach (IBlockOperationState state in block.operationStates)
            {
                state.CancelOperation();
            }
        }
    }

    private PlacedBlock CreatePlacedBlock()
    {
        SpriteRenderer[] renderers = activePreview.GetComponentsInChildren<SpriteRenderer>(true);
        Color[] colors = new Color[renderers.Length];
        Material[] materials = new Material[renderers.Length];
        int[] sortingOrders = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            colors[i] = renderers[i].color;
            materials[i] = renderers[i].sharedMaterial;
            sortingOrders[i] = renderers[i].sortingOrder;
        }

        MonoBehaviour[] behaviours = activePreview.GetComponentsInChildren<MonoBehaviour>(true);
        List<IBlockOperationState> states = new List<IBlockOperationState>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IBlockOperationState state)
            {
                states.Add(state);
            }
        }

        return new PlacedBlock
        {
            instance = activePreview,
            definition = activeDefinition,
            baseScale = activePreview.transform.localScale,
            renderers = renderers,
            baseColors = colors,
            baseMaterials = materials,
            baseSortingOrders = sortingOrders,
            operationStates = states.ToArray(),
            hoverOutlineRenderers = Array.Empty<SpriteRenderer>()
        };
    }

    private void UpdateOperationColors()
    {
        foreach (PlacedBlock block in placedBlocks)
        {
            bool isOperating = false;
            foreach (IBlockOperationState state in block.operationStates)
            {
                if (state.IsOperating)
                {
                    isOperating = true;
                    break;
                }
            }

            ApplyOperationColor(block, invertOperatingBlockColors && isOperating);
        }
    }

    private void UpdatePlayModeHoverOutline()
    {
        bool allowHover = showPlayModeHoverOutline &&
                          Input.touchCount == 0 &&
                          !Input.GetMouseButton(0) &&
                          !IsPointerOverUi();
        PlacedBlock hoveredBlock = allowHover
            ? FindPlacedBlock(Input.mousePosition)
            : null;

        foreach (PlacedBlock block in placedBlocks)
        {
            SetPlayModeHoverOutline(
                block,
                block == hoveredBlock && !IsBlockOperating(block));
        }
    }

    private static bool IsBlockOperating(PlacedBlock block)
    {
        foreach (IBlockOperationState state in block.operationStates)
        {
            if (state.IsOperating)
            {
                return true;
            }
        }

        return false;
    }

    private void CreatePlayModeHoverOutlines(PlacedBlock block)
    {
        DestroyPlayModeHoverOutlines(block);
        if (!showPlayModeHoverOutline || block.renderers == null)
        {
            return;
        }

        block.hoverOutlineRenderers = new SpriteRenderer[
            block.renderers.Length * HoverOutlineDirections.Length];
        for (int i = 0; i < block.renderers.Length; i++)
        {
            SpriteRenderer source = block.renderers[i];
            if (source == null)
            {
                continue;
            }

            for (int directionIndex = 0;
                 directionIndex < HoverOutlineDirections.Length;
                 directionIndex++)
            {
                int outlineIndex = i * HoverOutlineDirections.Length + directionIndex;
                GameObject outlineObject =
                    new GameObject($"{source.name} (Hover Outline {directionIndex + 1})");
                outlineObject.layer = source.gameObject.layer;
                SpriteRenderer outline = outlineObject.AddComponent<SpriteRenderer>();
                outline.sprite = source.sprite;
                outline.color = playModeHoverOutlineColor;
                outline.flipX = source.flipX;
                outline.flipY = source.flipY;
                outline.drawMode = source.drawMode;
                outline.size = source.size;
                outline.maskInteraction = source.maskInteraction;
                outline.sortingLayerID = source.sortingLayerID;
                outline.sortingOrder = playModeHoverOutlineSortingOrder;
                outline.sharedMaterial = playModeHoverOutlineMaterial != null
                    ? playModeHoverOutlineMaterial
                    : source.sharedMaterial;
                outlineObject.SetActive(false);
                block.hoverOutlineRenderers[outlineIndex] = outline;
            }
        }
    }

    private void SetPlayModeHoverOutline(PlacedBlock block, bool visible)
    {
        if (block.hoverOutlineRenderers == null)
        {
            return;
        }

        for (int i = 0; i < block.renderers.Length; i++)
        {
            SpriteRenderer source = block.renderers[i];
            if (source != null)
            {
                source.sortingOrder = visible
                    ? playModeHoverOutlineSortingOrder + 1
                    : block.baseSortingOrders[i];
            }
        }

        for (int i = 0; i < block.hoverOutlineRenderers.Length; i++)
        {
            SpriteRenderer outline = block.hoverOutlineRenderers[i];
            int sourceIndex = i / HoverOutlineDirections.Length;
            int directionIndex = i % HoverOutlineDirections.Length;
            SpriteRenderer source = sourceIndex < block.renderers.Length
                ? block.renderers[sourceIndex]
                : null;
            if (outline == null || source == null)
            {
                continue;
            }

            if (visible)
            {
                Transform outlineTransform = outline.transform;
                Transform sourceTransform = source.transform;
                Vector2 offset =
                    HoverOutlineDirections[directionIndex] * playModeHoverOutlineWidth;
                outlineTransform.position = sourceTransform.position +
                                            new Vector3(offset.x, offset.y, 0f);
                outlineTransform.rotation = sourceTransform.rotation;
                outlineTransform.localScale = sourceTransform.lossyScale;
            }

            outline.gameObject.SetActive(
                visible &&
                source.enabled &&
                source.gameObject.activeInHierarchy);
        }
    }

    private void HideAllPlayModeHoverOutlines()
    {
        foreach (PlacedBlock block in placedBlocks)
        {
            SetPlayModeHoverOutline(block, false);
        }
    }

    private static void DestroyPlayModeHoverOutlines(PlacedBlock block)
    {
        if (block == null || block.hoverOutlineRenderers == null)
        {
            return;
        }

        if (block.baseSortingOrders != null)
        {
            for (int i = 0; i < block.renderers.Length; i++)
            {
                if (block.renderers[i] != null && i < block.baseSortingOrders.Length)
                {
                    block.renderers[i].sortingOrder = block.baseSortingOrders[i];
                }
            }
        }

        foreach (SpriteRenderer outline in block.hoverOutlineRenderers)
        {
            if (outline != null)
            {
                outline.gameObject.SetActive(false);
                Destroy(outline.gameObject);
            }
        }

        block.hoverOutlineRenderers = Array.Empty<SpriteRenderer>();
    }

    private void ApplyOperationColor(PlacedBlock block, bool inverted)
    {
        if (block == null || block.isColorInverted == inverted)
        {
            return;
        }

        for (int i = 0; i < block.renderers.Length; i++)
        {
            Color color = block.baseColors[i];
            block.renderers[i].sharedMaterial = inverted && operationInversionMaterial != null
                ? operationInversionMaterial
                : block.baseMaterials[i];
            block.renderers[i].color = inverted && operationInversionMaterial == null
                ? new Color(1f - color.r, 1f - color.g, 1f - color.b, color.a)
                : color;
        }

        block.isColorInverted = inverted;
    }

    private Vector3 GetSnappedPosition(BlockDefinition definition, Vector3Int origin)
    {
        Vector2Int footprint = GetValidFootprint(definition);
        Vector3 footprintMin = placementTilemap.CellToWorld(origin);
        Vector3 footprintMax = placementTilemap.CellToWorld(
            origin + new Vector3Int(footprint.x, footprint.y, 0));
        Vector3 position = (footprintMin + footprintMax) * 0.5f + definition.placementOffset;
        position.z = placedBlockZ;
        return position;
    }

    private bool CanPlace(BlockDefinition definition, Vector3Int origin)
    {
        Vector2Int footprint = GetValidFootprint(definition);

        for (int y = 0; y < footprint.y; y++)
        {
            for (int x = 0; x < footprint.x; x++)
            {
                Vector3Int cell = origin + new Vector3Int(x, y, 0);
                if (!IsInsidePlacementBounds(cell) ||
                    placementTilemap.HasTile(cell) ||
                    (stageManager != null && stageManager.IsBlockPlacementReservedCell(cell)) ||
                    occupiedCells.Contains(cell))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsInsidePlacementBounds(Vector3Int cell)
    {
        int width = Mathf.Max(1, placementGridSize.x);
        int height = Mathf.Max(1, placementGridSize.y);

        bool isInsideConfiguredGrid =
            cell.x >= placementGridOrigin.x &&
            cell.y >= placementGridOrigin.y &&
            cell.x < placementGridOrigin.x + width &&
            cell.y < placementGridOrigin.y + height;

        // 指定サイズに加え、参照Tilemap自体のセル範囲からも外れないようにします。
        return isInsideConfiguredGrid && placementTilemap.cellBounds.Contains(cell);
    }

    private void OnValidate()
    {
        placementGridSize.x = Mathf.Max(1, placementGridSize.x);
        placementGridSize.y = Mathf.Max(1, placementGridSize.y);
        sourceHoverScaleMultiplier = Mathf.Max(1f, sourceHoverScaleMultiplier);
        sourceHoverScaleSpeed = Mathf.Max(0f, sourceHoverScaleSpeed);
        placedHoverScaleMultiplier = Mathf.Max(1f, placedHoverScaleMultiplier);
        placedHoverScaleSpeed = Mathf.Max(0f, placedHoverScaleSpeed);
        playModeHoverOutlineWidth = Mathf.Max(0f, playModeHoverOutlineWidth);
    }

    private void OnDrawGizmosSelected()
    {
        if (placementTilemap == null)
        {
            return;
        }

        Vector3 min = placementTilemap.CellToWorld(
            new Vector3Int(placementGridOrigin.x, placementGridOrigin.y, 0));
        Vector3 max = placementTilemap.CellToWorld(new Vector3Int(
            placementGridOrigin.x + Mathf.Max(1, placementGridSize.x),
            placementGridOrigin.y + Mathf.Max(1, placementGridSize.y),
            0));

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube((min + max) * 0.5f, max - min);
    }

    private void RegisterOccupiedCells(BlockDefinition definition, Vector3Int origin)
    {
        Vector2Int footprint = GetValidFootprint(definition);
        for (int y = 0; y < footprint.y; y++)
        {
            for (int x = 0; x < footprint.x; x++)
            {
                Vector3Int cell = origin + new Vector3Int(x, y, 0);
                occupiedCells.Add(cell);
                SetCompositeTile(cell, runtimeColliderTile);
            }
        }

        RefreshCompositeCollider();
    }

    private void RemoveOccupiedCells(BlockDefinition definition, Vector3Int origin)
    {
        Vector2Int footprint = GetValidFootprint(definition);
        for (int y = 0; y < footprint.y; y++)
        {
            for (int x = 0; x < footprint.x; x++)
            {
                Vector3Int cell = origin + new Vector3Int(x, y, 0);
                occupiedCells.Remove(cell);

                if (placementTilemap != null && placementTilemap.GetTile(cell) == runtimeColliderTile)
                {
                    SetCompositeTile(cell, null);
                }
            }
        }

        RefreshCompositeCollider();
    }

    private void SetCompositeTile(Vector3Int cell, TileBase tile)
    {
        if (mergePlacedBlocksIntoTilemap && placementTilemap != null && runtimeColliderTile != null)
        {
            placementTilemap.SetTile(cell, tile);
        }
    }

    private void RefreshCompositeCollider()
    {
        if (placementTilemapCollider != null && placementTilemapCollider.hasTilemapChanges)
        {
            placementTilemapCollider.ProcessTilemapChanges();
        }
    }

    private static Vector2Int GetValidFootprint(BlockDefinition definition)
    {
        return new Vector2Int(Mathf.Max(1, definition.footprint.x), Mathf.Max(1, definition.footprint.y));
    }

    private static bool CanCreate(BlockDefinition definition)
    {
        return definition != null && definition.dragSource != null && definition.worldTemplate != null &&
               (definition.availableCount < 0 || definition.usedCount < definition.availableCount);
    }

    private static Camera GetUiCamera(RectTransform source)
    {
        Canvas canvas = source.GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private void CachePreviewRenderers()
    {
        previewRenderers.Clear();
        previewOriginalColors.Clear();

        activePreview.GetComponentsInChildren(true, previewRenderers);
        foreach (SpriteRenderer spriteRenderer in previewRenderers)
        {
            previewOriginalColors.Add(spriteRenderer.color);
            spriteRenderer.sortingOrder = draggedBlockSortingOrder;
        }
    }

    private void PrepareGridPreview()
    {
        gridPreviewRenderers.Clear();
        gridPreviewOriginalColors.Clear();

        activeGridPreview.GetComponentsInChildren(true, gridPreviewRenderers);
        foreach (SpriteRenderer spriteRenderer in gridPreviewRenderers)
        {
            gridPreviewOriginalColors.Add(spriteRenderer.color);
            spriteRenderer.sortingOrder = gridPreviewSortingOrder;
        }

        Collider2D[] colliders = activeGridPreview.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D previewCollider in colliders)
        {
            previewCollider.enabled = false;
        }

        MonoBehaviour[] behaviours = activeGridPreview.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            behaviour.enabled = false;
        }
    }

    private void CacheAndDisablePreviewComponents()
    {
        previewColliders.Clear();
        previewColliderStates.Clear();
        previewBehaviours.Clear();
        previewBehaviourStates.Clear();

        activePreview.GetComponentsInChildren(true, previewColliders);
        foreach (Collider2D previewCollider in previewColliders)
        {
            previewColliderStates.Add(previewCollider.enabled);
            previewCollider.enabled = false;
        }

        activePreview.GetComponentsInChildren(true, previewBehaviours);
        foreach (MonoBehaviour behaviour in previewBehaviours)
        {
            previewBehaviourStates.Add(behaviour.enabled);
            behaviour.enabled = false;
        }
    }

    private void ApplyGridPreviewColor(Color tint)
    {
        for (int i = 0; i < gridPreviewRenderers.Count; i++)
        {
            Color original = gridPreviewOriginalColors[i];
            gridPreviewRenderers[i].color = new Color(
                original.r * tint.r,
                original.g * tint.g,
                original.b * tint.b,
                original.a * tint.a);
        }
    }

    private void RestorePreviewAppearance()
    {
        for (int i = 0; i < previewRenderers.Count; i++)
        {
            previewRenderers[i].color = previewOriginalColors[i];
            previewRenderers[i].sortingOrder = placedSortingOrder;
        }
    }

    private void RestorePreviewComponents()
    {
        for (int i = 0; i < previewColliders.Count; i++)
        {
            previewColliders[i].enabled = previewColliderStates[i];
        }

        for (int i = 0; i < previewBehaviours.Count; i++)
        {
            previewBehaviours[i].enabled = previewBehaviourStates[i];
        }
    }

    private void SetPlacedCollidersAsTriggers()
    {
        if (!mergePlacedBlocksIntoTilemap)
        {
            return;
        }

        foreach (Collider2D placedCollider in previewColliders)
        {
            placedCollider.isTrigger = true;
        }
    }

    private void OnDestroy()
    {
        foreach (PlacedBlock block in placedBlocks)
        {
            DestroyPlayModeHoverOutlines(block);
        }

        if (runtimeColliderTile != null)
        {
            Destroy(runtimeColliderTile);
        }

        if (operationInversionMaterial != null)
        {
            Destroy(operationInversionMaterial);
        }

        if (playModeHoverOutlineMaterial != null)
        {
            Destroy(playModeHoverOutlineMaterial);
        }
    }

    private void OnDisable()
    {
        EndPlacedBlockOperation();
        HideAllPlayModeHoverOutlines();

        if (activePreview != null)
        {
            if (activePlacedBlock != null)
            {
                PlaceActiveBlock(activePlacedBlock.cell);
            }
            else
            {
                Destroy(activePreview);
                Destroy(activeGridPreview);
                activeDefinition.dragSource.gameObject.SetActive(true);
            }

            ClearDragState();
        }

        foreach (BlockDefinition block in blocks)
        {
            if (block != null && block.dragSource != null && block.sourceBaseScale != Vector3.zero)
            {
                block.dragSource.localScale = block.sourceBaseScale;
            }
        }

        foreach (PlacedBlock block in placedBlocks)
        {
            ApplyOperationColor(block, false);

            foreach (IBlockOperationState state in block.operationStates)
            {
                state.CancelOperation();
            }

            if (block.instance != null)
            {
                block.instance.transform.localScale = block.baseScale;
            }
        }
    }

    private void ClearDragState()
    {
        bool wasDragging = activePreview != null;
        activeDefinition = null;
        activePreview = null;
        activeGridPreview = null;
        activePlacedBlock = null;
        activeCellIsValid = false;
        previewRenderers.Clear();
        previewOriginalColors.Clear();
        previewColliders.Clear();
        previewColliderStates.Clear();
        previewBehaviours.Clear();
        previewBehaviourStates.Clear();
        gridPreviewRenderers.Clear();
        gridPreviewOriginalColors.Clear();

        if (wasDragging)
        {
            DragStateChanged?.Invoke(false);
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

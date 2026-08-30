using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

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

    public interface IPlayModeBlockState
    {
        void OnPlayModeEntered();
        void OnBuildModeEntered();
    }

    public event Action<bool> DragStateChanged;

    public bool IsDragging => activePreview != null;
    public Color PlayModeHoverOutlineColor => playModeHoverOutlineColor;
    public Shader PlayModeHoverOutlineShader => playModeHoverOutlineShader;
    public float PlayModeHoverOutlineWidth => playModeHoverOutlineWidth;
    public bool IsBuildMode { get; private set; } = true;
    public int PlacedBlockCount => placedBlocks.Count;
    public bool AllBlocksPlaced
    {
        get
        {
            bool hasEnabledBlock = false;
            foreach (BlockDefinition block in blocks)
            {
                if (block == null || !block.isEnabled || block.dragSource == null)
                {
                    continue;
                }

                hasEnabledBlock = true;
                if (block.availableCount < 0 || block.usedCount < block.availableCount)
                {
                    return false;
                }
            }

            return hasEnabledBlock;
        }
    }

    [Serializable]
    private sealed class BlockDefinition
    {
        [Tooltip("Inspector上で識別するための名前です。")]
        public string displayName;

        [HideInInspector]
        public bool isEnabled = true;

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

        [Tooltip("ドラッグ元を構成する表示全体です。未設定ならDrag Sourceだけを表示切替します。")]
        public GameObject sourceVisualRoot;

        [Tooltip("BGM音量を操作する1×4のスクロールバーブロックとして扱います。")]
        public bool isBgmScrollBar;

        [Tooltip("画面の明るさを操作する1×4のスクロールバーブロックとして扱います。")]
        public bool isBrightnessScrollBar;

        [Tooltip("開始時にSTEP 1〜3と横幅1〜3をランダムに選ぶ足場として扱います。")]
        public bool isRandomStepBlock;

        [Tooltip("タップすると上方向へ3×2に展開する候補リスト足場として扱います。")]
        public bool isUpwardDropdownBlock;

        [Tooltip("タップすると上方向へポップアップ足場を展開するSandBox用ブロックとして扱います。")]
        public bool isPopupBlock;

        [Tooltip("TilemapへColliderを統合せず、ブロック自身の可変Colliderを使用します。")]
        public bool usesDynamicCollider;

        [Tooltip("worldTemplate未設定のMoveR/MoveLを自動生成するときの移動速度です。")]
        [Min(0f)] public float moveSpeed = 5f;

        [Tooltip("worldTemplate未設定のJumpを自動生成するときのジャンプ力です。")]
        [Min(0f)] public float jumpPower = 15f;

        [Tooltip("BGMScrollBarの背景デザインに使うImageです。")]
        public Image bgmTrackSource;

        [Tooltip("BGMScrollBarのボタンデザインに使うImageです。")]
        public Image bgmHandleSource;

        [Tooltip("BrightnessScrollBarの四角ハンドル内に表示するアイコンです。")]
        public Sprite brightnessIconSprite;

        [NonSerialized] public int usedCount;
        [NonSerialized] public Vector3 sourceBaseScale;
        [NonSerialized] public Vector3 sourceBaseLocalPosition;
        [NonSerialized] public Vector3 sourceVisualCenterLocal;
        [NonSerialized] public float bgmMaximumVolume;
        [NonSerialized] public bool isBgmVertical;
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
        public IPlayModeBlockState[] playModeStates;
        public SpriteRenderer[] hoverOutlineRenderers;
        public bool isColorInverted;
        public Transform bgmHandle;
        public Collider2D bgmHandleCollider;
        public SpriteRenderer bgmTrackRenderer;
        public SpriteRenderer bgmTrackRightRenderer;
        public SpriteRenderer bgmTrackOutlineRenderer;
        public Color bgmTrackBaseColor;
        public int bgmTrackBaseSortingLayerId;
        public int bgmTrackBaseSortingOrder;
        public float bgmNormalizedValue = 1f;
        public float bgmMaximumVolume;
        public bool isBgmVertical;
        public BrightnessVisibilityVisual brightnessVisibilityVisual;
    }

    private sealed class BrightnessVisibilityVisual
    {
        public RectTransform root;
        public RectTransform trackOutline;
        public RectTransform trackLeft;
        public RectTransform trackRight;
        public RectTransform handle;
        public RectTransform handleFill;
        public RectTransform icon;
        public Image trackLeftImage;
        public Image trackRightImage;
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

    [Tooltip("BGMScrollBarのハンドル上で追従させるプレイヤーのRigidbody2Dです。未設定ならPlayerから取得します。")]
    [SerializeField] private Rigidbody2D playerBody;

    [Tooltip("BGMScrollBarのハンドル上面への接地判定に使うプレイヤーColliderです。未設定ならPlayerから取得します。")]
    [SerializeField] private Collider2D playerCollider;

    [Tooltip("プレイ中にBGMScrollBarのTrackを奥へ配置する基準となるPlayerのSpriteRendererです。")]
    [SerializeField] private SpriteRenderer playerRenderer;

    [Tooltip("BrightnessScrollBarから画面の明るさを変更するコントローラーです。")]
    [SerializeField] private ScreenBrightnessController brightnessController;

    [Tooltip("回転可能なブロックをドラッグしている間だけ表示するCanvas上のPopです。")]
    [SerializeField] private GameObject rotationDragPop;

    [Header("配置するブロック")]
    [SerializeField] private BlockDefinition[] blocks = Array.Empty<BlockDefinition>();

    [SerializeField, HideInInspector] private int blockAvailabilityVersion;

    [Header("BGMScrollBar")]
    [Tooltip("プレイモード中、ハンドル右側にあるTrackの不透明度です。0で完全透明、1で不透明です。")]
    [Range(0f, 1f)]
    [SerializeField] private float bgmTrackRightOpacity = 0.25f;

    [Tooltip("BGMScrollBarのTrack全体に使用する色です。右側にはこの色とRight Opacityの両方が適用されます。")]
    [SerializeField] private Color bgmTrackColor = new Color(0.23137257f, 0.23137257f, 0.23137257f, 1f);

    [Tooltip("BGMScrollBarをドラッグ中、配置予定位置の透過表示に使用するSpriteです。")]
    [SerializeField] private Sprite bgmScrollBarShadowSprite;

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
    private Texture2D runtimeSolidTexture;
    private Sprite runtimeSolidSprite;
    private Texture2D runtimeVariableBlockFrameTexture;
    private Sprite runtimeVariableBlockFrameSprite;
    private Texture2D runtimeVariableBlockCircleTexture;
    private Sprite runtimeVariableBlockCircleSprite;
    private PlacedBlock activeBgmScrollBar;
    private PlacedBlock pressedPlayModeBlock;
    private bool isPlayerAttachedToBgmHandle;
    private Vector2 playerBgmHandleOffset;
    private bool hasBgmScrollBarResetVolume;
    private float bgmScrollBarResetVolume;

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
    private bool activeGridPreviewUsesBgmShadow;
    private PlacedBlock activePlacedBlock;
    private Vector3Int activeCell;
    private bool activeCellIsValid;
    private Vector3Int lastCursorSoundCell;
    private bool hasLastCursorSoundCell;

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

        if (brightnessController == null)
        {
            brightnessController = FindFirstObjectByType<ScreenBrightnessController>();
        }

        FindPlayerPhysicsReferences();
        SetRotationDragPopVisible(false);

        foreach (BlockDefinition block in blocks)
        {
            if (block == null)
            {
                continue;
            }

            bool isEnabled = block.isEnabled && block.dragSource != null;
            if (block.dragSource != null)
            {
                SetSourceActive(block, isEnabled);
            }

            if (!isEnabled)
            {
                continue;
            }

            if (IsScrollBar(block))
            {
                PrepareBgmScrollBarDefinition(block);
            }
            else if (IsDynamicPlatform(block))
            {
                PrepareDynamicPlatformDefinition(block);
            }
            else
            {
                PrepareBuiltInBlockDefinition(block);
            }
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
            if (block != null && block.isEnabled && block.dragSource != null)
            {
                Transform sourceTransform = GetSourceTransform(block);
                block.sourceBaseScale = sourceTransform.localScale;
                block.sourceBaseLocalPosition = sourceTransform.localPosition;
                block.sourceVisualCenterLocal = block.sourceVisualRoot != null
                    ? RectTransformUtility.CalculateRelativeRectTransformBounds(sourceTransform).center
                    : Vector3.zero;
            }
        }
    }

    private void Update()
    {
        UpdateBgmTrackVisuals(IsBuildMode);
        UpdateBrightnessVisibilityVisuals();

        if (!IsBuildMode)
        {
            UpdateBgmScrollBarInteraction();
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

        if (Input.GetMouseButtonDown(1) && CanRotate(activeDefinition))
        {
            ToggleActiveBgmOrientation();
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
            ? FindPlacedBlockForBuildMode(screenPosition)
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
            if (block == null || !block.isEnabled || block.dragSource == null)
            {
                continue;
            }

            bool isHovered = allowHover &&
                             GetSourceObject(block).activeInHierarchy &&
                             RectTransformUtility.RectangleContainsScreenPoint(
                                 block.dragSource,
                                 screenPosition,
                                 GetUiCamera(block.dragSource));
            Vector3 targetScale = block.sourceBaseScale *
                                  (isHovered ? Mathf.Max(1f, sourceHoverScaleMultiplier) : 1f);
            Transform sourceTransform = GetSourceTransform(block);
            Vector3 nextScale = Vector3.Lerp(
                sourceTransform.localScale,
                targetScale,
                interpolation);
            sourceTransform.localScale = nextScale;

            if (block.sourceVisualRoot != null)
            {
                Vector3 centerCompensation = Vector3.Scale(
                    block.sourceVisualCenterLocal,
                    block.sourceBaseScale - nextScale);
                sourceTransform.localPosition =
                    block.sourceBaseLocalPosition + centerCompensation;
            }
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
            if (block.isBgmScrollBar)
            {
                if (!hasBgmScrollBarResetVolume)
                {
                    bgmScrollBarResetVolume = AudioManager.CurrentBgmVolume;
                    hasBgmScrollBarResetVolume = true;
                }

                block.bgmMaximumVolume = AudioManager.CurrentBgmVolume;
            }

            BeginWorldDrag(block, preview);

            SetSourceActive(block, false);
            return true;
        }

        return false;
    }

    private bool TryBeginPlacedBlockDrag(Vector2 screenPosition)
    {
        PlacedBlock block = FindPlacedBlockForBuildMode(screenPosition);
        if (block == null)
        {
            return false;
        }

        ApplyOperationColor(block, false);
        DestroyPlayModeHoverOutlines(block);
        activePlacedBlock = block;
        block.instance.transform.localScale = block.baseScale;
        if (IsScrollBar(block.definition))
        {
            block.definition.isBgmVertical = block.isBgmVertical;
            UpdateBgmFootprint(block.definition);
        }

        placedBlocks.Remove(block);
        SetBrightnessVisibilityActive(block, false);
        RemoveOccupiedCells(block.definition, block.cell);
        BeginWorldDrag(block.definition, block.instance);
        return true;
    }

    private void BeginWorldDrag(BlockDefinition definition, GameObject preview)
    {
        activeDefinition = definition;
        activePreview = preview;
        activeGridPreview = CreateGridPreview(definition);
        activeGridPreview.name = $"{activePreview.name} (Grid Preview)";

        if (IsScrollBar(definition))
        {
            ApplyBgmOrientation(activePreview, definition.isBgmVertical, definition.isBrightnessScrollBar);
            ApplyBgmOrientation(activeGridPreview, definition.isBgmVertical, definition.isBrightnessScrollBar);
        }

        CacheAndDisablePreviewComponents();
        CachePreviewRenderers();
        PrepareGridPreview();
        activePreview.SetActive(true);
        activeGridPreview.SetActive(true);
        hasLastCursorSoundCell = false;
        SetRotationDragPopVisible(CanRotate(definition));
        DragStateChanged?.Invoke(true);
    }

    private GameObject CreateGridPreview(BlockDefinition definition)
    {
        activeGridPreviewUsesBgmShadow = definition.isBgmScrollBar &&
                                         bgmScrollBarShadowSprite != null;
        if (!activeGridPreviewUsesBgmShadow)
        {
            return Instantiate(definition.worldTemplate, placedBlockParent);
        }

        GameObject shadow = new GameObject("BGMScrollBar Shadow");
        shadow.layer = activePreview != null ? activePreview.layer : gameObject.layer;
        shadow.transform.SetParent(placedBlockParent, false);
        SpriteRenderer shadowRenderer = shadow.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = bgmScrollBarShadowSprite;
        return shadow;
    }

    private void ApplyActiveBgmGridPreviewOrientation(bool isVertical)
    {
        if (!activeGridPreviewUsesBgmShadow)
        {
            ApplyBgmOrientation(activeGridPreview, isVertical, activeDefinition.isBrightnessScrollBar);
            return;
        }

        activeGridPreview.transform.localRotation = isVertical
            ? Quaternion.Euler(0f, 0f, 90f)
            : Quaternion.identity;
    }

    private void ToggleActiveBgmOrientation()
    {
        activeDefinition.isBgmVertical = !activeDefinition.isBgmVertical;
        UpdateBgmFootprint(activeDefinition);
        ApplyBgmOrientation(activePreview, activeDefinition.isBgmVertical, activeDefinition.isBrightnessScrollBar);
        ApplyBgmOrientation(activeGridPreview, activeDefinition.isBgmVertical, activeDefinition.isBrightnessScrollBar);
    }

    private static void UpdateBgmFootprint(BlockDefinition definition)
    {
        definition.footprint = definition.isBgmVertical
            ? new Vector2Int(1, 4)
            : new Vector2Int(4, 1);
    }

    private void ApplyBgmOrientation(GameObject target, bool isVertical, bool isBrightnessScrollBar)
    {
        if (target == null)
        {
            return;
        }

        Transform handle = target.transform.Find("Handle");
        if (handle != null)
        {
            handle.localPosition = isVertical
                ? new Vector3(0f, 1.5f, -0.01f)
                : new Vector3(1.5f, 0f, -0.01f);
            handle.localRotation = Quaternion.identity;
        }

        SpriteRenderer track = target.transform.Find("Track")?.GetComponent<SpriteRenderer>();
        if (track != null)
        {
            SetBgmTrackSegment(
                track,
                -1.9f,
                1.9f,
                0.6f,
                isBrightnessScrollBar ? Color.white : bgmTrackColor,
                isVertical);
        }

        SpriteRenderer trackRight = target.transform.Find("TrackRight")?.GetComponent<SpriteRenderer>();
        if (trackRight != null)
        {
            SetBgmTrackSegment(
                trackRight,
                -1.9f,
                1.9f,
                0.6f,
                isBrightnessScrollBar ? new Color(0.22f, 0.22f, 0.22f, 1f) : bgmTrackColor,
                isVertical);
            trackRight.gameObject.SetActive(false);
        }

        SpriteRenderer trackOutline = target.transform.Find("TrackOutline")?.GetComponent<SpriteRenderer>();
        if (trackOutline != null)
        {
            SetBgmTrackSegment(trackOutline, -2f, 2f, 0.76f, Color.black, isVertical);
        }
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

    private PlacedBlock FindPlacedBlockForBuildMode(Vector2 screenPosition)
    {
        Vector3 point = ScreenToWorld(screenPosition);
        for (int i = placedBlocks.Count - 1; i >= 0; i--)
        {
            PlacedBlock block = placedBlocks[i];
            if (block.instance == null)
            {
                continue;
            }

            if (IsScrollBar(block.definition) && IsPointInsideBlockVisual(block, point))
            {
                return block;
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

    private static bool IsPointInsideBlockVisual(PlacedBlock block, Vector2 point)
    {
        if (block.renderers == null)
        {
            return false;
        }

        foreach (SpriteRenderer renderer in block.renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            if (point.x >= bounds.min.x && point.x <= bounds.max.x &&
                point.y >= bounds.min.y && point.y <= bounds.max.y)
            {
                return true;
            }
        }

        return false;
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
        PlayCursorSoundWhenCellChanges(activeCell);

        // ドラッグ中はグリッドへ吸着させず、カーソルへ滑らかに追従させます。
        Vector3 previewPosition = worldPoint + dragPreviewOffset;
        previewPosition.z = placedBlockZ;
        activePreview.transform.position = previewPosition;

        activeCellIsValid = CanPlace(activeDefinition, activeCell);
        activeGridPreview.transform.position = GetSnappedPosition(activeDefinition, activeCell);
        ApplyGridPreviewColor(activeCellIsValid ? validPreviewColor : invalidPreviewColor);
    }

    private void PlayCursorSoundWhenCellChanges(Vector3Int cell)
    {
        if (!IsInsidePlacementBounds(cell))
        {
            hasLastCursorSoundCell = false;
            return;
        }

        if (hasLastCursorSoundCell && cell == lastCursorSoundCell)
        {
            return;
        }

        lastCursorSoundCell = cell;
        hasLastCursorSoundCell = true;
        AudioManager.Instance?.PlayCursorSound();
    }

    private void EndDrag()
    {
        if (activeCellIsValid)
        {
            PlaceActiveBlock(activeCell);
        }
        else if (activePlacedBlock != null)
        {
            if (IsScrollBar(activeDefinition))
            {
                activeDefinition.isBgmVertical = activePlacedBlock.isBgmVertical;
                UpdateBgmFootprint(activeDefinition);
                ApplyBgmOrientation(activePreview, activeDefinition.isBgmVertical, activeDefinition.isBrightnessScrollBar);
            }

            PlaceActiveBlock(activePlacedBlock.cell);
        }
        else
        {
            Destroy(activePreview);
            Destroy(activeGridPreview);
            SetSourceActive(activeDefinition, true);
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
        block.isBgmVertical = IsScrollBar(activeDefinition) && activeDefinition.isBgmVertical;
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
            SetSourceActive(activeDefinition, false);
        }
    }

    public void SetBuildMode(bool isBuildMode)
    {
        bool isReturningToBuildMode = isBuildMode && !IsBuildMode;
        EndPlacedBlockOperation();
        StopBgmHandlePlayerMotion();
        activeBgmScrollBar = null;
        pressedPlayModeBlock = null;
        isPlayerAttachedToBgmHandle = false;
        HideAllPlayModeHoverOutlines();
        IsBuildMode = isBuildMode;

        if (isReturningToBuildMode)
        {
            ResetBgmScrollBarVolume();
        }

        foreach (PlacedBlock block in placedBlocks)
        {
            ApplyOperationColor(block, false);
            ApplyBgmTrackVisual(block, isBuildMode);
            ApplyBgmTrackDepth(block, isBuildMode);

            foreach (IPlayModeBlockState state in block.playModeStates)
            {
                if (isBuildMode)
                {
                    state.OnBuildModeEntered();
                }
                else
                {
                    state.OnPlayModeEntered();
                }
            }

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

    /// <summary>
    /// BGMScrollBarで変更した音量とハンドル位置を、配置時の状態へ戻します。
    /// </summary>
    public void ResetBgmScrollBarVolume()
    {
        StopBgmHandlePlayerMotion();
        activeBgmScrollBar = null;
        isPlayerAttachedToBgmHandle = false;

        foreach (PlacedBlock block in placedBlocks)
        {
            if (!IsScrollBar(block.definition) || block.bgmHandle == null)
            {
                continue;
            }

            Vector2Int footprint = GetValidFootprint(block.definition);
            bool isVertical = block.isBgmVertical;
            float cellLength = isVertical
                ? Mathf.Abs(placementTilemap.layoutGrid.cellSize.y)
                : Mathf.Abs(placementTilemap.layoutGrid.cellSize.x);
            int footprintLength = isVertical ? footprint.y : footprint.x;
            float maximumLocalPosition = Mathf.Max(0f, (footprintLength - 1) * cellLength) * 0.5f;

            Vector3 handlePosition = block.bgmHandle.localPosition;
            if (isVertical)
            {
                handlePosition.y = maximumLocalPosition;
            }
            else
            {
                handlePosition.x = maximumLocalPosition;
            }

            block.bgmHandle.localPosition = handlePosition;
            block.bgmNormalizedValue = 1f;
            ApplyBgmTrackVisual(block, IsBuildMode);
        }

        if (hasBgmScrollBarResetVolume)
        {
            AudioManager.Instance?.SetBgmVolume(bgmScrollBarResetVolume);
        }

        brightnessController?.ResetBrightness();
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
        List<IPlayModeBlockState> playModeStates = new List<IPlayModeBlockState>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IBlockOperationState state)
            {
                states.Add(state);
            }

            if (behaviour is IPlayModeBlockState playModeState)
            {
                playModeStates.Add(playModeState);
            }
        }

        Transform bgmHandle = IsScrollBar(activeDefinition)
            ? activePreview.transform.Find("Handle")
            : null;
        SpriteRenderer bgmTrackRenderer = IsScrollBar(activeDefinition)
            ? activePreview.transform.Find("Track")?.GetComponent<SpriteRenderer>()
            : null;
        SpriteRenderer bgmTrackRightRenderer = IsScrollBar(activeDefinition)
            ? activePreview.transform.Find("TrackRight")?.GetComponent<SpriteRenderer>()
            : null;
        SpriteRenderer bgmTrackOutlineRenderer = IsScrollBar(activeDefinition)
            ? activePreview.transform.Find("TrackOutline")?.GetComponent<SpriteRenderer>()
            : null;

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
            playModeStates = playModeStates.ToArray(),
            hoverOutlineRenderers = Array.Empty<SpriteRenderer>(),
            bgmHandle = bgmHandle,
            bgmHandleCollider = bgmHandle != null ? bgmHandle.GetComponent<Collider2D>() : null,
            bgmTrackRenderer = bgmTrackRenderer,
            bgmTrackRightRenderer = bgmTrackRightRenderer,
            bgmTrackOutlineRenderer = bgmTrackOutlineRenderer,
            bgmTrackBaseColor = bgmTrackRenderer != null
                ? bgmTrackRenderer.color
                : Color.white,
            bgmTrackBaseSortingLayerId = bgmTrackRenderer != null
                ? bgmTrackRenderer.sortingLayerID
                : 0,
            bgmTrackBaseSortingOrder = bgmTrackRenderer != null
                ? bgmTrackRenderer.sortingOrder
                : 0,
            bgmNormalizedValue = 1f,
            bgmMaximumVolume = activeDefinition.bgmMaximumVolume,
            isBgmVertical = IsScrollBar(activeDefinition) && activeDefinition.isBgmVertical
        };
    }

    private void UpdateBgmScrollBarInteraction()
    {
        if (!TryGetPointerState(out Vector2 screenPosition, out PointerPhase phase))
        {
            return;
        }

        if (phase == PointerPhase.Began)
        {
            activeBgmScrollBar = FindBgmScrollBarHandle(screenPosition);
            isPlayerAttachedToBgmHandle = TryAttachPlayerToBgmHandle(activeBgmScrollBar);
        }

        if (activeBgmScrollBar != null && phase != PointerPhase.Ended)
        {
            SetBgmScrollBarFromScreenPosition(activeBgmScrollBar, screenPosition);
        }

        if (phase == PointerPhase.Ended)
        {
            StopBgmHandlePlayerMotion();
            activeBgmScrollBar = null;
            isPlayerAttachedToBgmHandle = false;
        }
    }

    private PlacedBlock FindBgmScrollBarHandle(Vector2 screenPosition)
    {
        Vector3 point = ScreenToWorld(screenPosition);
        for (int i = placedBlocks.Count - 1; i >= 0; i--)
        {
            PlacedBlock block = placedBlocks[i];
            if (!IsScrollBar(block.definition) ||
                block.bgmHandleCollider == null ||
                !block.bgmHandleCollider.enabled)
            {
                continue;
            }

            if (block.bgmHandleCollider.OverlapPoint(point))
            {
                return block;
            }
        }

        return null;
    }

    private void SetBgmScrollBarFromScreenPosition(PlacedBlock block, Vector2 screenPosition)
    {
        if (block.bgmHandle == null || placementTilemap == null)
        {
            return;
        }

        Vector3 pointerWorld = ScreenToWorld(screenPosition);
        Vector2Int footprint = GetValidFootprint(block.definition);
        bool isVertical = block.isBgmVertical;
        float cellLength = isVertical
            ? Mathf.Abs(placementTilemap.layoutGrid.cellSize.y)
            : Mathf.Abs(placementTilemap.layoutGrid.cellSize.x);
        int footprintLength = isVertical ? footprint.y : footprint.x;
        float travel = Mathf.Max(0f, (footprintLength - 1) * cellLength);
        float center = isVertical
            ? block.instance.transform.position.y
            : block.instance.transform.position.x;
        float minimum = center - travel * 0.5f;
        float maximum = center + travel * 0.5f;
        float handleAxisPosition = Mathf.Clamp(
            isVertical ? pointerWorld.y : pointerWorld.x,
            minimum,
            maximum);

        if (!isPlayerAttachedToBgmHandle)
        {
            isPlayerAttachedToBgmHandle = TryAttachPlayerToBgmHandle(block);
        }

        Vector3 handlePosition = block.bgmHandle.position;
        if (isVertical)
        {
            handlePosition.y = handleAxisPosition;
        }
        else
        {
            handlePosition.x = handleAxisPosition;
        }

        block.bgmHandle.position = handlePosition;
        ApplyBgmTrackVisual(block, false);

        if (isPlayerAttachedToBgmHandle && playerBody != null)
        {
            Vector2 playerPosition = playerBody.position;
            playerPosition = (Vector2)block.bgmHandle.position + playerBgmHandleOffset;
            playerBody.position = playerPosition;
            playerBody.WakeUp();
            playerBody.linearVelocity = Vector2.zero;
        }

        block.bgmNormalizedValue = travel <= Mathf.Epsilon
            ? 1f
            : Mathf.InverseLerp(minimum, maximum, handleAxisPosition);

        if (block.definition.isBrightnessScrollBar)
        {
            brightnessController?.SetBrightness(block.bgmNormalizedValue);
        }
        else
        {
            AudioManager.Instance?.SetBgmVolume(
                block.bgmMaximumVolume * block.bgmNormalizedValue);
        }
    }

    private bool TryAttachPlayerToBgmHandle(PlacedBlock block)
    {
        if (block == null || block.bgmHandle == null || block.bgmHandleCollider == null ||
            playerBody == null || playerCollider == null ||
            !block.bgmHandleCollider.enabled || !playerCollider.enabled)
        {
            return false;
        }

        Physics2D.SyncTransforms();
        Bounds handleBounds = block.bgmHandleCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;
        const float contactTolerance = 0.2f;
        bool overlapsHorizontally =
            playerBounds.max.x > handleBounds.min.x &&
            playerBounds.min.x < handleBounds.max.x;
        bool isAboveHandle = playerBounds.center.y > handleBounds.center.y;
        float verticalGap = playerBounds.min.y - handleBounds.max.y;
        ColliderDistance2D distance = playerCollider.Distance(block.bgmHandleCollider);
        bool isInContact = playerCollider.IsTouching(block.bgmHandleCollider) ||
                           distance.isOverlapped ||
                           distance.distance <= contactTolerance ||
                           Mathf.Abs(verticalGap) <= contactTolerance;
        if (!overlapsHorizontally || !isAboveHandle || !isInContact)
        {
            return false;
        }

        playerBgmHandleOffset = playerBody.position - (Vector2)block.bgmHandle.position;
        return true;
    }

    private void StopBgmHandlePlayerMotion()
    {
        if (!isPlayerAttachedToBgmHandle || playerBody == null)
        {
            return;
        }

        playerBody.linearVelocity = Vector2.zero;
    }

    private void FindPlayerPhysicsReferences()
    {
        if (playerBody != null && playerCollider != null && playerRenderer != null)
        {
            return;
        }

        GameObject playerObject = playerBody != null
            ? playerBody.gameObject
            : playerCollider != null
                ? playerCollider.gameObject
                : GameObject.Find("Player");
        if (playerObject == null)
        {
            Player player = FindFirstObjectByType<Player>();
            playerObject = player != null ? player.gameObject : null;
        }

        if (playerObject != null)
        {
            playerBody ??= playerObject.GetComponent<Rigidbody2D>();
            playerCollider ??= playerObject.GetComponent<Collider2D>();
            playerRenderer ??= playerObject.GetComponent<SpriteRenderer>();
        }
    }

    private void ApplyBgmTrackDepth(PlacedBlock block, bool isBuildMode)
    {
        if (!IsScrollBar(block.definition) || block.bgmTrackRenderer == null)
        {
            return;
        }

        if (isBuildMode)
        {
            block.bgmTrackRenderer.sortingLayerID = block.bgmTrackBaseSortingLayerId;
            block.bgmTrackRenderer.sortingOrder = block.bgmTrackBaseSortingOrder;
            if (block.bgmTrackRightRenderer != null)
            {
                block.bgmTrackRightRenderer.sortingLayerID = block.bgmTrackBaseSortingLayerId;
                block.bgmTrackRightRenderer.sortingOrder = block.bgmTrackBaseSortingOrder;
            }
            ApplyBgmTrackOutlineDepth(block, block.bgmTrackBaseSortingLayerId, block.bgmTrackBaseSortingOrder - 1);
            return;
        }

        if (playerRenderer != null)
        {
            block.bgmTrackRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            block.bgmTrackRenderer.sortingOrder = playerRenderer.sortingOrder - 1;
            if (block.bgmTrackRightRenderer != null)
            {
                block.bgmTrackRightRenderer.sortingLayerID = playerRenderer.sortingLayerID;
                block.bgmTrackRightRenderer.sortingOrder = playerRenderer.sortingOrder - 1;
            }
            ApplyBgmTrackOutlineDepth(block, playerRenderer.sortingLayerID, playerRenderer.sortingOrder - 2);
        }
        else
        {
            block.bgmTrackRenderer.sortingOrder = placedSortingOrder - 1;
            if (block.bgmTrackRightRenderer != null)
            {
                block.bgmTrackRightRenderer.sortingOrder = placedSortingOrder - 1;
            }
            ApplyBgmTrackOutlineDepth(block, block.bgmTrackRenderer.sortingLayerID, placedSortingOrder - 2);
        }
    }

    private static void ApplyBgmTrackOutlineDepth(PlacedBlock block, int sortingLayerId, int sortingOrder)
    {
        if (block.bgmTrackOutlineRenderer == null)
        {
            return;
        }

        block.bgmTrackOutlineRenderer.sortingLayerID = sortingLayerId;
        block.bgmTrackOutlineRenderer.sortingOrder = sortingOrder;
    }

    private void UpdateBgmTrackVisuals(bool isBuildMode)
    {
        foreach (PlacedBlock block in placedBlocks)
        {
            ApplyBgmTrackVisual(block, isBuildMode);
        }
    }

    private void ApplyBgmTrackVisual(PlacedBlock block, bool isBuildMode)
    {
        if (!IsScrollBar(block.definition) || block.bgmTrackRenderer == null ||
            block.bgmTrackRightRenderer == null || block.bgmHandle == null)
        {
            return;
        }

        const float trackMinX = -1.9f;
        const float trackMaxX = 1.9f;
        const float trackHeight = 0.6f;
        bool isVertical = block.isBgmVertical;
        Color leftColor = block.definition.isBrightnessScrollBar ? Color.white : bgmTrackColor;
        Color rightColor = block.definition.isBrightnessScrollBar
            ? new Color(0.22f, 0.22f, 0.22f, 1f)
            : bgmTrackColor;

        if (isBuildMode)
        {
            SetBgmTrackSegment(
                block.bgmTrackRenderer,
                trackMinX,
                trackMaxX,
                trackHeight,
                leftColor,
                isVertical);
            block.bgmTrackRightRenderer.gameObject.SetActive(false);
            return;
        }

        float splitX = Mathf.Clamp(
            isVertical ? block.bgmHandle.localPosition.y : block.bgmHandle.localPosition.x,
            trackMinX,
            trackMaxX);
        SetBgmTrackSegment(
            block.bgmTrackRenderer,
            trackMinX,
            splitX,
            trackHeight,
            leftColor,
            isVertical);

        Color transparentColor = rightColor;
        if (!block.definition.isBrightnessScrollBar)
        {
            transparentColor.a *= bgmTrackRightOpacity;
        }
        block.bgmTrackRightRenderer.gameObject.SetActive(true);
        SetBgmTrackSegment(
            block.bgmTrackRightRenderer,
            splitX,
            trackMaxX,
            trackHeight,
            transparentColor,
            isVertical);
    }

    private void UpdateBrightnessVisibilityVisuals()
    {
        RectTransform visibilityLayer = brightnessController != null
            ? brightnessController.VisibilityLayer
            : null;
        bool shouldShow = !IsBuildMode &&
                          visibilityLayer != null &&
                          brightnessController.Brightness < 0.999f;

        foreach (PlacedBlock block in placedBlocks)
        {
            if (block.definition == null || !block.definition.isBrightnessScrollBar)
            {
                continue;
            }

            if (!shouldShow || block.instance == null || block.bgmHandle == null)
            {
                SetBrightnessVisibilityActive(block, false);
                continue;
            }

            BrightnessVisibilityVisual visual = EnsureBrightnessVisibilityVisual(block, visibilityLayer);
            if (visual == null || !SyncBrightnessVisibilityVisual(block, visual, visibilityLayer))
            {
                SetBrightnessVisibilityActive(block, false);
                continue;
            }

            visual.root.gameObject.SetActive(true);
        }
    }

    private BrightnessVisibilityVisual EnsureBrightnessVisibilityVisual(
        PlacedBlock block,
        RectTransform visibilityLayer)
    {
        if (block.brightnessVisibilityVisual != null &&
            block.brightnessVisibilityVisual.root != null)
        {
            return block.brightnessVisibilityVisual;
        }

        GameObject rootObject = new GameObject(
            $"{block.definition.displayName} Visibility",
            typeof(RectTransform));
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(visibilityLayer, false);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = Vector2.zero;
        root.SetAsLastSibling();

        RectTransform outline = CreateBrightnessSourceImage(
            root, "TrackOutline", Vector2.zero, Vector2.one, Color.black);
        RectTransform trackLeft = CreateBrightnessSourceImage(
            root, "TrackLeft", Vector2.zero, Vector2.one, Color.white);
        RectTransform trackRight = CreateBrightnessSourceImage(
            root,
            "TrackRight",
            Vector2.zero,
            Vector2.one,
            new Color(0.22f, 0.22f, 0.22f, 1f));
        RectTransform handle = CreateBrightnessSourceImage(
            root, "Handle", Vector2.zero, Vector2.one, Color.black);
        RectTransform fill = CreateBrightnessSourceImage(
            handle,
            "Fill",
            Vector2.zero,
            Vector2.one,
            new Color(0.92f, 0.92f, 0.92f, 1f));
        RectTransform icon = CreateBrightnessSourceImage(
            handle,
            "Icon",
            Vector2.zero,
            Vector2.one,
            Color.black,
            block.definition.brightnessIconSprite);

        block.brightnessVisibilityVisual = new BrightnessVisibilityVisual
        {
            root = root,
            trackOutline = outline,
            trackLeft = trackLeft,
            trackRight = trackRight,
            handle = handle,
            handleFill = fill,
            icon = icon,
            trackLeftImage = trackLeft.GetComponent<Image>(),
            trackRightImage = trackRight.GetComponent<Image>()
        };
        rootObject.SetActive(false);
        return block.brightnessVisibilityVisual;
    }

    private bool SyncBrightnessVisibilityVisual(
        PlacedBlock block,
        BrightnessVisibilityVisual visual,
        RectTransform visibilityLayer)
    {
        if (placementCamera == null || block.instance == null || block.bgmHandle == null)
        {
            return false;
        }

        Vector3 centerWorld = block.instance.transform.TransformPoint(Vector3.zero);
        Vector3 axisWorld = block.instance.transform.TransformPoint(
            block.isBgmVertical ? Vector3.up : Vector3.right);
        if (!TryWorldToVisibilityLocal(visibilityLayer, centerWorld, out Vector2 center) ||
            !TryWorldToVisibilityLocal(visibilityLayer, axisWorld, out Vector2 axisPoint) ||
            !TryWorldToVisibilityLocal(visibilityLayer, block.bgmHandle.position, out Vector2 handlePoint))
        {
            return false;
        }

        Vector2 axis = axisPoint - center;
        float pixelsPerWorldUnit = axis.magnitude;
        if (pixelsPerWorldUnit <= Mathf.Epsilon)
        {
            return false;
        }

        axis /= pixelsPerWorldUnit;
        float angle = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
        visual.root.anchoredPosition = center;

        SetBrightnessVisibilityRect(
            visual.trackOutline,
            Vector2.zero,
            new Vector2(4f * pixelsPerWorldUnit, 0.76f * pixelsPerWorldUnit),
            angle);

        const float trackMin = -1.9f;
        const float trackMax = 1.9f;
        float split = Mathf.Clamp(
            block.isBgmVertical ? block.bgmHandle.localPosition.y : block.bgmHandle.localPosition.x,
            trackMin,
            trackMax);
        SetBrightnessVisibilityTrackSegment(
            visual.trackLeft,
            visual.trackLeftImage,
            trackMin,
            split,
            axis,
            angle,
            pixelsPerWorldUnit,
            Color.white);
        SetBrightnessVisibilityTrackSegment(
            visual.trackRight,
            visual.trackRightImage,
            split,
            trackMax,
            axis,
            angle,
            pixelsPerWorldUnit,
            new Color(0.22f, 0.22f, 0.22f, 1f));

        visual.handle.anchoredPosition = handlePoint - center;
        visual.handle.sizeDelta = Vector2.one * pixelsPerWorldUnit;
        visual.handle.localRotation = Quaternion.identity;
        visual.handleFill.sizeDelta = Vector2.one * (0.84f * pixelsPerWorldUnit);
        visual.icon.sizeDelta = Vector2.one * (0.62f * pixelsPerWorldUnit);
        return true;
    }

    private bool TryWorldToVisibilityLocal(
        RectTransform visibilityLayer,
        Vector3 worldPosition,
        out Vector2 localPosition)
    {
        Vector3 screenPosition = placementCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f)
        {
            localPosition = default;
            return false;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            visibilityLayer,
            screenPosition,
            GetUiCamera(visibilityLayer),
            out localPosition);
    }

    private static void SetBrightnessVisibilityTrackSegment(
        RectTransform rect,
        Image image,
        float minimum,
        float maximum,
        Vector2 axis,
        float angle,
        float pixelsPerWorldUnit,
        Color color)
    {
        float width = Mathf.Max(0f, maximum - minimum);
        float center = (minimum + maximum) * 0.5f;
        rect.gameObject.SetActive(width > 0.0001f);
        rect.anchoredPosition = axis * (center * pixelsPerWorldUnit);
        rect.sizeDelta = new Vector2(width * pixelsPerWorldUnit, 0.6f * pixelsPerWorldUnit);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        image.color = color;
    }

    private static void SetBrightnessVisibilityRect(
        RectTransform rect,
        Vector2 anchoredPosition,
        Vector2 size,
        float angle)
    {
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private static void SetBrightnessVisibilityActive(PlacedBlock block, bool active)
    {
        if (block?.brightnessVisibilityVisual?.root != null)
        {
            block.brightnessVisibilityVisual.root.gameObject.SetActive(active);
        }
    }

    private static void SetBgmTrackSegment(
        SpriteRenderer renderer,
        float minX,
        float maxX,
        float height,
        Color color,
        bool isVertical)
    {
        float width = Mathf.Max(0f, maxX - minX);
        float center = (minX + maxX) * 0.5f;
        Transform segment = renderer.transform;
        segment.localPosition = isVertical
            ? new Vector3(0f, center, 0f)
            : new Vector3(center, 0f, 0f);
        segment.localRotation = isVertical
            ? Quaternion.Euler(0f, 0f, 90f)
            : Quaternion.identity;
        segment.localScale = new Vector3(width, height, 1f);
        renderer.color = color;
    }

    private void UpdateOperationColors()
    {
        foreach (PlacedBlock block in placedBlocks)
        {
            bool isOperating = block == activeBgmScrollBar;
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
        bool hasPointer = TryGetPointerState(out Vector2 screenPosition, out PointerPhase phase);
        bool isPointerHeld = hasPointer &&
                             (phase == PointerPhase.Began || phase == PointerPhase.Held);

        if (isPointerHeld)
        {
            if (pressedPlayModeBlock == null && !IsPointerOverUi())
            {
                pressedPlayModeBlock = activeBgmScrollBar ?? FindPlacedBlock(screenPosition);
            }
        }
        else
        {
            pressedPlayModeBlock = null;
        }

        bool allowHover = showPlayModeHoverOutline &&
                          !isPointerHeld &&
                          Input.touchCount == 0 &&
                          !IsPointerOverUi();
        PlacedBlock outlinedBlock = isPointerHeld
            ? pressedPlayModeBlock
            : allowHover
                ? FindPlacedBlock(Input.mousePosition)
                : null;

        foreach (PlacedBlock block in placedBlocks)
        {
            bool isJumpOperating = false;
            foreach (IBlockOperationState state in block.operationStates)
            {
                if (state is Jump && state.IsOperating)
                {
                    isJumpOperating = true;
                    break;
                }
            }

            SetPlayModeHoverOutline(
                block,
                block == outlinedBlock || isJumpOperating);
        }
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
            if (!IsHoverOutlineSource(block, source))
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
            if (IsHoverOutlineSource(block, source))
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

    private static bool IsHoverOutlineSource(PlacedBlock block, SpriteRenderer source)
    {
        if (source == null)
        {
            return false;
        }

        if (block?.definition != null && IsDynamicPlatform(block.definition))
        {
            return string.Equals(source.name, "Background", StringComparison.Ordinal);
        }

        return !IsScrollBar(block.definition) ||
               (block.bgmHandle != null && source.transform.IsChildOf(block.bgmHandle));
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
            SpriteRenderer renderer = block.renderers[i];
            if (block.definition.isBgmScrollBar &&
                (renderer == block.bgmTrackRenderer ||
                 renderer == block.bgmTrackRightRenderer))
            {
                continue;
            }

            Color color = block.baseColors[i];
            renderer.sharedMaterial = inverted && operationInversionMaterial != null
                ? operationInversionMaterial
                : block.baseMaterials[i];
            renderer.color = inverted && operationInversionMaterial == null
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
        if (blockAvailabilityVersion < 1)
        {
            foreach (BlockDefinition block in blocks)
            {
                if (block != null)
                {
                    block.isEnabled = block.dragSource != null;
                }
            }

            blockAvailabilityVersion = 1;
        }
        placementGridSize.x = Mathf.Max(1, placementGridSize.x);
        placementGridSize.y = Mathf.Max(1, placementGridSize.y);
        sourceHoverScaleMultiplier = Mathf.Max(1f, sourceHoverScaleMultiplier);
        sourceHoverScaleSpeed = Mathf.Max(0f, sourceHoverScaleSpeed);
        placedHoverScaleMultiplier = Mathf.Max(1f, placedHoverScaleMultiplier);
        placedHoverScaleSpeed = Mathf.Max(0f, placedHoverScaleSpeed);
        playModeHoverOutlineWidth = Mathf.Max(0f, playModeHoverOutlineWidth);
        bgmTrackRightOpacity = Mathf.Clamp01(bgmTrackRightOpacity);
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
                if (!IsScrollBar(definition) && !definition.usesDynamicCollider)
                {
                    SetCompositeTile(cell, runtimeColliderTile);
                }
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

                if (!IsScrollBar(definition) && !definition.usesDynamicCollider &&
                    placementTilemap != null &&
                    placementTilemap.GetTile(cell) == runtimeColliderTile)
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
        return definition != null && definition.isEnabled && definition.dragSource != null && definition.worldTemplate != null &&
               (definition.availableCount < 0 || definition.usedCount < definition.availableCount);
    }

    private static bool IsScrollBar(BlockDefinition definition) =>
        definition != null && (definition.isBgmScrollBar || definition.isBrightnessScrollBar);

    private static bool IsDynamicPlatform(BlockDefinition definition) =>
        definition != null &&
        (definition.isRandomStepBlock || definition.isUpwardDropdownBlock || definition.isPopupBlock);

    private static bool CanRotate(BlockDefinition definition) => IsScrollBar(definition);

    private void SetRotationDragPopVisible(bool visible)
    {
        // Do not use ?. here: it only checks the managed reference and does not
        // recognize UnityEngine.Object instances that have already been destroyed.
        if (rotationDragPop != null)
        {
            rotationDragPop.SetActive(visible);
        }
    }

    private void PrepareBgmScrollBarDefinition(BlockDefinition definition)
    {
        definition.isBgmVertical = false;
        UpdateBgmFootprint(definition);
        definition.bgmMaximumVolume = definition.isBgmScrollBar
            ? AudioManager.CurrentBgmVolume
            : 1f;

        EnsureRuntimeSolidSprite();
        if (definition.isBrightnessScrollBar)
        {
            PrepareBrightnessSourceVisual(definition);
        }

        if (definition.worldTemplate != null)
        {
            return;
        }

        GameObject template = new GameObject($"{definition.displayName} Template");
        template.transform.SetParent(placedBlockParent, false);

        GameObject trackOutline = new GameObject("TrackOutline");
        trackOutline.transform.SetParent(template.transform, false);
        SpriteRenderer trackOutlineRenderer = trackOutline.AddComponent<SpriteRenderer>();
        trackOutlineRenderer.sprite = runtimeSolidSprite;
        trackOutlineRenderer.color = Color.black;
        trackOutline.transform.localScale = new Vector3(4f, 0.76f, 1f);

        GameObject track = new GameObject("Track");
        track.transform.SetParent(template.transform, false);
        SpriteRenderer trackRenderer = track.AddComponent<SpriteRenderer>();
        trackRenderer.sprite = runtimeSolidSprite;
        trackRenderer.color = bgmTrackColor;
        track.transform.localScale = new Vector3(3.8f, 0.6f, 1f);

        GameObject trackRight = new GameObject("TrackRight");
        trackRight.transform.SetParent(template.transform, false);
        SpriteRenderer trackRightRenderer = trackRight.AddComponent<SpriteRenderer>();
        trackRightRenderer.sprite = runtimeSolidSprite;
        trackRightRenderer.color = trackRenderer.color;
        trackRight.transform.localScale = new Vector3(3.8f, 0.6f, 1f);
        trackRight.SetActive(false);

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(template.transform, false);
        handle.transform.localPosition = new Vector3(1.5f, 0f, -0.01f);

        if (definition.isBrightnessScrollBar)
        {
            CreateBrightnessWorldHandle(handle.transform, definition.brightnessIconSprite);
        }
        else
        {
            GameObject handleVisual = new GameObject("Visual");
            handleVisual.transform.SetParent(handle.transform, false);
            SpriteRenderer handleRenderer = handleVisual.AddComponent<SpriteRenderer>();
            handleRenderer.sprite = definition.bgmHandleSource != null
                ? definition.bgmHandleSource.sprite
                : runtimeSolidSprite;
            handleRenderer.color = definition.bgmHandleSource != null
                ? definition.bgmHandleSource.color
                : Color.white;
            FitSpriteRenderer(handleVisual.transform, handleRenderer, Vector2.one);
        }

        BoxCollider2D handleCollider = handle.AddComponent<BoxCollider2D>();
        handleCollider.size = Vector2.one;
        handleCollider.isTrigger = false;

        definition.worldTemplate = template;
        template.SetActive(false);
    }

    private void PrepareBrightnessSourceVisual(BlockDefinition definition)
    {
        RectTransform source = definition.dragSource;
        if (source == null)
        {
            return;
        }

        source.sizeDelta = new Vector2(450f, 100f);
        Image sourceImage = source.GetComponent<Image>();
        if (sourceImage != null)
        {
            sourceImage.sprite = null;
            sourceImage.color = Color.clear;
            sourceImage.preserveAspect = false;
            sourceImage.raycastTarget = true;
        }

        if (source.Find("BrightnessBarVisual") != null)
        {
            return;
        }

        RectTransform visual = CreateBrightnessSourceImage(
            source, "BrightnessBarVisual", Vector2.zero, new Vector2(400f, 76f), Color.black);
        CreateBrightnessSourceImage(
            visual, "Track", Vector2.zero, new Vector2(380f, 60f), Color.white);

        RectTransform handle = CreateBrightnessSourceImage(
            visual, "Handle", new Vector2(150f, 0f), new Vector2(100f, 100f), Color.black);
        CreateBrightnessSourceImage(
            handle, "Fill", Vector2.zero, new Vector2(84f, 84f), new Color(0.92f, 0.92f, 0.92f, 1f));
        CreateBrightnessSourceImage(
            handle, "Icon", Vector2.zero, new Vector2(62f, 62f), Color.black, definition.brightnessIconSprite);
    }

    private static RectTransform CreateBrightnessSourceImage(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color,
        Sprite sprite = null)
    {
        GameObject child = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = child.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = sprite != null;
        image.raycastTarget = false;
        return rect;
    }

    private void CreateBrightnessWorldHandle(Transform handle, Sprite iconSprite)
    {
        GameObject frame = CreateSolidWorldVisual(
            handle, "Frame", Color.black, Vector2.one, 0f);
        CreateSolidWorldVisual(
            frame.transform,
            "Fill",
            new Color(0.92f, 0.92f, 0.92f, 1f),
            new Vector2(0.84f, 0.84f),
            -0.01f);

        if (iconSprite == null)
        {
            return;
        }

        GameObject icon = new GameObject("Icon");
        icon.transform.SetParent(frame.transform, false);
        icon.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        SpriteRenderer iconRenderer = icon.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = iconSprite;
        iconRenderer.color = Color.black;
        FitSpriteRenderer(icon.transform, iconRenderer, new Vector2(0.62f, 0.62f), true);
    }

    private GameObject CreateSolidWorldVisual(
        Transform parent,
        string objectName,
        Color color,
        Vector2 size,
        float localZ)
    {
        GameObject visual = new GameObject(objectName);
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = new Vector3(0f, 0f, localZ);
        visual.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = runtimeSolidSprite;
        renderer.color = color;
        return visual;
    }

    private static void FitSpriteRenderer(
        Transform visual,
        SpriteRenderer renderer,
        Vector2 targetSize,
        bool preserveAspect = false)
    {
        if (renderer.sprite == null)
        {
            return;
        }

        Vector2 spriteSize = renderer.sprite.bounds.size;
        float scaleX = spriteSize.x > Mathf.Epsilon ? targetSize.x / spriteSize.x : 1f;
        float scaleY = spriteSize.y > Mathf.Epsilon ? targetSize.y / spriteSize.y : 1f;
        if (preserveAspect)
        {
            float uniformScale = Mathf.Min(scaleX, scaleY);
            scaleX = uniformScale;
            scaleY = uniformScale;
        }

        visual.localScale = new Vector3(scaleX, scaleY, 1f);
        Vector3 spriteCenter = renderer.sprite.bounds.center;
        visual.localPosition += new Vector3(
            -spriteCenter.x * scaleX,
            -spriteCenter.y * scaleY,
            0f);
    }

    private void PrepareBuiltInBlockDefinition(BlockDefinition definition)
    {
        if (definition.worldTemplate != null || definition.dragSource == null)
        {
            return;
        }

        string blockName = string.IsNullOrWhiteSpace(definition.displayName)
            ? definition.dragSource.name
            : definition.displayName;
        bool isMoveRight = string.Equals(blockName, "MoveR", StringComparison.OrdinalIgnoreCase);
        bool isMoveLeft = string.Equals(blockName, "MoveL", StringComparison.OrdinalIgnoreCase);
        bool isJump = string.Equals(blockName, "Jump", StringComparison.OrdinalIgnoreCase);
        if (!isMoveRight && !isMoveLeft && !isJump)
        {
            return;
        }

        EnsureRuntimeSolidSprite();

        GameObject template = new GameObject($"{blockName} Template");
        template.transform.SetParent(placedBlockParent, false);
        template.SetActive(false);

        Vector2Int footprint = GetValidFootprint(definition);
        BoxCollider2D collider = template.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(footprint.x, footprint.y);
        collider.isTrigger = false;

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(template.transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        Image sourceImage = definition.dragSource.GetComponent<Image>();
        if (sourceImage == null)
        {
            sourceImage = definition.dragSource.GetComponentInChildren<Image>(true);
        }

        renderer.sprite = sourceImage != null && sourceImage.sprite != null
            ? sourceImage.sprite
            : runtimeSolidSprite;
        renderer.color = sourceImage != null ? sourceImage.color : Color.white;

        if (renderer.sprite != null)
        {
            Vector2 spriteSize = renderer.sprite.bounds.size;
            Vector3 visualScale = new Vector3(
                spriteSize.x > Mathf.Epsilon ? footprint.x / spriteSize.x : 1f,
                spriteSize.y > Mathf.Epsilon ? footprint.y / spriteSize.y : 1f,
                1f);
            visual.transform.localScale = visualScale;
            Vector3 spriteCenter = renderer.sprite.bounds.center;
            visual.transform.localPosition = new Vector3(
                -spriteCenter.x * visualScale.x,
                -spriteCenter.y * visualScale.y,
                0f);
        }

        if (isMoveRight)
        {
            MoveR moveRight = template.AddComponent<MoveR>();
            moveRight.Configure(playerBody, definition.moveSpeed);
        }
        else if (isMoveLeft)
        {
            MoveL moveLeft = template.AddComponent<MoveL>();
            moveLeft.Configure(playerBody, definition.moveSpeed);
        }
        else
        {
            Jump jump = template.AddComponent<Jump>();
            jump.Configure(playerBody, definition.jumpPower);
        }

        definition.worldTemplate = template;
    }

    private void PrepareDynamicPlatformDefinition(BlockDefinition definition)
    {
        if (definition.worldTemplate != null || definition.dragSource == null)
        {
            return;
        }

        EnsureRuntimeSolidSprite();
        EnsureRuntimeVariableBlockFrameSprite();
        EnsureRuntimeVariableBlockCircleSprite();
        string blockName = string.IsNullOrWhiteSpace(definition.displayName)
            ? definition.dragSource.name
            : definition.displayName;
        GameObject template = new GameObject($"{blockName} Template");
        template.transform.SetParent(placedBlockParent, false);
        template.SetActive(false);

        definition.usesDynamicCollider = true;
        StyleDynamicBlockSource(definition);
        TMP_FontAsset fontAsset = definition.dragSource.GetComponentInChildren<TMP_Text>(true)?.font;
        if (definition.isRandomStepBlock)
        {
            definition.footprint = new Vector2Int(3, 1);
            RandomStepPlatform stepPlatform = template.AddComponent<RandomStepPlatform>();
            stepPlatform.Configure(
                runtimeVariableBlockFrameSprite,
                runtimeVariableBlockCircleSprite,
                runtimeSolidSprite,
                fontAsset);
        }
        else if (definition.isUpwardDropdownBlock)
        {
            definition.footprint = new Vector2Int(3, 2);
            UpwardDropdownPlatform dropdown = template.AddComponent<UpwardDropdownPlatform>();
            dropdown.Configure(
                runtimeVariableBlockFrameSprite,
                runtimeVariableBlockCircleSprite,
                runtimeSolidSprite,
                fontAsset);
        }
        else if (definition.isPopupBlock)
        {
            definition.footprint = new Vector2Int(5, 3);
            PopupPlatform popup = template.AddComponent<PopupPlatform>();
            popup.Configure(
                runtimeVariableBlockFrameSprite,
                runtimeSolidSprite,
                fontAsset);
        }

        definition.worldTemplate = template;
    }

    private void StyleDynamicBlockSource(BlockDefinition definition)
    {
        if (definition.isRandomStepBlock || definition.isPopupBlock)
        {
            float sourceHeight = Mathf.Max(1f, definition.dragSource.sizeDelta.y);
            definition.dragSource.sizeDelta = new Vector2(sourceHeight * 3f, sourceHeight);
        }

        Image sourceImage = definition.dragSource.GetComponent<Image>();
        if (sourceImage == null)
        {
            sourceImage = definition.dragSource.GetComponentInChildren<Image>(true);
        }

        if (sourceImage != null)
        {
            sourceImage.sprite = runtimeVariableBlockFrameSprite;
            sourceImage.type = Image.Type.Sliced;
            sourceImage.preserveAspect = false;
            sourceImage.color = Color.white;
        }

        TMP_Text sourceLabel = definition.dragSource.GetComponentInChildren<TMP_Text>(true);
        TMP_FontAsset fontAsset = sourceLabel != null ? sourceLabel.font : null;
        if (sourceLabel != null)
        {
            sourceLabel.gameObject.SetActive(false);
        }

        Transform oldVisual = definition.dragSource.Find("DynamicSourceVisual");
        if (oldVisual != null)
        {
            oldVisual.gameObject.SetActive(false);
            Destroy(oldVisual.gameObject);
        }

        RectTransform visualRoot = CreateSourceVisualRoot(definition.dragSource);
        if (definition.isRandomStepBlock)
        {
            BuildStepSourceVisual(visualRoot, fontAsset);
        }
        else if (definition.isUpwardDropdownBlock)
        {
            BuildDropdownSourceVisual(visualRoot, fontAsset);
        }
        else if (definition.isPopupBlock)
        {
            BuildPopupSourceVisual(visualRoot, fontAsset);
        }
    }

    private RectTransform CreateSourceVisualRoot(RectTransform parent)
    {
        GameObject visualObject = new GameObject(
            "DynamicSourceVisual",
            typeof(RectTransform));
        RectTransform root = visualObject.GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        return root;
    }

    private void BuildStepSourceVisual(RectTransform root, TMP_FontAsset fontAsset)
    {
        Color32 dark = new Color32(59, 59, 59, 255);
        Color32 light = new Color32(235, 235, 235, 255);
        Color32 muted = new Color32(145, 145, 145, 255);
        for (int i = 0; i < 2; i++)
        {
            CreateSourceImage(
                root,
                $"Connector{i + 1}",
                runtimeSolidSprite,
                muted,
                new Vector2(i == 0 ? -50f : 50f, 0f),
                new Vector2(42f, 7f));
        }

        for (int i = 0; i < 3; i++)
        {
            Vector2 position = new Vector2((i - 1) * 100f, 0f);
            CreateSourceImage(root, $"Node{i + 1}Outer", runtimeVariableBlockCircleSprite,
                dark, position, new Vector2(58f, 58f));
            CreateSourceImage(root, $"Node{i + 1}Inner", runtimeVariableBlockCircleSprite,
                light, position, new Vector2(46f, 46f));
            CreateSourceText(root, $"Node{i + 1}Label", (i + 1).ToString(), fontAsset,
                muted, position, new Vector2(42f, 42f), 30f);
        }
    }

    private void BuildDropdownSourceVisual(RectTransform root, TMP_FontAsset fontAsset)
    {
        Color32 dark = new Color32(59, 59, 59, 255);
        Vector2 nodePosition = new Vector2(-8f, 0f);
        CreateSourceImage(root, "SelectedNodeOuter", runtimeVariableBlockCircleSprite,
            dark, nodePosition, new Vector2(58f, 58f));
        CreateSourceImage(root, "SelectedNodeInner", runtimeVariableBlockCircleSprite,
            dark, nodePosition, new Vector2(46f, 46f));
        CreateSourceText(root, "SelectedNodeLabel", "A", fontAsset,
            Color.white, nodePosition, new Vector2(42f, 42f), 30f);

        Image leftChevron = CreateSourceImage(root, "ChevronLeft", runtimeSolidSprite,
            dark, new Vector2(20f, 25f), new Vector2(18f, 6f));
        leftChevron.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 42f);
        Image rightChevron = CreateSourceImage(root, "ChevronRight", runtimeSolidSprite,
            dark, new Vector2(33f, 25f), new Vector2(18f, 6f));
        rightChevron.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -42f);
    }

    private void BuildPopupSourceVisual(RectTransform root, TMP_FontAsset fontAsset)
    {
        Color32 dark = new Color32(59, 59, 59, 255);
        CreateSourceText(
            root,
            "PopupLabel",
            "ポップアップを開く",
            fontAsset,
            dark,
            Vector2.zero,
            new Vector2(270f, 72f),
            28f);
    }

    private static Image CreateSourceImage(
        RectTransform parent,
        string objectName,
        Sprite sprite,
        Color color,
        Vector2 position,
        Vector2 size)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateSourceText(
        RectTransform parent,
        string objectName,
        string value,
        TMP_FontAsset fontAsset,
        Color color,
        Vector2 position,
        Vector2 size,
        float fontSize)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null)
        {
            label.font = fontAsset;
        }

        label.text = value;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Truncate;
        label.raycastTarget = false;
        return label;
    }

    private void EnsureRuntimeVariableBlockFrameSprite()
    {
        if (runtimeVariableBlockFrameSprite != null)
        {
            return;
        }

        const int textureSize = 128;
        const int frameInset = 10;
        runtimeVariableBlockFrameTexture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false)
        {
            name = "Variable Block Frame Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32 transparent = new Color32(0, 0, 0, 0);
        Color32 border = new Color32(59, 59, 59, 255);
        Color32 fill = new Color32(235, 235, 235, 255);
        Color32[] pixels = new Color32[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                bool insideOuter = IsInsideRoundedRect(x, y, textureSize, 0, 15f);
                bool insideInner = IsInsideRoundedRect(x, y, textureSize, frameInset, 8f);
                pixels[y * textureSize + x] = !insideOuter
                    ? transparent
                    : insideInner
                        ? fill
                        : border;
            }
        }

        runtimeVariableBlockFrameTexture.SetPixels32(pixels);
        runtimeVariableBlockFrameTexture.Apply();
        runtimeVariableBlockFrameSprite = Sprite.Create(
            runtimeVariableBlockFrameTexture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize,
            0,
            SpriteMeshType.FullRect,
            new Vector4(18f, 18f, 18f, 18f));
        runtimeVariableBlockFrameSprite.name = "Variable Block Frame Sprite";
        runtimeVariableBlockFrameSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    private void EnsureRuntimeVariableBlockCircleSprite()
    {
        if (runtimeVariableBlockCircleSprite != null)
        {
            return;
        }

        const int textureSize = 128;
        float radius = textureSize * 0.5f - 1f;
        Vector2 center = new Vector2(textureSize * 0.5f, textureSize * 0.5f);
        runtimeVariableBlockCircleTexture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false)
        {
            name = "Variable Block Circle Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                pixels[y * textureSize + x] = Vector2.Distance(point, center) <= radius
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);
            }
        }

        runtimeVariableBlockCircleTexture.SetPixels32(pixels);
        runtimeVariableBlockCircleTexture.Apply();
        runtimeVariableBlockCircleSprite = Sprite.Create(
            runtimeVariableBlockCircleTexture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize,
            0,
            SpriteMeshType.FullRect);
        runtimeVariableBlockCircleSprite.name = "Variable Block Circle Sprite";
        runtimeVariableBlockCircleSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    private static bool IsInsideRoundedRect(
        int pixelX,
        int pixelY,
        int textureSize,
        int inset,
        float radius)
    {
        float x = pixelX + 0.5f;
        float y = pixelY + 0.5f;
        float min = inset;
        float max = textureSize - inset;
        if (x < min || x > max || y < min || y > max)
        {
            return false;
        }

        float centerX = Mathf.Clamp(x, min + radius, max - radius);
        float centerY = Mathf.Clamp(y, min + radius, max - radius);
        float deltaX = x - centerX;
        float deltaY = y - centerY;
        return deltaX * deltaX + deltaY * deltaY <= radius * radius;
    }

    private void EnsureRuntimeSolidSprite()
    {
        if (runtimeSolidSprite != null)
        {
            return;
        }

        runtimeSolidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "BGMScrollBar Solid Texture",
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.HideAndDontSave
        };
        runtimeSolidTexture.SetPixel(0, 0, Color.white);
        runtimeSolidTexture.Apply();
        runtimeSolidSprite = Sprite.Create(
            runtimeSolidTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        runtimeSolidSprite.name = "BGMScrollBar Solid Sprite";
        runtimeSolidSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    private static GameObject GetSourceObject(BlockDefinition definition) =>
        definition.sourceVisualRoot != null
            ? definition.sourceVisualRoot
            : definition.dragSource.gameObject;

    private static Transform GetSourceTransform(BlockDefinition definition) =>
        GetSourceObject(definition).transform;

    private static void SetSourceActive(BlockDefinition definition, bool active) =>
        GetSourceObject(definition).SetActive(active);

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
        // BGMScrollBarはTilemapへ統合せず、ハンドル1セルだけを通常Colliderとして残します。
        if (!mergePlacedBlocksIntoTilemap || IsScrollBar(activeDefinition) || activeDefinition.usesDynamicCollider)
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
            if (block.brightnessVisibilityVisual?.root != null)
            {
                Destroy(block.brightnessVisibilityVisual.root.gameObject);
            }
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

        if (runtimeSolidSprite != null)
        {
            Destroy(runtimeSolidSprite);
        }

        if (runtimeVariableBlockFrameSprite != null)
        {
            Destroy(runtimeVariableBlockFrameSprite);
        }

        if (runtimeVariableBlockCircleSprite != null)
        {
            Destroy(runtimeVariableBlockCircleSprite);
        }

        if (runtimeSolidTexture != null)
        {
            Destroy(runtimeSolidTexture);
        }

        if (runtimeVariableBlockFrameTexture != null)
        {
            Destroy(runtimeVariableBlockFrameTexture);
        }

        if (runtimeVariableBlockCircleTexture != null)
        {
            Destroy(runtimeVariableBlockCircleTexture);
        }

    }

    private void OnDisable()
    {
        EndPlacedBlockOperation();
        StopBgmHandlePlayerMotion();
        activeBgmScrollBar = null;
        pressedPlayModeBlock = null;
        isPlayerAttachedToBgmHandle = false;
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
                SetSourceActive(activeDefinition, true);
            }

            ClearDragState();
        }

        foreach (BlockDefinition block in blocks)
        {
            if (block != null && block.dragSource != null && block.sourceBaseScale != Vector3.zero)
            {
                Transform sourceTransform = GetSourceTransform(block);
                sourceTransform.localScale = block.sourceBaseScale;
                sourceTransform.localPosition = block.sourceBaseLocalPosition;
            }
        }

        foreach (PlacedBlock block in placedBlocks)
        {
            ApplyOperationColor(block, false);
            SetBrightnessVisibilityActive(block, false);

            foreach (IBlockOperationState state in block.operationStates)
            {
                state.CancelOperation();
            }

            if (block.instance != null)
            {
                block.instance.transform.localScale = block.baseScale;
            }
        }

        SetRotationDragPopVisible(false);
    }

    private void ClearDragState()
    {
        bool wasDragging = activePreview != null;
        SetRotationDragPopVisible(false);
        activeDefinition = null;
        activePreview = null;
        activeGridPreview = null;
        activeGridPreviewUsesBgmShadow = false;
        activePlacedBlock = null;
        activeCellIsValid = false;
        hasLastCursorSoundCell = false;
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

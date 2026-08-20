using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 画面上部のブロックをフィールドへドラッグし、Tilemap のセルに沿って配置します。
/// ドラッグ元ごとの見た目、生成元、占有セル数などは Inspector から設定できます。
/// </summary>
public class BlockManager : MonoBehaviour
{
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
    }

    [Header("必須参照")]
    [Tooltip("画面座標をワールド座標へ変換するカメラです。未設定ならMain Cameraを使用します。")]
    [SerializeField] private Camera placementCamera;

    [Tooltip("配置グリッド、配置可能範囲、既存Tileの占有判定に使うTilemapです。")]
    [SerializeField] private Tilemap placementTilemap;

    [Tooltip("配置後のブロックをまとめる親Transformです。未設定ならTilemapと同じ親を使用します。")]
    [SerializeField] private Transform placedBlockParent;

    [Header("配置するブロック")]
    [SerializeField] private BlockDefinition[] blocks = Array.Empty<BlockDefinition>();

    [Header("配置範囲")]
    [Tooltip("配置グリッド左下のTilemapセル座標です。")]
    [SerializeField] private Vector2Int placementGridOrigin = new Vector2Int(-9, -4);

    [Tooltip("配置できるグリッドの大きさです。Xが横、Yが縦です（例: X=8、Y=16で8×16）。")]
    [SerializeField] private Vector2Int placementGridSize = new Vector2Int(18, 16);

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

    private readonly HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
    private readonly List<SpriteRenderer> previewRenderers = new List<SpriteRenderer>();
    private readonly List<Color> previewOriginalColors = new List<Color>();
    private readonly List<Collider2D> previewColliders = new List<Collider2D>();
    private readonly List<bool> previewColliderStates = new List<bool>();
    private readonly List<MonoBehaviour> previewBehaviours = new List<MonoBehaviour>();
    private readonly List<bool> previewBehaviourStates = new List<bool>();
    private readonly List<SpriteRenderer> gridPreviewRenderers = new List<SpriteRenderer>();
    private readonly List<Color> gridPreviewOriginalColors = new List<Color>();

    private BlockDefinition activeDefinition;
    private GameObject activePreview;
    private GameObject activeGridPreview;
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
    }

    private void Update()
    {
        if (!TryGetPointerState(out Vector2 screenPosition, out PointerPhase phase))
        {
            return;
        }

        if (phase == PointerPhase.Began)
        {
            TryBeginDrag(screenPosition);
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

    private void TryBeginDrag(Vector2 screenPosition)
    {
        if (placementCamera == null || placementTilemap == null)
        {
            Debug.LogWarning("BlockManager: CameraまたはTilemapが設定されていません。", this);
            return;
        }

        foreach (BlockDefinition block in blocks)
        {
            if (!CanCreate(block) ||
                !RectTransformUtility.RectangleContainsScreenPoint(block.dragSource, screenPosition, GetUiCamera(block.dragSource)))
            {
                continue;
            }

            activeDefinition = block;
            activePreview = Instantiate(block.worldTemplate, placedBlockParent);
            activePreview.name = string.IsNullOrWhiteSpace(block.displayName)
                ? block.worldTemplate.name
                : block.displayName;
            activeGridPreview = Instantiate(block.worldTemplate, placedBlockParent);
            activeGridPreview.name = $"{activePreview.name} (Grid Preview)";

            // inactiveの生成元から複製した直後に、ドラッグ中は不要な
            // 当たり判定と動作用Behaviourを止めてから表示します。
            CacheAndDisablePreviewComponents();
            CachePreviewRenderers();
            PrepareGridPreview();
            activePreview.SetActive(true);
            activeGridPreview.SetActive(true);

            // 掴んだブロックは上部の一覧から一旦消します。
            // 配置できなかった場合だけ EndDrag で元に戻します。
            block.dragSource.gameObject.SetActive(false);

            return;
        }
    }

    private void UpdatePreview(Vector2 screenPosition)
    {
        Vector3 screenPoint = new Vector3(screenPosition.x, screenPosition.y,
            Mathf.Abs(placementCamera.transform.position.z - placementTilemap.transform.position.z));
        Vector3 worldPoint = placementCamera.ScreenToWorldPoint(screenPoint);

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
            // 配置を確定する瞬間だけ、占有セル全体の中心へスナップします。
            activePreview.transform.position = GetSnappedPosition(activeDefinition, activeCell);
            RegisterOccupiedCells(activeDefinition, activeCell);
            RestorePreviewAppearance();
            RestorePreviewComponents();
            Destroy(activeGridPreview);

            activeDefinition.usedCount++;
            if (activeDefinition.availableCount >= 0 &&
                activeDefinition.usedCount >= activeDefinition.availableCount &&
                activeDefinition.hideSourceWhenExhausted)
            {
                activeDefinition.dragSource.gameObject.SetActive(false);
            }
        }
        else
        {
            Destroy(activePreview);
            Destroy(activeGridPreview);
            activeDefinition.dragSource.gameObject.SetActive(true);
        }

        ClearDragState();
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
                occupiedCells.Add(origin + new Vector3Int(x, y, 0));
            }
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

    private void OnDisable()
    {
        if (activePreview != null)
        {
            Destroy(activePreview);

            if (activeGridPreview != null)
            {
                Destroy(activeGridPreview);
            }

            // シーン切り替え以外の理由でManagerが無効になった場合も、
            // 途中だったドラッグ元を失わないよう復帰させます。
            if (activeDefinition != null && activeDefinition.dragSource != null)
            {
                activeDefinition.dragSource.gameObject.SetActive(true);
            }

            ClearDragState();
        }
    }

    private void ClearDragState()
    {
        activeDefinition = null;
        activePreview = null;
        activeGridPreview = null;
        activeCellIsValid = false;
        previewRenderers.Clear();
        previewOriginalColors.Clear();
        previewColliders.Clear();
        previewColliderStates.Clear();
        previewBehaviours.Clear();
        previewBehaviourStates.Clear();
        gridPreviewRenderers.Clear();
        gridPreviewOriginalColors.Clear();
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

    private enum PointerPhase
    {
        None,
        Began,
        Held,
        Ended
    }
}

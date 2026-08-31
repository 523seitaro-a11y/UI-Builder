using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class PopupPlatform : MonoBehaviour,
    BlockManager.IBlockOperationState,
    BlockManager.IPlayModeBlockState
{
    private static readonly Color32 Dark = new Color32(59, 59, 59, 255);

    private static readonly Vector2 ButtonSize = new Vector2(3f, 1f);
    private static readonly Vector2 ButtonCenter = new Vector2(0f, -1f);
    private static readonly Vector2 PopupSize = new Vector2(5f, 2f);
    private static readonly Vector2 PopupCenter = new Vector2(0f, 0.5f);

    [SerializeField] private BoxCollider2D buttonCollider;
    [SerializeField] private BoxCollider2D popupCollider;
    [SerializeField] private SpriteRenderer buttonBackground;
    [SerializeField] private TMP_Text buttonLabel;
    [SerializeField] private GameObject popupVisualRoot;
    [SerializeField] private SpriteRenderer popupBackground;
    [SerializeField] private TMP_Text popupLabel;
    [SerializeField] private SpriteRenderer[] closeMarkRenderers;

    private Camera pointerCamera;
    private bool isPlayMode;
    private bool isOpen;

    public bool IsOperating => false;
    public bool IsOpen => isOpen;

    public void Configure(Sprite frameSprite, Sprite solidSprite, TMP_FontAsset fontAsset)
    {
        buttonCollider = GetComponent<BoxCollider2D>();
        if (buttonCollider == null)
        {
            buttonCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        buttonCollider.isTrigger = false;
        buttonCollider.size = ButtonSize;
        buttonCollider.offset = ButtonCenter;

        buttonBackground = CreateRenderer("Background", transform, frameSprite, Color.white, 10);
        buttonBackground.drawMode = SpriteDrawMode.Sliced;
        buttonBackground.size = ButtonSize;
        buttonBackground.transform.localPosition = new Vector3(ButtonCenter.x, ButtonCenter.y, 0f);

        buttonLabel = CreateLabel(
            "ButtonLabel",
            transform,
            "ポップアップを開く",
            fontAsset,
            new Vector2(2.82f, 0.7f),
            14,
            2.65f);
        buttonLabel.transform.localPosition = new Vector3(ButtonCenter.x, ButtonCenter.y, -0.03f);

        popupVisualRoot = new GameObject("PopupVisualRoot");
        popupVisualRoot.transform.SetParent(transform, false);

        popupCollider = gameObject.AddComponent<BoxCollider2D>();
        popupCollider.isTrigger = false;
        popupCollider.size = PopupSize;
        popupCollider.offset = PopupCenter;

        popupBackground = CreateRenderer(
            "PopupBackground",
            popupVisualRoot.transform,
            frameSprite,
            Color.white,
            10);
        popupBackground.drawMode = SpriteDrawMode.Sliced;
        popupBackground.size = PopupSize;

        popupLabel = CreateLabel(
            "PopupLabel",
            popupVisualRoot.transform,
            "これはポップアップウィンドウです",
            fontAsset,
            new Vector2(4.5f, 0.74f),
            14,
            2.65f);
        popupLabel.transform.localPosition = new Vector3(-0.16f, -0.05f, -0.03f);

        closeMarkRenderers = new SpriteRenderer[2];
        for (int i = 0; i < closeMarkRenderers.Length; i++)
        {
            SpriteRenderer mark = CreateRenderer(
                $"CloseMark{i + 1}",
                popupVisualRoot.transform,
                solidSprite,
                Dark,
                15);
            mark.transform.localPosition = new Vector3(2.08f, 0.63f, -0.04f);
            mark.transform.localScale = new Vector3(0.38f, 0.065f, 1f);
            mark.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
            closeMarkRenderers[i] = mark;
        }

        popupVisualRoot.transform.localPosition = new Vector3(PopupCenter.x, PopupCenter.y, 0f);
        ApplyClosedState();
    }

    public void OnPlayModeEntered()
    {
        isPlayMode = true;
        ApplyClosedState();
    }

    public void OnBuildModeEntered()
    {
        isPlayMode = false;
        ApplyClosedState();
    }

    private void OnMouseDown()
    {
        if (Input.touchCount == 0)
        {
            HandlePointer(Input.mousePosition);
        }
    }

    private void Update()
    {
        if (!isPlayMode || Input.touchCount == 0)
        {
            return;
        }

        Touch touch = Input.GetTouch(0);
        if (touch.phase == TouchPhase.Began && IsPointerInside(touch.position))
        {
            HandlePointer(touch.position);
        }
    }

    public void BeginOperation()
    {
        if (!isPlayMode)
        {
            return;
        }

        Vector2 screenPosition = Input.touchCount > 0
            ? Input.GetTouch(0).position
            : (Vector2)Input.mousePosition;
        HandlePointer(screenPosition);
    }

    private void HandlePointer(Vector2 screenPosition)
    {
        if (!isPlayMode)
        {
            return;
        }

        Vector3 localPoint = transform.InverseTransformPoint(ScreenToWorld(screenPosition));

        if (!isOpen)
        {
            if (Contains(localPoint, ButtonCenter, ButtonSize))
            {
                ApplyOpenState();
            }

            return;
        }

        Vector2 closeCenter = PopupCenter + new Vector2(2.08f, 0.63f);
        if (Contains(localPoint, closeCenter, new Vector2(0.8f, 0.8f)))
        {
            ApplyClosedState();
        }
    }

    public void CancelOperation()
    {
    }

    private void ApplyOpenState()
    {
        isOpen = true;
        popupVisualRoot.SetActive(true);
        popupCollider.enabled = true;
    }

    private void ApplyClosedState()
    {
        isOpen = false;
        if (popupVisualRoot != null)
        {
            popupVisualRoot.SetActive(false);
        }

        if (popupCollider != null)
        {
            popupCollider.enabled = false;
        }
    }

    private Vector3 ScreenToWorld(Vector2 screenPosition)
    {
        pointerCamera ??= Camera.main;
        if (pointerCamera == null)
        {
            return transform.position;
        }

        float depth = Mathf.Abs(pointerCamera.transform.position.z - transform.position.z);
        return pointerCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
    }

    private bool IsPointerInside(Vector2 screenPosition)
    {
        Vector2 worldPoint = ScreenToWorld(screenPosition);
        return (buttonCollider != null && buttonCollider.enabled && buttonCollider.OverlapPoint(worldPoint)) ||
               (popupCollider != null && popupCollider.enabled && popupCollider.OverlapPoint(worldPoint));
    }

    private static bool Contains(Vector2 point, Vector2 center, Vector2 size)
    {
        Vector2 halfSize = size * 0.5f;
        return point.x >= center.x - halfSize.x && point.x <= center.x + halfSize.x &&
               point.y >= center.y - halfSize.y && point.y <= center.y + halfSize.y;
    }

    private static SpriteRenderer CreateRenderer(
        string objectName,
        Transform parent,
        Sprite sprite,
        Color color,
        int sortingOrder)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static TMP_Text CreateLabel(
        string objectName,
        Transform parent,
        string value,
        TMP_FontAsset fontAsset,
        Vector2 size,
        int sortingOrder,
        float fontSize)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        TextMeshPro label = child.AddComponent<TextMeshPro>();
        if (fontAsset != null)
        {
            label.font = fontAsset;
        }

        label.text = value;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Dark;
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Truncate;
        label.rectTransform.sizeDelta = size;
        label.GetComponent<Renderer>().sortingOrder = sortingOrder;
        return label;
    }
}

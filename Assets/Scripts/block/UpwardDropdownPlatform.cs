using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class UpwardDropdownPlatform : MonoBehaviour,
    BlockManager.IBlockOperationState,
    BlockManager.IPlayModeBlockState
{
    private static readonly Color32 Dark = new Color32(59, 59, 59, 255);
    private static readonly Color32 Light = new Color32(235, 235, 235, 255);

    [SerializeField] private BoxCollider2D platformCollider;
    [SerializeField] private SpriteRenderer background;
    [SerializeField] private SpriteRenderer collapsedOuter;
    [SerializeField] private SpriteRenderer collapsedInner;
    [SerializeField] private TMP_Text collapsedLabel;
    [SerializeField] private SpriteRenderer[] chevronRenderers;
    [SerializeField] private SpriteRenderer[] candidateOuterRenderers;
    [SerializeField] private SpriteRenderer[] candidateInnerRenderers;
    [SerializeField] private TMP_Text[] candidateLabels;
    [SerializeField] private TMP_Text selectedText;
    [SerializeField] private SpriteRenderer horizontalDivider;

    private Camera pointerCamera;
    private bool isPlayMode;
    private bool isExpanded;
    private int selectedIndex;

    public bool IsOperating => false;
    public bool IsExpanded => isExpanded;
    public int SelectedIndex => selectedIndex;

    public void Configure(
        Sprite frameSprite,
        Sprite circleSprite,
        Sprite solidSprite,
        TMP_FontAsset fontAsset)
    {
        platformCollider = GetComponent<BoxCollider2D>();
        if (platformCollider == null)
        {
            platformCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        platformCollider.isTrigger = false;
        background = CreateRenderer("Background", frameSprite, Color.white, 10);
        background.drawMode = SpriteDrawMode.Sliced;

        collapsedOuter = CreateRenderer("CollapsedNodeOuter", circleSprite, Dark, 12);
        collapsedInner = CreateRenderer("CollapsedNodeInner", circleSprite, Dark, 13);
        collapsedLabel = CreateLabel("CollapsedLabel", fontAsset, 0.58f, 0.58f, 14, 2.8f);

        chevronRenderers = new SpriteRenderer[2];
        for (int i = 0; i < chevronRenderers.Length; i++)
        {
            chevronRenderers[i] = CreateRenderer($"Chevron{i + 1}", solidSprite, Dark, 14);
            chevronRenderers[i].transform.localScale = new Vector3(0.19f, 0.06f, 1f);
        }

        candidateOuterRenderers = new SpriteRenderer[3];
        candidateInnerRenderers = new SpriteRenderer[3];
        candidateLabels = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            candidateOuterRenderers[i] = CreateRenderer($"Candidate{i}Outer", circleSprite, Dark, 12);
            candidateInnerRenderers[i] = CreateRenderer($"Candidate{i}Inner", circleSprite, Light, 13);
            candidateLabels[i] = CreateLabel(
                $"Candidate{i}Label", fontAsset, 0.58f, 0.58f, 14, 2.8f);
            candidateLabels[i].text = ((char)('A' + i)).ToString();
        }

        selectedText = CreateLabel("SelectedLabel", fontAsset, 2.55f, 0.64f, 14, 2.8f);
        horizontalDivider = CreateRenderer("HorizontalDivider", solidSprite, Dark, 11);
        horizontalDivider.transform.localScale = new Vector3(2.72f, 0.055f, 1f);

        ApplyCollapsedState();
    }

    public void OnPlayModeEntered()
    {
        isPlayMode = true;
        selectedIndex = 0;
        ApplyCollapsedState();
    }

    public void OnBuildModeEntered()
    {
        isPlayMode = false;
        selectedIndex = 0;
        ApplyCollapsedState();
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

    public void CancelOperation()
    {
    }

    private void HandlePointer(Vector2 screenPosition)
    {
        if (!isPlayMode)
        {
            return;
        }

        if (!isExpanded)
        {
            ApplyExpandedState();
            return;
        }

        Vector3 localPoint = transform.InverseTransformPoint(ScreenToWorld(screenPosition));
        if (localPoint.y >= 0f)
        {
            selectedIndex = Mathf.Clamp(Mathf.FloorToInt(localPoint.x + 1.5f), 0, 2);
        }

        ApplyCollapsedState();
    }

    private bool IsPointerInside(Vector2 screenPosition)
    {
        return platformCollider != null &&
               platformCollider.enabled &&
               platformCollider.OverlapPoint(ScreenToWorld(screenPosition));
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

    private void ApplyCollapsedState()
    {
        isExpanded = false;
        SetPlatformGeometry(new Vector2(1f, 1f), new Vector2(0f, -0.5f));

        Vector3 nodePosition = new Vector3(-0.08f, -0.5f, -0.02f);
        collapsedOuter.transform.localPosition = nodePosition;
        collapsedOuter.transform.localScale = Vector3.one * 0.62f;
        collapsedInner.transform.localPosition = nodePosition + new Vector3(0f, 0f, -0.01f);
        collapsedInner.transform.localScale = Vector3.one * 0.48f;
        collapsedLabel.transform.localPosition = nodePosition + new Vector3(0f, 0f, -0.02f);
        collapsedLabel.text = GetCandidateLetter(selectedIndex).ToString();
        collapsedLabel.color = Color.white;

        SetCollapsedVisualsVisible(true);
        SetExpandedVisualsVisible(false);
        LayoutChevron();
    }

    private void ApplyExpandedState()
    {
        isExpanded = true;
        SetPlatformGeometry(new Vector2(3f, 2f), Vector2.zero);
        SetCollapsedVisualsVisible(false);
        SetExpandedVisualsVisible(true);

        for (int i = 0; i < 3; i++)
        {
            Vector3 position = new Vector3(i - 1f, 0.5f, -0.02f);
            candidateOuterRenderers[i].transform.localPosition = position;
            candidateOuterRenderers[i].transform.localScale = Vector3.one * 0.62f;
            candidateInnerRenderers[i].transform.localPosition = position + new Vector3(0f, 0f, -0.01f);
            candidateInnerRenderers[i].transform.localScale = Vector3.one * 0.48f;
            candidateLabels[i].transform.localPosition = position + new Vector3(0f, 0f, -0.02f);

            bool selected = i == selectedIndex;
            candidateInnerRenderers[i].color = selected ? Dark : Light;
            candidateLabels[i].color = selected ? Color.white : Dark;
        }

        selectedText.transform.localPosition = new Vector3(0f, -0.5f, -0.04f);
        selectedText.text = $"選択 {GetCandidateLetter(selectedIndex)}";
        selectedText.color = Dark;
        horizontalDivider.transform.localPosition = new Vector3(0f, 0f, -0.01f);
    }

    private void LayoutChevron()
    {
        chevronRenderers[0].transform.localPosition = new Vector3(0.19f, -0.22f, -0.04f);
        chevronRenderers[0].transform.localRotation = Quaternion.Euler(0f, 0f, 42f);
        chevronRenderers[1].transform.localPosition = new Vector3(0.32f, -0.22f, -0.04f);
        chevronRenderers[1].transform.localRotation = Quaternion.Euler(0f, 0f, -42f);
    }

    private void SetPlatformGeometry(Vector2 size, Vector2 center)
    {
        platformCollider.size = size;
        platformCollider.offset = center;
        background.size = size;
        background.transform.localPosition = new Vector3(center.x, center.y, 0f);
    }

    private void SetCollapsedVisualsVisible(bool visible)
    {
        collapsedOuter.gameObject.SetActive(visible);
        collapsedInner.gameObject.SetActive(visible);
        collapsedLabel.gameObject.SetActive(visible);
        foreach (SpriteRenderer chevron in chevronRenderers)
        {
            chevron.gameObject.SetActive(visible);
        }
    }

    private void SetExpandedVisualsVisible(bool visible)
    {
        foreach (SpriteRenderer renderer in candidateOuterRenderers)
        {
            renderer.gameObject.SetActive(visible);
        }

        foreach (SpriteRenderer renderer in candidateInnerRenderers)
        {
            renderer.gameObject.SetActive(visible);
        }

        foreach (TMP_Text label in candidateLabels)
        {
            label.gameObject.SetActive(visible);
        }

        selectedText.gameObject.SetActive(visible);
        horizontalDivider.gameObject.SetActive(visible);
    }

    private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, Color color, int sortingOrder)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private TMP_Text CreateLabel(
        string objectName,
        TMP_FontAsset fontAsset,
        float width,
        float height,
        int sortingOrder,
        float fontSize)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        TextMeshPro label = child.AddComponent<TextMeshPro>();
        if (fontAsset != null)
        {
            label.font = fontAsset;
        }

        label.alignment = TextAlignmentOptions.Center;
        label.color = Dark;
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Truncate;
        label.rectTransform.sizeDelta = new Vector2(width, height);
        label.GetComponent<Renderer>().sortingOrder = sortingOrder;
        return label;
    }

    private static char GetCandidateLetter(int index) => (char)('A' + Mathf.Clamp(index, 0, 2));
}

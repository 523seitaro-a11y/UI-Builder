using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class RandomStepPlatform : MonoBehaviour, BlockManager.IPlayModeBlockState
{
    private static readonly Color32 Dark = new Color32(59, 59, 59, 255);
    private static readonly Color32 Light = new Color32(235, 235, 235, 255);
    private static readonly Color32 Muted = new Color32(145, 145, 145, 255);

    private const float NodeOuterSize = 0.62f;
    private const float NodeInnerSize = 0.48f;

    [SerializeField] private BoxCollider2D platformCollider;
    [SerializeField] private SpriteRenderer background;
    [SerializeField] private SpriteRenderer[] connectorRenderers;
    [SerializeField] private SpriteRenderer[] nodeOuterRenderers;
    [SerializeField] private SpriteRenderer[] nodeInnerRenderers;
    [SerializeField] private TMP_Text[] nodeLabels;

    public int CurrentStep { get; private set; } = 1;

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

        connectorRenderers = new SpriteRenderer[2];
        for (int i = 0; i < connectorRenderers.Length; i++)
        {
            connectorRenderers[i] = CreateRenderer($"Connector{i + 1}", solidSprite, Muted, 11);
        }

        nodeOuterRenderers = new SpriteRenderer[3];
        nodeInnerRenderers = new SpriteRenderer[3];
        nodeLabels = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            nodeOuterRenderers[i] = CreateRenderer($"Node{i + 1}Outer", circleSprite, Dark, 12);
            nodeInnerRenderers[i] = CreateRenderer($"Node{i + 1}Inner", circleSprite, Light, 13);
            nodeLabels[i] = CreateNodeLabel($"Node{i + 1}Label", (i + 1).ToString(), fontAsset);
        }

        ApplyBuildState();
    }

    public void OnPlayModeEntered() => ApplyStep(Random.Range(1, 4));

    public void OnBuildModeEntered() => ApplyBuildState();

    private void ApplyBuildState()
    {
        CurrentStep = 1;
        SetGeometry(3);
        LayoutNodes(3, false);
    }

    private void ApplyStep(int step)
    {
        CurrentStep = Mathf.Clamp(step, 1, 3);
        SetGeometry(CurrentStep);
        LayoutNodes(CurrentStep, true);
    }

    private void SetGeometry(int width)
    {
        Vector2 size = new Vector2(width, 1f);
        float centerX = (width - 3f) * 0.5f;
        platformCollider.size = size;
        platformCollider.offset = new Vector2(centerX, 0f);
        background.size = size;
        background.transform.localPosition = new Vector3(centerX, 0f, 0f);
    }

    private void LayoutNodes(int visibleCount, bool showProgress)
    {
        for (int i = 0; i < nodeOuterRenderers.Length; i++)
        {
            bool visible = i < visibleCount;
            SetNodeVisible(i, visible);
            if (!visible)
            {
                continue;
            }

            float x = i - 1f;
            Vector3 position = new Vector3(x, 0f, -0.02f);
            nodeOuterRenderers[i].transform.localPosition = position;
            nodeInnerRenderers[i].transform.localPosition = position + new Vector3(0f, 0f, -0.01f);
            nodeLabels[i].transform.localPosition = position + new Vector3(0f, 0f, -0.02f);

            nodeOuterRenderers[i].color = Dark;
            nodeOuterRenderers[i].transform.localScale = Vector3.one * NodeOuterSize;
            nodeInnerRenderers[i].color = showProgress ? Dark : Light;
            nodeInnerRenderers[i].transform.localScale = Vector3.one * NodeInnerSize;
            nodeLabels[i].color = showProgress ? Color.white : Muted;
        }

        for (int i = 0; i < connectorRenderers.Length; i++)
        {
            bool visible = i < visibleCount - 1;
            connectorRenderers[i].gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            float leftX = i - 1f;
            connectorRenderers[i].transform.localPosition = new Vector3(leftX + 0.5f, 0f, -0.01f);
            connectorRenderers[i].transform.localScale = new Vector3(0.42f, 0.075f, 1f);
            connectorRenderers[i].color = showProgress ? Dark : Muted;
        }
    }

    private void SetNodeVisible(int index, bool visible)
    {
        nodeOuterRenderers[index].gameObject.SetActive(visible);
        nodeInnerRenderers[index].gameObject.SetActive(visible);
        nodeLabels[index].gameObject.SetActive(visible);
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

    private TMP_Text CreateNodeLabel(string objectName, string value, TMP_FontAsset fontAsset)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        TextMeshPro label = child.AddComponent<TextMeshPro>();
        if (fontAsset != null)
        {
            label.font = fontAsset;
        }

        label.text = value;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Dark;
        label.fontSize = 2.8f;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Truncate;
        label.rectTransform.sizeDelta = new Vector2(0.58f, 0.58f);
        label.GetComponent<Renderer>().sortingOrder = 14;
        return label;
    }
}

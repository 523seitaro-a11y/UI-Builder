using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class ButtonHoverOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Color outlineColor = new(1f, 0.5764706f, 0.5803922f, 1f);
    [SerializeField, Min(0f)] private float outlineWidth = 2f;

    [Tooltip("背後のボタン操作を遮らずに、カーソル位置から枠を表示します。装飾フレーム用です。")]
    [SerializeField] private bool hoverWithoutRaycast;

    [Tooltip("0より大きい場合、ワールド上の既存ブロック枠と同じ太さに換算します。")]
    [SerializeField, Min(0f)] private float worldOutlineWidth;

    [SerializeField] private bool invertSpriteWhileOutlined;
    private bool holdOutline;
    private bool selectedInversion;
    [SerializeField] private Sprite selectedSprite;
    private Sprite normalSprite;
    private TMP_Text[] childTexts;
    private Color[] childTextColors;
    private int numberTextIndex = -1;
    private int stageNameTextIndex = -1;
    private Material normalMaterial;
    private Material inversionMaterial;

    private Selectable selectable;
    private RectTransform targetRect;
    private RectTransform outlineRect;
    private Image outlineImage;
    private Material outlineMaterial;

    private void Awake()
    {
        childTexts = GetComponentsInChildren<TMP_Text>(true);
        childTextColors = new Color[childTexts.Length];
        for (int i = 0; i < childTexts.Length; i++)
        {
            childTextColors[i] = childTexts[i].color;
            if (childTexts[i].name == "Number") numberTextIndex = i;
            else if (childTexts[i].name == "StageName") stageNameTextIndex = i;
        }
        targetImage ??= GetComponent<Image>();
        if (targetImage != null) normalSprite = targetImage.sprite;
        selectable = GetComponent<Selectable>();
        targetRect = targetImage != null ? targetImage.rectTransform : null;
        if (targetImage != null) normalMaterial = targetImage.material;
        if (invertSpriteWhileOutlined)
        {
            Shader shader = Resources.Load<Shader>("UIButtonColorInvert");
            if (shader != null) inversionMaterial = new Material(shader)
                { hideFlags = HideFlags.HideAndDontSave };
        }
        CreateOutline();
        SetOutlineVisible(false);
    }

    private void OnEnable() => SetOutlineVisible(false);

    private void OnDisable()
    {
        holdOutline = false;
        SetOutlineVisible(false);
    }

    public void HoldOutlineUntilTransitionEnds(bool hold)
    {
        holdOutline = hold;
        SetOutlineVisible(hold);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetOutlineVisible(selectable == null || selectable.IsInteractable());
    }

    public void OnPointerExit(PointerEventData eventData) => SetOutlineVisible(false);

    public void SetSelectedInversion(bool selected)
    {
        selectedInversion = selected;
        if (selected) SetOutlineVisible(false);
        if (targetImage != null && selectedSprite != null)
            targetImage.sprite = selected ? selectedSprite : normalSprite;
        if (childTexts != null)
        {
            for (int i = 0; i < childTexts.Length; i++)
            {
                if (childTexts[i] == null) continue;
                int colorIndex = i;
                if (selected && numberTextIndex >= 0 && stageNameTextIndex >= 0)
                {
                    if (i == numberTextIndex) colorIndex = stageNameTextIndex;
                    else if (i == stageNameTextIndex) colorIndex = numberTextIndex;
                }
                childTexts[i].color = childTextColors[colorIndex];
            }
        }
        if (targetImage != null && inversionMaterial != null)
            targetImage.material = holdOutline || (selectedInversion && selectedSprite == null) ? inversionMaterial : normalMaterial;
    }

    private void CreateOutline()
    {
        if (targetImage == null || targetRect == null || targetRect.parent == null)
        {
            return;
        }

        Shader shader = Resources.Load<Shader>("StartButtonOutlineSolid");
        if (shader == null)
        {
            Debug.LogWarning("StartButtonOutlineSolid shader was not found.", this);
            return;
        }

        outlineMaterial = new Material(shader)
        {
            name = "Start Button Hover Border",
            hideFlags = HideFlags.HideAndDontSave
        };

        GameObject outlineObject = new(
            targetImage.name + " (Hover Outline)",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        outlineObject.layer = gameObject.layer;
        outlineRect = outlineObject.GetComponent<RectTransform>();
        outlineRect.SetParent(targetRect.parent, false);
        outlineRect.SetSiblingIndex(targetRect.GetSiblingIndex());

        outlineImage = outlineObject.GetComponent<Image>();
        outlineImage.raycastTarget = false;
        outlineImage.maskable = targetImage.maskable;
        outlineImage.material = outlineMaterial;
        SynchronizeOutline();
    }

    private void SynchronizeOutline()
    {
        if (outlineImage == null || outlineRect == null || targetImage == null || targetRect == null)
        {
            return;
        }

        outlineImage.sprite = targetImage.sprite;
        outlineImage.type = targetImage.type;
        outlineImage.preserveAspect = targetImage.preserveAspect;
        outlineImage.fillCenter = targetImage.fillCenter;
        outlineImage.fillMethod = targetImage.fillMethod;
        outlineImage.fillAmount = targetImage.fillAmount;
        outlineImage.fillClockwise = targetImage.fillClockwise;
        outlineImage.fillOrigin = targetImage.fillOrigin;
        outlineImage.color = outlineColor;

        outlineRect.anchorMin = targetRect.anchorMin;
        outlineRect.anchorMax = targetRect.anchorMax;
        outlineRect.pivot = targetRect.pivot;
        outlineRect.anchoredPosition = targetRect.anchoredPosition;
        outlineRect.sizeDelta = targetRect.sizeDelta + GetOutlinePadding() * 2f;
        outlineRect.localRotation = targetRect.localRotation;
        outlineRect.localScale = targetRect.localScale;
        outlineRect.SetSiblingIndex(Mathf.Max(0, targetRect.GetSiblingIndex() - 1));
    }

    private Vector2 GetOutlinePadding()
    {
        Camera worldCamera = Camera.main;
        if (worldOutlineWidth <= 0f || worldCamera == null || !worldCamera.orthographic)
            return Vector2.one * outlineWidth;

        Canvas canvas = targetImage.canvas.rootCanvas;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 origin = RectTransformUtility.WorldToScreenPoint(uiCamera, targetRect.position);
        float pixelsPerX = (RectTransformUtility.WorldToScreenPoint(uiCamera,
            targetRect.TransformPoint(Vector3.right)) - origin).magnitude;
        float pixelsPerY = (RectTransformUtility.WorldToScreenPoint(uiCamera,
            targetRect.TransformPoint(Vector3.up)) - origin).magnitude;
        float pixels = worldOutlineWidth * worldCamera.pixelHeight / (worldCamera.orthographicSize * 2f);
        return new Vector2(pixels / Mathf.Max(0.001f, pixelsPerX),
            pixels / Mathf.Max(0.001f, pixelsPerY));
    }

    private void SetOutlineVisible(bool visible)
    {
        visible = !selectedInversion && (holdOutline || visible) &&
            (outlineWidth > 0f || worldOutlineWidth > 0f);
        if (outlineImage != null) outlineImage.gameObject.SetActive(visible);
        if (targetImage != null && inversionMaterial != null)
            targetImage.material = holdOutline || (selectedInversion && selectedSprite == null) ? inversionMaterial : normalMaterial;
    }

    private void LateUpdate()
    {
        bool canHover = !ScreenTransition.IsTransitioning &&
            (selectable == null || selectable.IsInteractable());
        foreach (CanvasGroup group in GetComponentsInParent<CanvasGroup>())
        {
            if (!group.interactable || !group.blocksRaycasts || group.alpha <= 0f)
                canHover = false;
        }
        if (hoverWithoutRaycast)
        {
            Canvas canvas = targetImage != null ? targetImage.canvas : null;
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            SetOutlineVisible(canHover && Input.touchCount == 0 && targetRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(targetRect, Input.mousePosition, camera));
        }
        else if (!canHover) SetOutlineVisible(false);

        if (outlineImage != null && outlineImage.gameObject.activeSelf)
        {
            SynchronizeOutline();
        }
    }

    private void OnValidate() => targetImage ??= GetComponent<Image>();

    private void OnDestroy()
    {
        if (targetImage != null && inversionMaterial != null) targetImage.material = normalMaterial;
        if (inversionMaterial != null) Destroy(inversionMaterial);
        if (outlineMaterial != null)
        {
            Destroy(outlineMaterial);
        }

        if (outlineImage != null)
        {
            Destroy(outlineImage.gameObject);
        }
    }
}

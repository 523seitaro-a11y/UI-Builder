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

    private Selectable selectable;
    private RectTransform targetRect;
    private RectTransform outlineRect;
    private Image outlineImage;
    private Material outlineMaterial;

    private void Awake()
    {
        targetImage ??= GetComponent<Image>();
        selectable = GetComponent<Selectable>();
        targetRect = targetImage != null ? targetImage.rectTransform : null;
        CreateOutline();
        SetOutlineVisible(false);
    }

    private void OnEnable() => SetOutlineVisible(false);

    private void OnDisable() => SetOutlineVisible(false);

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetOutlineVisible(selectable == null || selectable.IsInteractable());
    }

    public void OnPointerExit(PointerEventData eventData) => SetOutlineVisible(false);

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
        outlineRect.sizeDelta = targetRect.sizeDelta + Vector2.one * (outlineWidth * 2f);
        outlineRect.localRotation = targetRect.localRotation;
        outlineRect.localScale = targetRect.localScale;
        outlineRect.SetSiblingIndex(Mathf.Max(0, targetRect.GetSiblingIndex() - 1));
    }

    private void SetOutlineVisible(bool visible)
    {
        if (outlineImage != null)
        {
            outlineImage.gameObject.SetActive(visible && outlineWidth > 0f);
        }
    }

    private void LateUpdate()
    {
        if (outlineImage != null && outlineImage.gameObject.activeSelf)
        {
            SynchronizeOutline();
        }
    }

    private void OnValidate() => targetImage ??= GetComponent<Image>();

    private void OnDestroy()
    {
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

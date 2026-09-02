using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面全体を覆うCanvas Imageの透明度を使って、ステージの明るさを制御します。
/// 明るさバーだけは専用の前面レイヤーへ複製し、暗転の対象外にします。
/// 1が通常の明るさ、0が最も暗い状態です。
/// </summary>
[DisallowMultipleComponent]
public sealed class ScreenBrightnessController : MonoBehaviour
{
    [Tooltip("画面全体（UIを含む）を暗くするCanvas Imageです。")]
    [SerializeField] private Image darknessOverlay;

    [Tooltip("暗転の対象外にする明るさバーを表示する前面レイヤーです。")]
    [SerializeField] private RectTransform visibilityLayer;

    [Range(0f, 1f)]
    [SerializeField] private float defaultBrightness = 1f;

    [Tooltip("ハンドルを左端からこの割合まで動かしても、完全暗転を維持します。")]
    [Range(0f, 0.5f)]
    [SerializeField] private float fullDarknessHandleRange = 0.2f;

    public float Brightness { get; private set; } = 1f;

    public RectTransform VisibilityLayer => visibilityLayer;
    public bool UsesWorldOverlay => false;

    private void Awake()
    {
        if (darknessOverlay != null)
        {
            darknessOverlay.enabled = true;
            darknessOverlay.raycastTarget = false;
        }

        EnsureOverlayOrder();
        ResetBrightness();
    }

    private void LateUpdate() => EnsureOverlayOrder();

    private void EnsureOverlayOrder()
    {
        // PauseやResultが実行中に描画順を変更しても、暗転を常にその手前へ戻します。
        darknessOverlay?.rectTransform.SetAsLastSibling();

        // 明るさバーの複製だけを暗転Imageより後に描画します。
        visibilityLayer?.SetAsLastSibling();
    }

    public void SetBrightness(float brightness)
    {
        float handleValue = Mathf.Clamp01(brightness);
        Brightness = Mathf.InverseLerp(fullDarknessHandleRange, 1f, handleValue);
        ApplyOverlay();
    }

    public void ResetBrightness() => SetBrightness(defaultBrightness);

    private void ApplyOverlay()
    {
        Color color = new Color32(0x3C, 0x3C, 0x3C, 0xFF);
        color.a = 1f - Brightness;

        if (darknessOverlay != null)
        {
            darknessOverlay.enabled = true;
            darknessOverlay.color = color;
            darknessOverlay.raycastTarget = false;
        }
    }

    private void OnValidate()
    {
        defaultBrightness = Mathf.Clamp01(defaultBrightness);
        fullDarknessHandleRange = Mathf.Clamp(fullDarknessHandleRange, 0f, 0.5f);

        if (!Application.isPlaying)
        {
            Brightness = defaultBrightness;
            ApplyOverlay();
        }
    }
}

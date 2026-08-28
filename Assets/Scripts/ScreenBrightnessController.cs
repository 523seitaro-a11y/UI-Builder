using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面全体を覆う黒いImageの透明度を使って、ステージの明るさを制御します。
/// 1が通常の明るさ、0が最も暗い状態です。
/// </summary>
[DisallowMultipleComponent]
public sealed class ScreenBrightnessController : MonoBehaviour
{
    [SerializeField] private Image darknessOverlay;

    [Tooltip("暗転中も表示する操作UIを置く、BrightnessOverlay直後のレイヤーです。")]
    [SerializeField] private RectTransform visibilityLayer;

    [Range(0f, 1f)]
    [SerializeField] private float defaultBrightness = 1f;

    [Tooltip("明るさが0のときに重ねる黒の最大不透明度です。1でゲーム世界を完全に暗転します。")]
    [Range(0f, 1f)]
    [SerializeField] private float maximumDarknessAlpha = 0.82f;

    [Tooltip("ハンドルを左端からこの割合まで動かしても、完全暗転を維持します。")]
    [Range(0f, 0.5f)]
    [SerializeField] private float fullDarknessHandleRange = 0.2f;

    public float Brightness { get; private set; } = 1f;

    public RectTransform VisibilityLayer => visibilityLayer;

    private void Awake() => ResetBrightness();

    public void SetBrightness(float brightness)
    {
        float handleValue = Mathf.Clamp01(brightness);
        Brightness = Mathf.InverseLerp(fullDarknessHandleRange, 1f, handleValue);
        ApplyOverlay();
    }

    public void ResetBrightness() => SetBrightness(defaultBrightness);

    private void ApplyOverlay()
    {
        if (darknessOverlay == null)
        {
            return;
        }

        Color color = darknessOverlay.color;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        color.a = (1f - Brightness) * maximumDarknessAlpha;
        darknessOverlay.color = color;
        darknessOverlay.raycastTarget = false;
    }

    private void OnValidate()
    {
        defaultBrightness = Mathf.Clamp01(defaultBrightness);
        maximumDarknessAlpha = Mathf.Clamp01(maximumDarknessAlpha);
        fullDarknessHandleRange = Mathf.Clamp(fullDarknessHandleRange, 0f, 0.5f);

        if (!Application.isPlaying)
        {
            Brightness = defaultBrightness;
            ApplyOverlay();
        }
    }
}

using System;
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

    [Range(0f, 1f)]
    [SerializeField] private float defaultBrightness = 1f;

    [Tooltip("明るさが0のときに重ねる黒の最大不透明度です。完全な黒にはせず、操作対象が見える値にします。")]
    [Range(0f, 1f)]
    [SerializeField] private float maximumDarknessAlpha = 0.82f;

    public event Action<float> BrightnessChanged;

    public float Brightness { get; private set; } = 1f;

    private void Awake() => ResetBrightness();

    public void SetBrightness(float brightness)
    {
        Brightness = Mathf.Clamp01(brightness);
        ApplyOverlay();
        BrightnessChanged?.Invoke(Brightness);
    }

    public void ResetBrightness() => SetBrightness(defaultBrightness);

    public bool IsAtOrBelow(float maximumBrightness) =>
        Brightness <= Mathf.Clamp01(maximumBrightness);

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

        if (!Application.isPlaying)
        {
            Brightness = defaultBrightness;
            ApplyOverlay();
        }
    }
}

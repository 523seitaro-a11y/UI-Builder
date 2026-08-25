using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Scrollbar))]
[DefaultExecutionOrder(-900)]
public sealed class BgmVolumeBar : MonoBehaviour
{
    [SerializeField] private Scrollbar scrollbar;

    private void Awake() => FindScrollbar();

    private void OnEnable()
    {
        FindScrollbar();
        if (scrollbar == null)
        {
            return;
        }

        scrollbar.SetValueWithoutNotify(AudioManager.CurrentBgmVolume);
        scrollbar.onValueChanged.RemoveListener(SetBgmVolume);
        scrollbar.onValueChanged.AddListener(SetBgmVolume);
        AudioManager.BgmVolumeChanged -= SynchronizeBar;
        AudioManager.BgmVolumeChanged += SynchronizeBar;
    }

    private void OnDisable()
    {
        if (scrollbar != null)
        {
            scrollbar.onValueChanged.RemoveListener(SetBgmVolume);
        }

        AudioManager.BgmVolumeChanged -= SynchronizeBar;
    }

    private static void SetBgmVolume(float volume) =>
        AudioManager.Instance?.SetBgmVolume(volume);

    private void SynchronizeBar(float volume) =>
        scrollbar?.SetValueWithoutNotify(volume);

    private void OnValidate() => FindScrollbar();

    private void FindScrollbar()
    {
        scrollbar ??= GetComponent<Scrollbar>();
    }
}

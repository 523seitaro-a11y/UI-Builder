using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Scrollbar))]
[DefaultExecutionOrder(-900)]
public sealed class BgmVolumeBar : MonoBehaviour
{
    private const int DefaultNumberOfSteps = 10;

    [SerializeField] private Scrollbar scrollbar;
    private int lastStepIndex;

    private void Awake() => FindScrollbar();

    private void OnEnable()
    {
        FindScrollbar();
        if (scrollbar == null)
        {
            return;
        }

        if (scrollbar.numberOfSteps <= 1)
        {
            scrollbar.numberOfSteps = DefaultNumberOfSteps;
        }

        float currentVolume = AudioManager.CurrentBgmVolume;
        scrollbar.SetValueWithoutNotify(currentVolume);
        lastStepIndex = GetStepIndex(currentVolume);
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

    private void SetBgmVolume(float volume)
    {
        int stepIndex = GetStepIndex(volume);
        bool stepChanged = stepIndex != lastStepIndex;
        AudioManager.Instance?.SetBgmVolume(volume);

        if (stepChanged)
        {
            lastStepIndex = stepIndex;
            AudioManager.Instance?.PlaySeVolumeStepSound();
        }
    }

    private void SynchronizeBar(float volume)
    {
        lastStepIndex = GetStepIndex(volume);
        scrollbar?.SetValueWithoutNotify(volume);
    }

    private int GetStepIndex(float volume)
    {
        int numberOfSteps = scrollbar != null && scrollbar.numberOfSteps > 1
            ? scrollbar.numberOfSteps
            : DefaultNumberOfSteps;
        return Mathf.RoundToInt(Mathf.Clamp01(volume) * (numberOfSteps - 1));
    }

    private void OnValidate() => FindScrollbar();

    private void FindScrollbar()
    {
        scrollbar ??= GetComponent<Scrollbar>();
    }
}

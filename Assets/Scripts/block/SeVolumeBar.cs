using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Scrollbar))]
[DefaultExecutionOrder(-900)]
public sealed class SeVolumeBar : MonoBehaviour
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

        float currentVolume = AudioManager.CurrentSeVolume;
        scrollbar.SetValueWithoutNotify(currentVolume);
        lastStepIndex = GetStepIndex(currentVolume);
        scrollbar.onValueChanged.RemoveListener(SetSeVolume);
        scrollbar.onValueChanged.AddListener(SetSeVolume);
        AudioManager.SeVolumeChanged -= SynchronizeBar;
        AudioManager.SeVolumeChanged += SynchronizeBar;
    }

    private void OnDisable()
    {
        if (scrollbar != null)
        {
            scrollbar.onValueChanged.RemoveListener(SetSeVolume);
        }

        AudioManager.SeVolumeChanged -= SynchronizeBar;
    }

    private void SetSeVolume(float volume)
    {
        int stepIndex = GetStepIndex(volume);
        bool stepChanged = stepIndex != lastStepIndex;
        AudioManager.Instance?.SetSeVolume(volume);

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

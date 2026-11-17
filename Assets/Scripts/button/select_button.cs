using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class select_button : MonoBehaviour
{
    [Header("フレーム選択からのステージ開始")]
    [SerializeField] private Button stageLaunchButton;
    [SerializeField] private TMP_Text stageLaunchLabel;
    [SerializeField] private ButtonHoverOutline[] stageFrames;
    [SerializeField] private GameObject stageWindowRoot;
    [SerializeField] private TMP_Text stageWindowNumber;
    [SerializeField] private TMP_Text stageWindowName;
    private int selectedFrameStage;

    private void Start()
    {
        if (stageLaunchButton != null) UpdateFrameSelection();
    }

    public void ToggleFrameStage(int stageNumber)
    {
        if (ScreenTransition.IsTransitioning || stageFrames == null ||
            stageNumber < 1 || stageNumber > stageFrames.Length) return;
        selectedFrameStage = selectedFrameStage == stageNumber ? 0 : stageNumber;
        UpdateFrameSelection();
    }

    private void UpdateFrameSelection()
    {
        for (int i = 0; i < stageFrames.Length; i++)
            stageFrames[i]?.SetSelectedInversion(selectedFrameStage == i + 1);
        stageLaunchButton.interactable = selectedFrameStage > 0;
        if (stageLaunchLabel != null)
            stageLaunchLabel.text = selectedFrameStage > 0 ? selectedFrameStage.ToString() : "—";

        string number = "—";
        string stageName = "";
        if (selectedFrameStage > 0 && stageFrames[selectedFrameStage - 1] != null)
        {
            number = selectedFrameStage.ToString("00");
            foreach (TMP_Text label in stageFrames[selectedFrameStage - 1].GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name == "Number") number = label.text;
                else if (label.name == "StageName") stageName = label.text;
            }
        }
        if (stageWindowRoot != null) stageWindowRoot.SetActive(selectedFrameStage > 0);
        if (stageWindowNumber != null) stageWindowNumber.text = number;
        if (stageWindowName != null) stageWindowName.text = stageName;
    }

    public void OnSelectedFrameStageClick()
    {
        if (selectedFrameStage > 0 && !ScreenTransition.IsTransitioning)
            LoadStage(selectedFrameStage);
    }

    private static void LoadStage(int stageNumber)
    {
        string sceneName = $"Stage{stageNumber}";
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"StageSelect: '{sceneName}' はBuild Settingsに登録されていません。");
            return;
        }

        ScreenTransition.LoadScene(sceneName);
    }

    public void OnSelectButtonClickStage1()
    {
        LoadStage(1);
    }

    public void OnSelectButtonClickStage2()
    {
        LoadStage(2);
    }

    public void OnSelectButtonClickStage3()
    {
        LoadStage(3);
    }

    public void OnSelectButtonClickStage4()
    {
        LoadStage(4);
    }

    public void OnSelectButtonClickStage5()
    {
        LoadStage(5);
    }

    public void OnSelectButtonClickStage6()
    {
        LoadStage(6);
    }

    public void OnSelectButtonClickStage7() => LoadStage(7);
    public void OnSelectButtonClickStage8() => LoadStage(8);
    public void OnSelectButtonClickStage9() => LoadStage(9);
    public void OnSelectButtonClickStage10() => LoadStage(10);
    public void OnSelectButtonClickStage11() => LoadStage(11);
    public void OnSelectButtonClickStage12() => LoadStage(12);
    public void OnSelectButtonClickStage13() => LoadStage(13);
    public void OnSelectButtonClickStage14() => LoadStage(14);
    public void OnSelectButtonClickStage15() => LoadStage(15);
}

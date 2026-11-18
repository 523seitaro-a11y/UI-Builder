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
    [SerializeField] private RectTransform selectLine;
    private Vector3 selectLineOffset;
    private bool hasSelectLineOffset;
    private int selectedFrameStage;
    private Image landformImage;
    private bool launchReady = true;
    public RectTransform LaunchFrame => stageLaunchButton != null ? stageLaunchButton.transform as RectTransform : null;
    public RectTransform PlayerFace => stageLaunchButton != null
        ? stageLaunchButton.GetComponent<StageStartIconHover>()?.PlayerIcon : null;

    public void SetLaunchReady(bool ready)
    {
        launchReady = ready;
        if (PlayerFace != null) PlayerFace.gameObject.SetActive(ready);
        if (stageLaunchButton != null) stageLaunchButton.interactable = ready && selectedFrameStage > 0;
    }

    private void Start()
    {
        if (stageLaunchButton == null) return;
        // 戻り先の選択が復元済みなら維持し、初回起動ではステージ1を選びます。
        if (selectedFrameStage == 0) SelectFrameStage(1);
        else UpdateFrameSelection();
    }

    public void ToggleFrameStage(int stageNumber)
    {
        if (ScreenTransition.IsTransitioning || stageFrames == null ||
            stageNumber < 1 || stageNumber > stageFrames.Length ||
            selectedFrameStage == stageNumber) return;
        selectedFrameStage = stageNumber;
        UpdateFrameSelection();
    }

    public void SelectFrameStage(int stageNumber)
    {
        if (stageLaunchButton == null || stageFrames == null) return;
        selectedFrameStage = stageNumber >= 1 && stageNumber <= stageFrames.Length
            ? stageNumber : 0;
        UpdateFrameSelection();
    }

    private void UpdateFrameSelection()
    {
        for (int i = 0; i < stageFrames.Length; i++)
            stageFrames[i]?.SetSelectedInversion(selectedFrameStage == i + 1);
        stageLaunchButton.interactable = launchReady && selectedFrameStage > 0;
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
        if (stageWindowRoot != null)
            stageWindowRoot.GetComponent<StageBlockPreview>()?.ShowStage(selectedFrameStage);
        UpdateLandform();
        UpdateSelectLine();
    }

    private void LateUpdate() => UpdateSelectLine();

    private void UpdateSelectLine()
    {
        if (selectLine == null || stageFrames == null || stageFrames.Length == 0) return;
        Transform parent = selectLine.parent;
        if (!hasSelectLineOffset && stageFrames[0] != null)
        {
            // 編集時にステージ1の横へ置いた位置を、各フレーム共通のオフセットにします。
            selectLineOffset = selectLine.localPosition
                - parent.InverseTransformPoint(stageFrames[0].transform.position);
            hasSelectLineOffset = true;
        }
        bool selected = selectedFrameStage > 0 && selectedFrameStage <= stageFrames.Length &&
            stageFrames[selectedFrameStage - 1] != null;
        selectLine.gameObject.SetActive(selected);
        if (selected && hasSelectLineOffset)
            selectLine.localPosition = parent.InverseTransformPoint(
                stageFrames[selectedFrameStage - 1].transform.position) + selectLineOffset;
    }

    private void UpdateLandform()
    {
        if (landformImage == null && stageWindowRoot != null)
        {
            Transform landform = stageWindowRoot.transform.Find("Landform");
            if (landform != null) landformImage = landform.GetComponent<Image>();
        }
        if (landformImage == null) return;

        landformImage.sprite = selectedFrameStage > 0
            ? Resources.Load<Sprite>($"StagePreviews/Stage{selectedFrameStage}")
            : null;
        landformImage.preserveAspect = true;
        landformImage.raycastTarget = false;
        landformImage.enabled = landformImage.sprite != null;
    }

    public void OnSelectedFrameStageClick()
    {
        if (launchReady && selectedFrameStage > 0 && !ScreenTransition.IsTransitioning)
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

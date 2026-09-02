using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class select_button : MonoBehaviour {

    private static void LoadStage(int stageNumber)
    {
        string sceneName = $"Stage{stageNumber}";
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"StageSelect: '{sceneName}' はBuild Settingsに登録されていません。");
            return;
        }

        SceneManager.LoadScene(sceneName);
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

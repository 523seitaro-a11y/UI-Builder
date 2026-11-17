using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class start_button : MonoBehaviour {

    public void OnButtonClick()
    {
        ScreenTransition.LoadScene("Stage1");
    }

    public void OnStageSelectButtonClick()
    {
        TitleBuildManager title = FindFirstObjectByType<TitleBuildManager>();
        if (title != null) title.OpenStageSelect();
        else ScreenTransition.LoadScene("StageSelect");
    }
}

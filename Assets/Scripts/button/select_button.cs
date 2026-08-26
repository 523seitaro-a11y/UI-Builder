using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class select_button : MonoBehaviour {

    public void OnSelectButtonClickStage1()
    {
        SceneManager.LoadScene("Stage1");
    }

    public void OnSelectButtonClickStage2()
    {
        SceneManager.LoadScene("Stage2");
    }

    public void OnSelectButtonClickStage3()
    {
        SceneManager.LoadScene("Stage3");
    }

    public void OnSelectButtonClickStage4()
    {
        SceneManager.LoadScene("Stage4");
    }

    public void OnSelectButtonClickStage5()
    {
        SceneManager.LoadScene("Stage5");
    }

    // public void OnSelectButtonClickStage6()
    // {
    //     SceneManager.LoadScene("Stage6");
    // }

    // public void OnSelectButtonClickStage7()
    // {
    //     SceneManager.LoadScene("Stage7");
    // }

    // public void OnSelectButtonClickStage8()
    // {
    //     SceneManager.LoadScene("Stage8");
    // }

    // public void OnSelectButtonClickStage9()
    // {
    //     SceneManager.LoadScene("Stage9");
    // }
}
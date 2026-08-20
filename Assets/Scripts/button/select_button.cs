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
}
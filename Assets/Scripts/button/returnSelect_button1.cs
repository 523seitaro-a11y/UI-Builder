using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class returnSelect_button : MonoBehaviour {

    public void OnButtonClick()
    {
        SceneManager.LoadScene("StageSelect");
    }
}
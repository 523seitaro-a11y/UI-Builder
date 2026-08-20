using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class return_button : MonoBehaviour {

    public void OnButtonClick()
    {
        SceneManager.LoadScene("Title");
    }
}
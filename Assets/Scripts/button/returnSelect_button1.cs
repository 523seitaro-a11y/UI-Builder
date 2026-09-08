using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class returnSelect_button : MonoBehaviour {

    public void OnButtonClick()
    {
        ScreenTransition.LoadScene("StageSelect");
    }

    public void OnTitleButtonClick()
    {
        ScreenTransition.LoadScene("Title");
    }
}

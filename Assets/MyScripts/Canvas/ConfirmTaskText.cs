using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ConfirmTaskText : MonoBehaviour
{

    public ConfirmationSelector ConfirmUI;

    public TextMeshProUGUI tmpText;

    public void OnEnable(){
        tmpText.text = "Go to Task "+ CanvasGlobal.TaskNum;
    }

}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static OVRInput;

public class TaskSelector : MonoBehaviour
{
    public GameObject TaskUI;

    public GameObject radialUI;

    public GameObject ConfirmUI;

    private float coolDown = .25f;

    void Start(){
    }

    void Update(){

        if(radialUI.activeSelf){
             if(Time.time - CanvasGlobal.hitTime >= coolDown){
                if(OVRInput.Get(OVRInput.Button.PrimaryThumbstickRight, OVRInput.Controller.RTouch)){
                    CanvasGlobal.hitTime= Time.time;
                    Debug.Log(TaskUI.activeSelf);
                    onConfirmation(2);
                }

                if(OVRInput.Get(OVRInput.Button.PrimaryThumbstickLeft, OVRInput.Controller.RTouch)){
                    CanvasGlobal.hitTime= Time.time;
                    onConfirmation(1);
                }
            }
        }
    }

    void onConfirmation(int i){
        CanvasGlobal.TaskNum=i;
        ConfirmUI.SetActive(true);
        TaskUI.SetActive(false);
    }

}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static OVRInput;

public class ConfirmationSelector : MonoBehaviour
{

    public GameObject TasksCanvas;
    public GameObject TaskUI;

    public GameObject ConfirmUI;

    public float coolDown = .25f;

    void Start(){
        ConfirmUI.SetActive(false);
    }

    void Update(){

        if(ConfirmUI.activeSelf){
            if(Time.time - CanvasGlobal.hitTime >= coolDown){

                if(OVRInput.Get(OVRInput.Button.PrimaryThumbstickRight, OVRInput.Controller.RTouch)){
                    CanvasGlobal.hitTime = Time.time;
                    TaskUI.SetActive(true);
                    ConfirmUI.SetActive(false);
                }

                if(OVRInput.Get(OVRInput.Button.PrimaryThumbstickLeft, OVRInput.Controller.RTouch)){
                    if(CanvasGlobal.TaskNum == 1){
                        TasksGlobal.Task1Active = true;
                    }
                    else{
                        if(CanvasGlobal.TaskNum == 2){
                            TasksGlobal.Task2Active = true;
                        }
                    }
                }
            }
                
        }
    }

}

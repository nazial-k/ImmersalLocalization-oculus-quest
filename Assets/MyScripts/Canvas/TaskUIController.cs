using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static OVRInput;

public class TaskUIController : MonoBehaviour
{
    public GameObject TaskRadialUI;
    //public Controller controller;

    void Start(){
        TaskRadialUI.SetActive(false);
    }

    void Update(){
        float val = OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger);
        if(val>0.9){
            TaskRadialUI.SetActive(true);
        }
        else{
            TaskRadialUI.SetActive(false);
        }
    }
    
}

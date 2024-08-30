using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class TasksGlobal : MonoBehaviour
{
    public static bool Task1Active = false;

    private bool allJoined = false;

    public static bool Task2Active = false;

    public GameObject Task1;

    //public GameObject Task2;

    public GameObject CanvasUI;

    public bool checkConnection(){
        if(PhotonNetwork.IsConnectedAndReady == true && PhotonNetwork.InRoom == true){
            if(PhotonNetwork.CurrentRoom.PlayerCount>1){
                allJoined = true;
                return allJoined;
            }
        }
        return false;
    }

    void Update(){
        //Task1Active = true;
        if(checkConnection()){

            if(Task1Active){

                CanvasUI.SetActive(false);

                Task1 Script1 = Task1.GetComponent<Task1>();

                Script1.spawnTarget();

            }

        }
    }
}
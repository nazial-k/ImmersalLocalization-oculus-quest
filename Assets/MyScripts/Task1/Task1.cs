using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class Task1 : MonoBehaviourPunCallbacks
{

    public GameObject TargetPrefab;
    public GameObject PhotonParent;

    private Vector3 Pos = new Vector3(0.46f,-0.5f,1.2f);

    private bool Spawned = false;

    public GameObject VRSpherePrefab;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void spawnTarget(){
        if(!Spawned){
            GameObject TargetObject = PhotonNetwork.Instantiate(TargetPrefab.name,Pos, Quaternion.identity, 0);
            //TargetObject.transform.parent = PhotonObjects.transform;

            //Debug.Log(PhotonParent);

            Spawned=true;
            
            this.photonView.RPC("spawnSpheres", RpcTarget.All);
            //Debug.Log("Target Spawned");

        }
    }

    [PunRPC]
    public void spawnSpheres(){
        GameObject VRSphere = Instantiate(VRSpherePrefab);
    }

}

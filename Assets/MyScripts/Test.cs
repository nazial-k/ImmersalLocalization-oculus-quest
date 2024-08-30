using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class Test : MonoBehaviour
{
    public PhotonView View;
    // Start is called before the first frame update
    void Start()
    {
        View.GetComponent<PhotonView>();
        StartCoroutine(waitTillJoin());
    }

    // Update is called once per frame
    private IEnumerator waitTillJoin(){
        yield return new WaitUntil(() => PhotonNetwork.IsConnectedAndReady == true && PhotonNetwork.InRoom == true);
        View.RPC("test01", RpcTarget.AllBuffered);
    }

    [PunRPC]
    private void test01 (){
        Debug.Log("Message Recevied");
    }
}
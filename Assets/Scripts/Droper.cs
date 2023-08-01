using System.Collections;
using System.Collections.Generic;
using UIScripts;
using UnityEngine;
//require box collider
[RequireComponent(typeof(BoxCollider))]

public class Droper : MonoBehaviour
{
    //  [SerializeField] FixedJoint fixedJoint;
    [SerializeField] public Rigidbody connectedBody;
    [SerializeField] BoxCollider boxCollider;
    [SerializeField] FixedJoint fixedJoint;
    private void Start()
    {
        boxCollider = GetComponent<BoxCollider>(); boxCollider.isTrigger = true;


    }
    private void OnValidate()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        // fixedJoint = GetComponent<FixedJoint>();
    }
    private void FixedUpdate()
    {
        if (connectedBody)
        {
            //lerp the rotation of the bomb to the rotation of the drone
            //   connectedBody.transform.rotation = Quaternion.Lerp(connectedBody.transform.rotation, this.transform.rotation, 0.1f);
            //connectedBody.position = this.transform.position;
        }
    }
    /*
    private void OnTriggerEnter(Collider other)
    {
        if (connectedBody == null && other.GetComponent<Bomb>() != null)
        {
            Debug.Log("Pick up");
            other.GetComponent<Bomb>().isConnected = true;
            connectedBody = other.GetComponent<Rigidbody>();
            connectedBody.transform.position = this.transform.position;
            connectedBody.transform.parent = this.transform;
            connectedBody.transform.rotation = this.transform.rotation;
            connectedBody.constraints = RigidbodyConstraints.FreezeAll;
        }
    }
    public void Droup()
    {
        if (connectedBody != null)
        {
            Debug.Log("Droup");
            connectedBody.GetComponent<Bomb>().isConnected = false;

            connectedBody.transform.parent = null;
            connectedBody.constraints = RigidbodyConstraints.None;
            connectedBody.velocity = DataDisplay.Instance.dataList.droneRigidbody.velocity;
            Invoke("InvokeDisconnect", 1);

            // //use fixed joint to disconnect the bomb and the drone
            // fixedJoint.breakForce = 0;
        } 
    }*/


    private void OnTriggerEnter(Collider other)
    {
        if (connectedBody == null && other.GetComponent<Bomb>() != null)
        {
            gameObject.AddComponent<FixedJoint>();
            fixedJoint = GetComponent<FixedJoint>();
            connectedBody = other.GetComponent<Rigidbody>();

            var BombScript = other.GetComponent<Bomb>();

            connectedBody.transform.parent = this.transform;
            // connectedBody.transform.position = boxCollider.bounds.center;

            connectedBody.transform.position = boxCollider.bounds.center + BombScript.differPosition;
            connectedBody.rotation = this.transform.rotation;

            connectedBody.interpolation = RigidbodyInterpolation.Interpolate;
            fixedJoint.breakForce = Mathf.Infinity; fixedJoint.connectedBody = connectedBody;
            BombScript.isConnected = true;
        }
    }
    public void Droup()
    {
        if (connectedBody != null)
        {
            Debug.Log("Droup");
            connectedBody.GetComponent<Bomb>().isConnected = false;

            fixedJoint.breakForce = 0;
            Invoke("InvokeDisconnect", 1);

            // //use fixed joint to disconnect the bomb and the drone

        }
    }
    void InvokeConnectSeeting()
    {
        connectedBody.position = this.transform.position;
    }
    void InvokeDisconnect()
    {
        connectedBody.transform.parent = null;
        connectedBody = null;

    }
}

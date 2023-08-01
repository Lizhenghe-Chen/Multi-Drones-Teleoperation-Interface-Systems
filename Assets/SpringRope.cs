using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpringRope : MonoBehaviour
{
    [Tooltip("Assign the drone body and place the ropeStart object at the position where the rope should start")]
    [SerializeField] private Rigidbody DroneBody;
    [SerializeField] private GameObject ropeStart;
    [SerializeField] private GameObject _spingRopePrefab;
    [SerializeField] private bool isHingeJoint = false;
    [SerializeField] private int _ropeCount = 10;
    [SerializeField] FixedJoint payloadConnectPoint;
    [SerializeField] private bool useCollider = false;
    [SerializeField] private bool useGravity = false;

    private void Awake()
    {
        ropeStart.GetComponent<FixedJoint>().connectedBody = DroneBody;
        if (isHingeJoint)
        {
            for (int i = 0; i < _ropeCount; i++)
            {
                GameObject rope = Instantiate(_spingRopePrefab, transform);
                rope.transform.position = ropeStart.transform.position + new Vector3(0, -0.15f, 0);
                rope.GetComponent<HingeJoint>().connectedBody = ropeStart.GetComponent<Rigidbody>();
                rope.GetComponent<CapsuleCollider>().enabled = useCollider;
                rope.GetComponent<Rigidbody>().useGravity = useGravity;
                ropeStart = rope;
            }
        }
        else
        {
            for (int i = 0; i < _ropeCount; i++)
            {
                GameObject rope = Instantiate(_spingRopePrefab, transform);
                rope.transform.position = ropeStart.transform.position + new Vector3(0, -0.1f, 0);
                rope.GetComponent<SpringJoint>().connectedBody = ropeStart.GetComponent<Rigidbody>();
                rope.GetComponent<SphereCollider>().enabled = useCollider;
                rope.GetComponent<Rigidbody>().useGravity = useGravity;
                ropeStart = rope;
            }
        }


        payloadConnectPoint.transform.position = ropeStart.transform.position;
        payloadConnectPoint.connectedBody = ropeStart.GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {

    }
}

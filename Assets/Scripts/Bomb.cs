using UnityEngine.InputSystem;
using UnityEngine;

public class Bomb : MonoBehaviour
{
    public Transform connectedPoint;

    public bool isConnected = false;
    public float explodeForce = 6000;
    public float explodeRadius = 30;
    [SerializeField] GameObject[] explosion;
    public Vector3 differPosition;
    //ignore the collision between the bomb and the drone
    private void Start()
    {
        //connectedPoint = transform.Find("ConnectedPoint").transform;
        differPosition = transform.position - connectedPoint.position;
    }
    private void OnCollisionEnter(Collision other)
    {
        Explode(other);
    }
    void Explode(Collision other)
    {

        if (other.gameObject.layer == 8 && isConnected)
        {
            //Physics.IgnoreCollision(other.collider, GetComponent<Collider>());
            return;
        }
        //        Debug.Log(other.gameObject.name + "," + (other.impulse / Time.fixedDeltaTime).magnitude);
        //if the hit force is greater than 1, the bomb will explode
        if ((other.impulse / Time.fixedDeltaTime).magnitude > 1)
        {
            Debug.Log(other.collider.name);
            //add explosion force to the objects in the explosion array
            Collider[] colliders = Physics.OverlapSphere(transform.position, explodeRadius);
            foreach (var item in colliders)
            {
                if (item.GetComponent<Rigidbody>())
                {
                    item.GetComponent<Rigidbody>().AddExplosionForce(explodeForce, transform.position, explodeRadius);
                }
            }

            foreach (var item in explosion)
            {
                Destroy(Instantiate(item, transform.position, Quaternion.identity), 5);
                Destroy(gameObject);
            }
        }
    }

}

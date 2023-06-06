using UnityEngine.InputSystem;
using UnityEngine;

public class Bomb : MonoBehaviour
{
    [SerializeField] GameObject[] explosion;
    public bool isConnected = false;
    //ignore the collision between the bomb and the drone

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
        Debug.Log(other.gameObject.name + "," + (other.impulse / Time.fixedDeltaTime).magnitude);
        //if the hit force is greater than 1, the bomb will explode
        if ((other.impulse / Time.fixedDeltaTime).magnitude > 1)
        {
            Debug.Log(other.collider.name);
            //add explosion force to the objects in the explosion array
            Collider[] colliders = Physics.OverlapSphere(transform.position, 30);
            foreach (var item in colliders)
            {
                if (item.GetComponent<Rigidbody>())
                {
                    item.GetComponent<Rigidbody>().AddExplosionForce(6000, transform.position, 30);
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

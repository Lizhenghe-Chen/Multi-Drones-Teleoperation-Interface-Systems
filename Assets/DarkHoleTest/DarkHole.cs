using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DarkHole : MonoBehaviour
{
    //https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@11.0/manual/renderer-features/how-to-custom-effect-render-objects.html
    // Start is called before the first frame update
    public Collider groundCollider;
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Disable collisions " + other.name);
        //ignore collisions with ground layer
        IgnoreCollision(other, groundCollider);

    }
    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Reset collisions " + other.name);
        //reset collisions with ground layer
        ResetCollision(other, groundCollider);
    }
    public void IgnoreCollision(Collider collider1, Collider collider2)
    {
        Physics.IgnoreCollision(collider1, collider2, true);
    }
    public void ResetCollision(Collider collider1, Collider collider2)
    {
        Physics.IgnoreCollision(collider1, collider2, false);
    }
}

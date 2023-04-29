using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DarkHoelDestory : MonoBehaviour
{
  private void OnTriggerEnter(Collider other) {
    Debug.Log("DarkHoelDestory OnTriggerEnter");
    Destroy(other.gameObject,2f);
  }
}

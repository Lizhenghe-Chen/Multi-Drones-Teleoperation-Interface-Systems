using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;
using FlightControlBundle;
using UIScripts;

[RequireComponent(typeof(PlayerInput))]
/* 
* Copyright (c) [2023] [Lizhneghe.Chen https://github.com/Lizhenghe-Chen]
* Please do not use these code directly without permission.
*/
public class InputReciever : MonoBehaviour
{
    [Header("Joystick Debug:")]
    public Image leftJoystick;
    public Image rightJoystick;
    public Image acceleratorBar, verticalBar;
    public bool debugJoyStick = false;
    private Vector3 leftJoystick_initialPos, rightJoystick_initialPos;

    [Header("Input Debug:")]
    public Vector2 leftStick;
    public bool isAccelerating, isMoving;
    public float accelerator;
    public Vector2 lookInput;
    public Vector2 mouseLookInput;
    public float LBInput;
    public float RBInput;
    private DroneCtrl droneCtrl;
    private void Start()
    {
        droneCtrl = GetComponent<DroneCtrl>();
        if (debugJoyStick)
        {
            leftJoystick_initialPos = leftJoystick.transform.position;
            rightJoystick_initialPos = rightJoystick.transform.position;
        }
        //Invoke("OnRecenterPOVCamera", 1f);
        OnRecenterPOVCamera();
        OnSetPOVCameraInput();
    }
    #region Input System
    private void OnLeftStick(InputValue value)
    {
        leftStick = value.Get<Vector2>();
        accelerator = leftStick.y;
        isAccelerating = Mathf.Abs(value.Get<Vector2>().y) > 0;
        // droneCtrl.WindEffect();
        // Debug.Log("Move");
        DebugJoyStick();
    }
    private void OnRightStick(InputValue value)
    {
        if (POVCameraEnable) return;
        lookInput = value.Get<Vector2>();
        isMoving = Mathf.Abs(value.Get<Vector2>().x) > 0;
        DebugJoyStick();
    }

    private void OnRightArrow(InputValue value)
    {
        droneCtrl.Height_Balance_PID();
    }
    private void OnLeftArrow(InputValue value)
    {
        droneCtrl.Roll_Yaw_PID();
    }
    private void OnYawLeft(InputValue value)
    {
        LBInput = .5f * value.Get<float>();
    }
    private void OnYawRight(InputValue value)
    {
        RBInput = .5f * value.Get<float>();
    }
    [SerializeField] bool POVCameraEnable = true;
    private void OnSetPOVCameraInput()//tap
    {
        // mouseLookInput = value.Get<Vector2>();
        Debug.Log("DisablePOVCamera");
        POVCameraEnable = !POVCameraEnable;
        if (!DataDisplay.Instance) return;
        if (POVCameraEnable)
        {
            DataDisplay.Instance.DisableRecenterPOVCamera();
            DataDisplay.Instance.SetPOVCameraInput(true);
        }
        else DataDisplay.Instance.SetPOVCameraInput(false);

    }
    private void OnRecenterPOVCamera()//double click
    {
        Debug.Log("RecenterPOVCamera");
        if (DataDisplay.Instance) DataDisplay.Instance.EnableRecenterPOVCamera();
    }
    private void OnReset()
    {
        droneCtrl.ResetDrone();
    }
    private void OnUpArrow()
    {
        DataDisplay.Instance.ChangeCamera(1);
    }
    private void OnDroup()
    {

        droneCtrl.Droper.Droup();
      
    }
    #endregion

    private void DebugJoyStick()
    {
        if (!debugJoyStick) return;
        // if (accelerator > 0)
        // {
        //     acceleratorBar.fillAmount = accelerator / 1 + 0.5f;
        // }
        // else
        // {
        //     acceleratorBar.fillAmount = accelerator / 1 + 0.5f;
        // }
        acceleratorBar.fillAmount = accelerator / 2f + 0.5f;
        // leftJoystick.transform.position = leftJoystick_initialPos + new Vector3(leftStick.x * 100, leftStick.y * 100, 0);
        rightJoystick.transform.position = rightJoystick.transform.parent.position + new Vector3(lookInput.x * Screen.width * 0.05f, lookInput.y * Screen.width * 0.05f, 0);
    }

}

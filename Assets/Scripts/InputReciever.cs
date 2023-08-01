using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;
using FlightControlBundle;
using UIScripts;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(PlayerInput))]
/* 
* Copyright (c) [2023] [Lizhneghe.Chen https://github.com/Lizhenghe-Chen]
* Please do not use these code directly without permission.
*/
public class InputReciever : MonoBehaviour
{
    [Header("Joystick Debug:")]

    [SerializeField] private Transform RightStickParent;
    [SerializeField] private GameObject joystickTrailImage;
    //[SerializeField] private float joystickToScreenRatio = 17;
    [SerializeField] private Image leftJoystick;
    [SerializeField] private Image rightJoystick;
    [SerializeField] private AnimationCurve rightJoystickCurve;
    [SerializeField] private Image acceleratorBar;
    [SerializeField] private Image yawLeftBar, yawRightBar;

    [SerializeField] private AnimationCurve acceleratorCurve;
    [SerializeField] private bool debug = false;
    [SerializeField] private Vector3 leftJoystick_initialPos, rightJoystick_initialPos;

    [Header("Input Debug & Message Data:")]
    public Vector2 leftStick;
    public Vector2 rightStcikRaw, rightStcik;
    public bool isAccelerating, onRightJoystick;
    public float accelerator;

    public Vector2 mouseLookInput;
    public float LBInput;
    public float RBInput;
    private DroneCtrl droneCtrl;

    //joystickTrailImage list 
    [SerializeField] List<Image> joystickTrailImageList = new List<Image>();
    [SerializeField] float joyStickDebugOffset = 1;
    private void OnValidate()
    {
        //        Debug.Log(Screen.width);

    }
    private void Start()
    //get rect width in pixel
    {
        joyStickDebugOffset = 15f / 196f * Screen.width;
        //GameObject.Find("Canvas").GetComponent<Canvas>().worldCamera = Camera.main;
        // //instantiate 100 joystickTrailImage
        for (int i = 0; i < 150; i++)
        {
            var temp = Instantiate(joystickTrailImage, transform).GetComponent<Image>();
            temp.transform.SetParent(RightStickParent);
            temp.transform.localScale = Vector3.one;
            temp.transform.localPosition = Vector3.zero;
            joystickTrailImageList.Add(temp);
        }
        //  StartCoroutine(SetJoystickTrail());

        droneCtrl = GetComponent<DroneCtrl>();
        if (debug)
        {
            leftJoystick_initialPos = leftJoystick.transform.position;
            rightJoystick_initialPos = rightJoystick.transform.position;
        }
        //Invoke("OnRecenterPOVCamera", 1f);

        // OnRecenterPOVCamera(); 
        POVCameraEnable = true;
        OnSetPOVCameraInput();
        //  joystickToScreenRatio = Screen.width * joystickToScreenRatio;
    }
    private void OnLostFocus()
    {
        Debug.Log("OnLostFocus");
        //joystickToScreenRatio = Screen.width * joystickToScreenRatio;
    }
    #region Input System
    private void OnLeftStick(InputValue value)
    {
        leftStick = value.Get<Vector2>();
        accelerator = acceleratorCurve.Evaluate(leftStick.y);
        isAccelerating = Mathf.Abs(value.Get<Vector2>().y) > 0;
        // droneCtrl.WindEffect();
        // Debug.Log("Move");
        DebugAccelerator();
    }
    private void OnRightStick(InputValue value)
    {
        if (POVCameraEnable) return;
        //        Debug.Log(value.Get<Vector2>());
        //use rightJoystickCurve to make the input more linear and keep the input value for 2 decimal places
        rightStcikRaw = value.Get<Vector2>();
        rightStcik = new Vector2(
            rightJoystickCurve.Evaluate(rightStcikRaw.x),
            rightJoystickCurve.Evaluate(rightStcikRaw.y)
            );
        //rightStcik = value.Get<Vector2>();
        onRightJoystick = rightStcik.magnitude > 0.01;
        //        Debug.Log(onRightJoystick);
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
        LBInput = value.Get<float>();
        DebugYaw();
    }
    private void OnYawRight(InputValue value)
    {
        RBInput = value.Get<float>();
        DebugYaw();
    }
    [SerializeField] bool POVCameraEnable = true;
    private void OnSetPOVCameraInput()//tap
    {
        // mouseLookInput = value.Get<Vector2>();
        POVCameraEnable = !POVCameraEnable;
        if (!DataDisplay.Instance) return;
        if (POVCameraEnable)
        {
            Debug.Log("EnablePOVCamera");
            DataDisplay.Instance.DisableRecenterPOVCamera();
            DataDisplay.Instance.SetPOVCameraInput(true);
        }
        else { DataDisplay.Instance.SetPOVCameraInput(false); Debug.Log("DisablePOVCamera"); }

    }
    private void OnRecenterPOVCamera()//double click
    {
        Debug.Log("RecenterPOVCamera");
        POVCameraEnable = true;
        OnSetPOVCameraInput();
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
    int joystickTrailImageListIndex = 0;

    private void DebugJoyStick()
    {
        if (!debug) return;
         joyStickDebugOffset = 15f / 196f * Screen.width ;
        // leftJoystick.transform.position = leftJoystick_initialPos + new Vector3(leftStick.x * 100, leftStick.y * 100, 0);
        rightJoystick.transform.position = rightJoystick.transform.parent.position + new Vector3(rightStcik.x, rightStcik.y, 0) * joyStickDebugOffset;
    }
    private void DebugAccelerator()
    {
        if (!debug) return;
        acceleratorBar.fillAmount = accelerator / 2f + 0.5f;
    }
    private void DebugYaw()
    {
        if (!debug) return;
       
        yawLeftBar.fillAmount = LBInput * 0.5f;
        yawRightBar.fillAmount = RBInput * 0.5f;
    }
    private void FixedUpdate()
    {
        {
            if (joystickTrailImageListIndex >= joystickTrailImageList.Count) joystickTrailImageListIndex = 0;
            joystickTrailImageList[joystickTrailImageListIndex].transform.position = rightJoystick.transform.position;
            //if the current joystickTrailImage and the next one are too far away, then set current one closer
            if (joystickTrailImageListIndex > 0 && Vector2.Distance(joystickTrailImageList[joystickTrailImageListIndex].transform.position, joystickTrailImageList[joystickTrailImageListIndex - 1].transform.position) >= 10)
            {
                joystickTrailImageList[joystickTrailImageListIndex].transform.position = joystickTrailImageList[joystickTrailImageListIndex - 1].transform.position + (joystickTrailImageList[joystickTrailImageListIndex].transform.position - joystickTrailImageList[joystickTrailImageListIndex - 1].transform.position).normalized * 15;
            }

            joystickTrailImageListIndex++;
        }
        IEnumerator SetJoystickTrail()
        {
            while (true)
            {
                if (joystickTrailImageListIndex >= joystickTrailImageList.Count) joystickTrailImageListIndex = 0;
                joystickTrailImageList[joystickTrailImageListIndex].transform.position = rightJoystick.transform.position;
                joystickTrailImageListIndex++;
                yield return new WaitForSeconds(0.001f);
            }
        }
    }
}

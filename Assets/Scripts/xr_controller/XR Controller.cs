// --------------------------------------------------------------------------------------------------------------------
//  XRController.cs
//
//  Description:
//  Handles movement of the XR camera in 3D space based on directional commands.
// --------------------------------------------------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class XRController : MonoBehaviour
{
    public GameObject XRCamera;
    public float speed = 1.0f;
    private Vector3 camDir;
    public void moveForward(){
        Debug.Log("Forward");
        camDir = XRCamera.transform.forward;
    }
    public void moveBackward(){
        Debug.Log("Backward");
        camDir = -XRCamera.transform.forward;
    }
    public void moveLeft(){
        Debug.Log("Left");
        camDir = -XRCamera.transform.right;
    }
    public void moveRight(){
        Debug.Log("Right");
        camDir = XRCamera.transform.right;
    }
    public void moveUp(){
        Debug.Log("Up");
        camDir = XRCamera.transform.up;
    }
    public void moveDown(){
        Debug.Log("Down");
        camDir = -XRCamera.transform.up;
    }

    public void buttonUp(){
        camDir = Vector3.zero;
    }

    void Update(){
        if(camDir != Vector3.zero){
            XRCamera.transform.position = XRCamera.transform.position + camDir * speed;
        }
    }
}

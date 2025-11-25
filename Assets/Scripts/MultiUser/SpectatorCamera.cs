/*
 * Copyright (c) 2025 Naval Group
 *
 * This program and the accompanying materials are made available under the
 * terms of the Eclipse Public License 2.0 which is available at
 * https://www.eclipse.org/legal/epl-2.0.
 *
 * SPDX-License-Identifier: EPL-2.0
 */

// --------------------------------------------------------------------------------------------------------------------
//  SpectatorCamera.cs
//
//  Description:
//  Used to control free-fly spectator camera movements in the scene.
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;

public class SpectatorCamera : MonoBehaviour
{
    public float baseMoveSpeed = 10f;
    public float lookSpeed = 2f;
    public float accelerationRate = 5f;
    public float maxShiftMultiplier = 5f;
    private float yaw = 0f;
    private float pitch = 0f;
    private float currentShiftMultiplier = 1f;

    void Update()
    {
        // Mouse rotation
        yaw += lookSpeed * Input.GetAxis("Mouse X");
        pitch -= lookSpeed * Input.GetAxis("Mouse Y");
        pitch = Mathf.Clamp(pitch, -90f, 90f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Shift acceleration
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            currentShiftMultiplier += accelerationRate * Time.deltaTime;
            currentShiftMultiplier = Mathf.Clamp(currentShiftMultiplier, 1f, maxShiftMultiplier);
        }
        else
        {
            currentShiftMultiplier = 1f;
        }

        // Only move when keys are pressed
        Vector3 direction = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) direction += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) direction += Vector3.back;
        if (Input.GetKey(KeyCode.A)) direction += Vector3.left;
        if (Input.GetKey(KeyCode.D)) direction += Vector3.right;
        if (Input.GetKey(KeyCode.E)) direction += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) direction += Vector3.down;

        if (direction != Vector3.zero)
        {
            Vector3 move = transform.TransformDirection(direction.normalized) * baseMoveSpeed * currentShiftMultiplier * Time.deltaTime;
            transform.position += move;
        }
    }
}
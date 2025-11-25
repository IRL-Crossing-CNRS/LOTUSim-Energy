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
//  CameraKnownTargetsNavigator.cs
//  
//  Description:
//  Handles smooth navigation between a predefined list of known scene targets.
//  - Moves the camera smoothly toward each target using configurable offsets.
//  - Performs smooth rotation to face each target.
//  - Smoothly adjusts the camera's field of view (zoom) per target.
//  - Navigation between targets is controlled using the Left and Right arrow keys.
//  
// --------------------------------------------------------------------------------------------------------------------


using System.Collections.Generic;
using UnityEngine;

public class CameraKnownTargetsNavigator : MonoBehaviour
{
    [System.Serializable]
    public class TargetData
    {
        public Transform target;
        public Vector3 offset = new Vector3(0, 5, -10);
        public float zoom = 60f;
    }

    public List<TargetData> targets = new List<TargetData>();
    public float moveSmoothTime = 0.3f;
    public float zoomSmoothTime = 0.5f;
    public float rotationSmoothTime = 0.3f;

    private int currentTargetIndex = 0;
    private Vector3 moveVelocity;
    private float zoomVelocity;
    private Vector3 currentLookDirection;
    private Vector3 lookDirectionVelocity;

    private Camera cam;

    void Start()
    {
        cam = GetComponentInChildren<Camera>();
        if (cam == null)
        {
            Debug.LogError("Aucune caméra trouvée dans les enfants !");
        }
    }

    void Update()
    {
        if (targets.Count == 0) return;

        // Keyboard navigation
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            currentTargetIndex = (currentTargetIndex + 1) % targets.Count;
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            currentTargetIndex = (currentTargetIndex - 1 + targets.Count) % targets.Count;
        }

        TargetData targetData = targets[currentTargetIndex];

        // --- Mouvement fluide ---
        Vector3 targetPosition = targetData.target.position + targetData.offset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref moveVelocity, moveSmoothTime);

        // --- Zoom fluide ---
        if (cam != null)
        {
            cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetData.zoom, ref zoomVelocity, zoomSmoothTime);
        }

        // --- Rotation fluide ---
        Vector3 desiredLookDirection = (targetData.target.position - transform.position).normalized;
        currentLookDirection = Vector3.SmoothDamp(currentLookDirection, desiredLookDirection, ref lookDirectionVelocity, rotationSmoothTime);

        if (currentLookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(currentLookDirection, Vector3.up);
            transform.rotation = targetRotation;
        }
    }
}

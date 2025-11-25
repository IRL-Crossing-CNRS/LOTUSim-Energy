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
//  CameraDynamicTargetsNavigator.cs
//  
//  Description:
//  Dynamically navigates the camera between scene objects by smoothly moving, rotating, and zooming.
//  Supports arrow key navigation (← →) to cycle through targets.
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dynamically navigates between scene objects by smoothly moving, rotating, and zooming the camera.
/// Navigation is controlled using arrow keys (← →).
/// </summary>

[RequireComponent(typeof(Transform))]
public class CameraDynamicTargetsNavigator : MonoBehaviour
{
    [System.Serializable]
    public class TargetData
    {
        public Transform target;
        public Vector3 offset;
        public float zoom;

        public TargetData(Transform target, Vector3 offset, float zoom)
        {
            this.target = target;
            this.offset = offset;
            this.zoom = zoom;
        }
    }

    [Header("Smoothness Settings")]
    [Tooltip("How quickly the camera moves toward the target.")]
    public float moveSmoothTime = 0.3f;

    [Tooltip("How quickly the camera zooms to the target.")]
    public float zoomSmoothTime = 0.5f;

    [Tooltip("How quickly the camera rotates to face the target.")]
    public float rotationSmoothTime = 0.3f;

    [Header("Locking Behavior")]
    [Tooltip("Delay before the camera locks onto a target when motion stabilizes.")]
    public float lockDelay = 1.5f;

    [Header("Ignored Scene Roots")]
    [Tooltip("Root objects to ignore when scanning for camera targets.")]
    public string[] ignoredRootNames = { "Environment", "Canvas", "World Script" };

    private readonly List<TargetData> targets = new();
    private int currentTargetIndex;
    private bool cameraLocked;
    private bool isLocking;
    private float lockTimer;

    private Vector3 moveVelocity;
    private float zoomVelocity;
    private Vector3 currentLookDirection;
    private Vector3 lookDirectionVelocity;

    private Camera cam;

    // -------------------------------------------------------------------------------------

    private void Start()
    {
        cam = GetComponentInChildren<Camera>();

        if (cam == null)
        {
            Debug.LogError("[CameraDynamicTargetsNavigator] No camera found in children!");
            enabled = false;
            return;
        }

        RefreshTargets();
    }

    private void Update()
    {
        if (targets.Count == 0) return;

        HandleInput();

        TargetData targetData = targets[currentTargetIndex];
        Vector3 targetPosition = targetData.target.position + targetData.offset;

        if (!cameraLocked)
        {
            MoveTowardsTarget(targetPosition);
            AdjustZoom(targetData.zoom);
            RotateTowardsTarget(targetData.target);

            HandleLocking(targetPosition, targetData.zoom);
        }
    }

    // -------------------------------------------------------------------------------------
    #region Core Behavior

    /// <summary>
    /// Smoothly moves the camera toward a target position.
    /// </summary>
    private void MoveTowardsTarget(Vector3 targetPosition)
    {
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref moveVelocity,
            moveSmoothTime
        );
    }

    /// <summary>
    /// Smoothly adjusts the camera’s field of view.
    /// </summary>
    private void AdjustZoom(float targetZoom)
    {
        if (cam == null) return;

        cam.fieldOfView = Mathf.SmoothDamp(
            cam.fieldOfView,
            targetZoom,
            ref zoomVelocity,
            zoomSmoothTime
        );
    }

    /// <summary>
    /// Smoothly rotates the camera to look at a target transform.
    /// </summary>
    private void RotateTowardsTarget(Transform target)
    {
        if (target == null) return;

        Vector3 desiredDirection = (target.position - transform.position).normalized;

        currentLookDirection = Vector3.SmoothDamp(
            currentLookDirection,
            desiredDirection,
            ref lookDirectionVelocity,
            rotationSmoothTime
        );

        if (currentLookDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(currentLookDirection, Vector3.up);
        }
    }

    /// <summary>
    /// Handles the gradual locking of the camera once it stabilizes on a target.
    /// </summary>
    private void HandleLocking(Vector3 targetPosition, float targetZoom)
    {
        float positionError = Vector3.Distance(transform.position, targetPosition);
        float zoomError = Mathf.Abs(cam.fieldOfView - targetZoom);

        bool nearTarget = positionError < 1f && zoomError < 1f;

        if (nearTarget)
        {
            if (!isLocking)
            {
                isLocking = true;
                lockTimer = lockDelay;
            }

            lockTimer -= Time.deltaTime;

            if (lockTimer <= 0f)
            {
                cameraLocked = true;
                isLocking = false;
            }
        }
        else
        {
            isLocking = false;
            lockTimer = 0f;
        }
    }

    #endregion
    // -------------------------------------------------------------------------------------

    #region Target Management

    /// <summary>
    /// Refreshes the list of valid scene root targets for the camera to navigate between.
    /// </summary>
    public void RefreshTargets()
    {
        targets.Clear();

        foreach (GameObject rootObj in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!rootObj.activeInHierarchy || IsIgnored(rootObj.name))
                continue;

            Renderer[] renderers = rootObj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;

            // Calculate combined bounds for the entire object
            Bounds combinedBounds = new(renderers[0].bounds.center, Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }

            float maxDimension = Mathf.Max(combinedBounds.size.x, combinedBounds.size.y, combinedBounds.size.z);
            float scaleFactor = Mathf.Max(maxDimension, 0.5f);

            Vector3 offset = new(0, scaleFactor * 0.5f, -scaleFactor * 1.5f);
            float zoom = 60f; // Could be dynamic later if needed

            targets.Add(new TargetData(rootObj.transform, offset, zoom));
        }

        currentTargetIndex = Mathf.Clamp(currentTargetIndex, 0, Mathf.Max(0, targets.Count - 1));
    }

    private bool IsIgnored(string name)
    {
        foreach (string ignored in ignoredRootNames)
        {
            if (ignored == name) return true;
        }
        return false;
    }

    #endregion
    // -------------------------------------------------------------------------------------

    #region Input

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            NextTarget();
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            PreviousTarget();
        }
    }

    private void NextTarget()
    {
        RefreshTargets();
        currentTargetIndex = (currentTargetIndex + 1) % targets.Count;
        ResetLockState();
    }

    private void PreviousTarget()
    {
        currentTargetIndex = (currentTargetIndex - 1 + targets.Count) % targets.Count;
        ResetLockState();
    }

    private void ResetLockState()
    {
        cameraLocked = false;
        isLocking = false;
        lockTimer = 0f;
    }

    #endregion
}

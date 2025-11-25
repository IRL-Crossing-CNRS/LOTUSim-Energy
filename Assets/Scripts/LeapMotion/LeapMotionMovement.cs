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
//  LeapMotionMovement.cs
//
//  Description:
//  Uses Leap Motion hand pose detection to control a CharacterController in 3D space.
//  Supports movement in six directions: forward, back, left, right, up, and down.
// --------------------------------------------------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Leap;

/// <summary>
/// Moves a CharacterController based on hand poses detected by Leap Motion.
/// </summary>
/// <remarks>
/// Assign <see cref="HandPoseDetector"/> components for each movement direction.  
/// Detected poses will move the character at the configured <see cref="speed"/>.
/// </remarks>
[RequireComponent(typeof(CharacterController))]
public class LeapMotionMovement : MonoBehaviour
{
    // ---------------------------------------------------------------------------------
    #region Inspector Fields

    [Header("Hand Pose Detectors")]
    [Tooltip("Pose that triggers forward movement.")]
    public HandPoseDetector moveForwardPose;

    [Tooltip("Pose that triggers backward movement.")]
    public HandPoseDetector moveBackPose;

    [Tooltip("Pose that triggers left movement.")]
    public HandPoseDetector moveLeftPose;

    [Tooltip("Pose that triggers right movement.")]
    public HandPoseDetector moveRightPose;

    [Tooltip("Pose that triggers downward movement.")]
    public HandPoseDetector moveDownPose;

    [Tooltip("Pose that triggers upward movement.")]
    public HandPoseDetector moveUpPose;

    [Header("Movement Settings")]
    [Tooltip("Movement speed in units per second.")]
    public float speed = 2f;

    #endregion
    // ---------------------------------------------------------------------------------

    #region Private Fields

    private CharacterController controller;

    #endregion
    // ---------------------------------------------------------------------------------

    #region Unity Lifecycle

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            Debug.LogError("[LeapMotionMovement] CharacterController component not found. Please add one to this GameObject.");
            enabled = false;
        }
    }

    private void Update()
    {
        Vector3 movement = DetectMovement();

        // Move character controller
        controller.Move(movement * speed * Time.deltaTime);
    }

    #endregion
    // ---------------------------------------------------------------------------------

    #region Core Behavior

    /// <summary>
    /// Checks all assigned hand poses and computes the resulting movement vector.
    /// </summary>
    /// <returns>Movement vector to apply this frame.</returns>
    private Vector3 DetectMovement()
    {
        Vector3 move = Vector3.zero;

        if (moveForwardPose != null && moveForwardPose.GetCurrentlyDetectedPose())
        {
            Debug.Log("[LeapMotionMovement] Detected moveForwardPose.");
            move += transform.forward;
        }
        if (moveBackPose != null && moveBackPose.GetCurrentlyDetectedPose())
        {
            Debug.Log("[LeapMotionMovement] Detected moveBackPose.");
            move -= transform.forward;
        }
        if (moveLeftPose != null && moveLeftPose.GetCurrentlyDetectedPose())
        {
            Debug.Log("[LeapMotionMovement] Detected moveLeftPose.");
            move -= transform.right;
        }
        if (moveRightPose != null && moveRightPose.GetCurrentlyDetectedPose())
        {
            Debug.Log("[LeapMotionMovement] Detected moveRightPose.");
            move += transform.right;
        }
        if (moveDownPose != null && moveDownPose.GetCurrentlyDetectedPose())
        {
            Debug.Log("[LeapMotionMovement] Detected moveDownPose.");
            move += Vector3.down;
        }
        if (moveUpPose != null && moveUpPose.GetCurrentlyDetectedPose())
        {
            Debug.Log("[LeapMotionMovement] Detected moveUpPose.");
            move += Vector3.up;
        }

        return move;
    }

    #endregion
}

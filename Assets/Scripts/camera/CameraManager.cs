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
//  CameraManager.cs
//  
//  Description:
//  Manages switching between multiple sets of Cinemachine virtual cameras in a scene.
//  - Each set (Front, Right, Left) represents a different viewpoint of the same scene target.
//  - Uses arrow key input to cycle through targets.
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using Cinemachine;

/// <summary>
/// Manages switching between multiple sets of Cinemachine virtual cameras.
/// Each camera set (Front, Right, Left) corresponds to different viewpoints for the same scene target.
/// The active set is determined by arrow key input.
/// </summary>
public class CameraManager : MonoBehaviour
{
    [Header("Camera Groups")]
    [Tooltip("Front-facing virtual cameras for each scene target.")]
    public CinemachineVirtualCamera[] virtualCamerasFront;

    [Tooltip("Right-facing virtual cameras for each scene target.")]
    public CinemachineVirtualCamera[] virtualCamerasRight;

    [Tooltip("Left-facing virtual cameras for each scene target.")]
    public CinemachineVirtualCamera[] virtualCamerasLeft;

    [Header("State")]
    [Tooltip("Current index of the active camera set.")]
    public int currentCameraIndex = 0;

    [Tooltip("Priority value assigned to the active camera set.")]
    public int activePriority = 10;

    [Tooltip("Priority value assigned to inactive camera sets.")]
    public int inactivePriority = 0;

    // -------------------------------------------------------------------------------------
    #region Unity Callbacks

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            SwitchToNextCamera();
        }
    }

    #endregion
    // -------------------------------------------------------------------------------------

    #region Camera Switching

    /// <summary>
    /// Switches all three camera groups (Front, Right, Left)
    /// to the next target index and updates their priority.
    /// </summary>
    public void SwitchToNextCamera()
    {
        if (!HasValidCameras()) return;

        // Deactivate current set
        SetCameraPriority(currentCameraIndex, inactivePriority);

        // Move to the next set
        currentCameraIndex = (currentCameraIndex + 1) % virtualCamerasFront.Length;

        // Activate the new set
        SetCameraPriority(currentCameraIndex, activePriority);
    }

    /// <summary>
    /// Sets the Cinemachine camera priorities for all three camera groups at a specific index.
    /// </summary>
    private void SetCameraPriority(int index, int priority)
    {
        if (IsValidIndex(virtualCamerasFront, index))
            virtualCamerasFront[index].Priority = priority;

        if (IsValidIndex(virtualCamerasRight, index))
            virtualCamerasRight[index].Priority = priority;

        if (IsValidIndex(virtualCamerasLeft, index))
            virtualCamerasLeft[index].Priority = priority;
    }

    #endregion
    // -------------------------------------------------------------------------------------

    #region Validation

    private bool HasValidCameras()
    {
        if (virtualCamerasFront == null || virtualCamerasFront.Length == 0)
        {
            Debug.LogWarning("[CameraManager] No front virtual cameras assigned!");
            return false;
        }

        return true;
    }

    private bool IsValidIndex(CinemachineVirtualCamera[] array, int index)
    {
        return array != null && index >= 0 && index < array.Length && array[index] != null;
    }

    #endregion
}

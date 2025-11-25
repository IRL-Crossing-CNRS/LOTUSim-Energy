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
//  FPSLimiter.cs
//  
//  Description:
//  Controls the application's target frame rate to ensure consistent performance.
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;

public class FPSLimiter : MonoBehaviour
{
    // -------------------------------------------------------------------------------------
    #region Inspector Fields

    [Header("Performance Settings")]
    [Tooltip("Desired maximum frames per second (FPS) for the application.")]
    [Range(15, 240)]
    public int targetFrameRate = 60;

    #endregion
    // -------------------------------------------------------------------------------------

    #region Unity Lifecycle

    private void Start()
    {
        // Apply the target frame rate setting
        Application.targetFrameRate = targetFrameRate;
        Debug.Log($"[FPSLimiter] Target frame rate set to {targetFrameRate} FPS.");
    }

    #endregion
}

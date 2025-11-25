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
//  CameraModeSwitcher.cs
//
//  Description:
//  Switches between automatic target-following camera mode and free-fly spectator mode.
//  - Arrow keys (↑ ↓ ← →) enable Auto mode (entity navigation).
//  - Q, W, E, A, S, D keys enable Free-Fly mode (manual movement).
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;

/// <summary>
/// Handles switching between two camera control modes:
/// <list type="bullet">
/// <item><description><b>Auto Mode</b>: Uses <see cref="CameraDynamicTargetsNavigator"/> to follow entities and cycle targets with arrow keys.</description></item>
/// <item><description><b>Free Mode</b>: Uses <see cref="FreeFlyCamera"/> to freely move the camera using QWEASD controls.</description></item>
/// </list>
/// This component automatically toggles between the two modes depending on user input.
/// </summary>
 
[RequireComponent(typeof(Camera))]
public class CameraModeSwitcher : MonoBehaviour
{
    [Header("Camera Modes")]
    [Tooltip("Automatic entity-following camera script.")]
    private CameraDynamicTargetsNavigator autoNavigator;

    [Tooltip("Free-fly spectator camera script.")]
    private FreeFlyCamera freeFly;

    [Header("Debug Settings")]
    [Tooltip("Whether to log camera mode switches to the console.")]
    public bool enableDebugLogs = true;

    // -------------------------------------------------------------------------------------
    #region Unity Callbacks

    private void Start()
    {
        // Cache both camera control scripts attached to the same GameObject
        autoNavigator = GetComponent<CameraDynamicTargetsNavigator>();
        freeFly = GetComponent<FreeFlyCamera>();

        // Start with Auto mode enabled by default
        SetModeAuto();
    }

    private void Update()
    {
        HandleInput();
    }

    #endregion
    // -------------------------------------------------------------------------------------

    #region Input Handling

    /// <summary>
    /// Checks for input and switches between camera modes accordingly.
    /// </summary>
    private void HandleInput()
    {
        if (IsArrowKeyPressed())
        {
            SetModeAuto();
        }
        else if (IsFreeFlyKeyPressed())
        {
            SetModeFree();
        }
    }

    /// <summary>
    /// Returns true if any of the arrow keys are pressed.
    /// </summary>
    private bool IsArrowKeyPressed()
    {
        return Input.GetKeyDown(KeyCode.LeftArrow) ||
               Input.GetKeyDown(KeyCode.RightArrow) ||
               Input.GetKeyDown(KeyCode.UpArrow) ||
               Input.GetKeyDown(KeyCode.DownArrow);
    }

    /// <summary>
    /// Returns true if any of the free-fly movement keys (Q, W, E, A, S, D) are pressed.
    /// </summary>
    private bool IsFreeFlyKeyPressed()
    {
        return Input.GetKeyDown(KeyCode.W) ||
               Input.GetKeyDown(KeyCode.A) ||
               Input.GetKeyDown(KeyCode.S) ||
               Input.GetKeyDown(KeyCode.D) ||
               Input.GetKeyDown(KeyCode.Q) ||
               Input.GetKeyDown(KeyCode.E);
    }

    #endregion
    // -------------------------------------------------------------------------------------

    #region Mode Switching

    /// <summary>
    /// Enables automatic navigation mode and disables free-fly mode.
    /// </summary>
    public void SetModeAuto()
    {
        if (autoNavigator != null)
            autoNavigator.enabled = true;

        if (freeFly != null)
            freeFly.enabled = false;

        if (enableDebugLogs)
            Debug.Log("[CameraModeSwitcher] Switched to AUTO (entity-follow) mode");
    }

    /// <summary>
    /// Enables free-fly spectator mode and disables automatic navigation.
    /// </summary>
    public void SetModeFree()
    {
        if (autoNavigator != null)
            autoNavigator.enabled = false;

        if (freeFly != null)
            freeFly.enabled = true;

        if (enableDebugLogs)
            Debug.Log("[CameraModeSwitcher] Switched to FREE (spectator) mode");
    }

    #endregion
}

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
//  InputLockManager.cs
//
//  Description:
//  Handles cursor locking/unlocking and enables or disables camera control accordingly.
//  Useful for toggling between gameplay (mouse locked) and UI interaction (mouse free).
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Manages the mouse cursor lock state and toggles camera look control depending on UI focus and user input.
/// </summary>
/// <remarks>
/// - Press <see cref="KeyCode.Escape"/> to toggle mouse lock.<br/>
/// - When the mouse is over a UI element, camera look is temporarily disabled.<br/>
/// - Attach this script to a GameObject that also references your camera control script.
/// </remarks>
public class InputLockManager : MonoBehaviour
{
    // -------------------------------------------------------------------------------------
    #region Inspector Fields

    [Header("References")]
    [Tooltip("Reference to the camera control script (e.g., a mouse look or free-fly script).")]
    public MonoBehaviour cameraLookScript;

    [Header("Settings")]
    [Tooltip("Determines whether the mouse is locked at startup.")]
    public bool lockMouseOnStart = true;

    #endregion
    // -------------------------------------------------------------------------------------

    #region Private Fields

    private bool isMouseLocked = false;

    #endregion
    // -------------------------------------------------------------------------------------

    #region Unity Lifecycle

    private void Start()
    {
        if (cameraLookScript == null)
        {
            Debug.LogWarning("[InputLockManager] No camera look script assigned — please assign one in the Inspector.");
        }

        if (lockMouseOnStart)
        {
            LockCursor();
        }
        else
        {
            UnlockCursor();
        }
    }

    private void Update()
    {
        HandleInput();
        UpdateCameraLookState();
    }

    #endregion
    // -------------------------------------------------------------------------------------

    #region Core Behavior

    /// <summary>
    /// Handles user input for toggling cursor lock state.
    /// </summary>
    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isMouseLocked)
                UnlockCursor();
            else
                LockCursor();
        }
    }

    /// <summary>
    /// Updates whether the camera look script should be active,
    /// based on whether the pointer is interacting with UI elements.
    /// </summary>
    private void UpdateCameraLookState()
    {
        if (cameraLookScript == null)
            return;

        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (pointerOverUI)
        {
            cameraLookScript.enabled = false;
        }
        else
        {
            cameraLookScript.enabled = isMouseLocked;
        }
    }

    /// <summary>
    /// Locks the mouse cursor to the center of the screen and hides it.
    /// </summary>
    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        isMouseLocked = true;
        Debug.Log("[InputLockManager] Mouse locked.");
    }

    /// <summary>
    /// Unlocks the mouse cursor and makes it visible.
    /// </summary>
    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isMouseLocked = false;
        Debug.Log("[InputLockManager] Mouse unlocked.");
    }

    #endregion
}

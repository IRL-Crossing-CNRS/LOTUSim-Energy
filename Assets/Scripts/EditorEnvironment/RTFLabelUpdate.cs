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
//  RTFLabelUpdate.cs
//
//  Description:
//  Displays the Real-Time Factor (RTF) from the ROS simulation as a percentage on a TMP label.
//  Toggles visibility with the 'L' key.
// --------------------------------------------------------------------------------------------------------------------

using System;
using UnityEngine;
using TMPro;
using RosMessageTypes.Lotusim;

namespace Lotusim
{
    /// <summary>
    /// Displays the simulation Real-Time Factor (RTF) value received from the ROS interface
    /// and updates a <see cref="TMP_Text"/> label in real time.
    /// </summary>
    /// <remarks>
    /// - Press **L** to toggle label visibility.<br/>
    /// - Automatically hides when ROS is disconnected.<br/>
    /// - Requires a <see cref="RosInterface"/> singleton to be initialized.
    /// </remarks>
    public class RTFLabelUpdate : MonoBehaviour
    {
        // ---------------------------------------------------------------------------------
        #region Inspector Fields

        [Header("UI Display")]
        [Tooltip("TextMeshPro UI element used to display the Real-Time Factor (RTF).")]
        [SerializeField] private TMP_Text statsLabel;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Private Fields

        private RosInterface rosInterface;
        private bool labelVisible = true;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Unity Lifecycle

        private void Awake()
        {
            rosInterface = RosInterface.Instance;
            if (rosInterface == null)
            {
                Debug.LogError("[RTFLabelController] RosInterface instance is null! Disabling script.");
                enabled = false;
            }
        }

        private void Start()
        {
            if (statsLabel == null)
            {
                Debug.LogError("[RTFLabelController] Stats label not assigned! Please link a TMP_Text component.");
                enabled = false;
            }
        }

        private void Update()
        {
            HandleInput();

            if (!labelVisible || rosInterface == null || !rosInterface.IsConnected)
                return;

            UpdateRTFLabel();
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Core Behavior

        /// <summary>
        /// Handles user input to toggle label visibility.
        /// </summary>
        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                labelVisible = !labelVisible;
                if (statsLabel != null)
                    statsLabel.enabled = labelVisible;

                Debug.Log($"[RTFLabelController] Label visibility: {(labelVisible ? "Visible" : "Hidden")}");
            }
        }

        /// <summary>
        /// Retrieves the latest RTF value from the <see cref="RosInterface"/> and updates the label text.
        /// </summary>
        private void UpdateRTFLabel()
        {
            SimStatsMsg msg = rosInterface.GetSimStats();
            if (msg == null)
                return;

            double rtfPercent = msg.real_time_factor * 100f;
            statsLabel.text = $"RTF: {rtfPercent:F2}%";
        }

        #endregion
    }
}

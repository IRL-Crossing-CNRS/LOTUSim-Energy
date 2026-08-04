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
//  OceanCurrentVisualizer.cs
//
//  Description:
//  Visualizes the ocean current as a grid of LineRenderer arrows below sea level. Current is a
//  single latched 2-axis (ENU x/y) vector for the whole scene — unlike wind, there is no vertical
//  component and, for now, no spatially-varying regions.
//
//  Setup:
//  1. Create an empty GameObject in the scene and add this component.
//  2. RosInterface dispatches received OceanCurrentMsg messages here automatically (see
//     RosInterface.OnOceanCurrentReceived / ProcessOceanCurrent) — no manual wiring needed.
//  3. Optionally adjust Arrow Grid Settings and Visual Settings in the Inspector.
// --------------------------------------------------------------------------------------------------------------------

using RosMessageTypes.Lotusim;
using UnityEngine;

namespace Lotusim
{
    /// <summary>
    /// Renders an ocean current visualization as a grid of LineRenderer arrows below sea level.
    /// </summary>
    public class OceanCurrentVisualizer : MonoBehaviour
    {
        // ---------------------------------------------------------------------------------
        #region Inspector Fields

        [Header("Auto-Fit")]
        [Tooltip("Automatically center this GameObject on all WindTurbineControllers in the scene.")]
        [SerializeField] private bool autoFitToBounds = true;

        [Header("Arrow Grid Settings")]
        [Tooltip("Show arrow grid overlay.")]
        [SerializeField] private bool showArrows = true;

        [Tooltip("Number of arrows along X and Z axes.")]
        [SerializeField] private Vector2Int arrowGridSize = new Vector2Int(6, 6);

        [Tooltip("Spacing between arrows in world units.")]
        [SerializeField] private float arrowSpacing = 100f;

        [Tooltip("Depth (Y) at which arrows are drawn below sea level (negative = underwater).")]
        [SerializeField] private float arrowDepth = -20f;

        [Tooltip("Base length of an arrow at current speed = 1 m/s.")]
        [SerializeField] private float arrowLengthScale = 4f;

        [Tooltip("Maximum arrow length in world units — prevents arrows overlapping at high current.")]
        [SerializeField] private float maxArrowLength = 50f;

        [Tooltip("Minimum line width of arrows (no current).")]
        [SerializeField] private float arrowLineWidthMin = 0.5f;

        [Tooltip("Maximum line width of arrows (full current).")]
        [SerializeField] private float arrowLineWidthMax = 4f;

        [Header("Visual Settings")]
        [Tooltip("Color of arrows at low current speed.")]
        [SerializeField] private Color colorLow = new Color(0.2f, 0.6f, 0.9f, 0.4f);

        [Tooltip("Color of arrows at high current speed.")]
        [SerializeField] private Color colorHigh = new Color(0.1f, 0.2f, 0.8f, 0.8f);

        [Tooltip("Current speed at which color is fully shifted to colorHigh.")]
        [SerializeField] private float maxCurrentSpeedForColor = 3f;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Private Fields

        private WindArrowFieldRenderer m_arrowField;

        private Vector3 m_currentVector = Vector3.zero;
        private Vector3 m_lastCurrentVector = new Vector3(float.NaN, float.NaN, float.NaN);
        private float m_currentMagnitude = 0f;

        /// <summary>Runtime visibility state (toggled with <see cref="KeyBindings.ToggleOceanCurrentField"/>).</summary>
        private bool m_isVisible;

        /// <summary>
        /// Mirrors the latest message's enable_current flag — this is the sole source of truth for
        /// whether the field should be shown, since the topic is latched and, in practice, only
        /// ever published once at scenario start plus once (enable_current=false) at teardown. No
        /// magnitude/timeout heuristics: generic_scenario explicitly tells us when to hide.
        /// </summary>
        private bool m_currentEnabled;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Unity Lifecycle

        private void Awake()
        {
            if (autoFitToBounds) AutoFitToTurbines();

            m_arrowField = new WindArrowFieldRenderer(
                transform, "OceanCurrentArrows",
                arrowLengthScale, maxArrowLength,
                arrowLineWidthMin, arrowLineWidthMax,
                maxCurrentSpeedForColor);

            // Always build the grid so toggling ON is instant.
            m_arrowField.BuildGrid(arrowGridSize, new Vector2(arrowSpacing, arrowSpacing), transform.position, arrowDepth);

            // Honour the Inspector default for initial visibility, but stay hidden until the
            // first message arrives with enable_current=true — nothing to show before that anyway.
            m_isVisible = showArrows;
            SetFieldVisible(false);
        }

        private void Update()
        {
            // Toggle visibility with the bound key (default: Alpha6).
            if (Input.GetKeyDown(KeyBindings.ToggleOceanCurrentField))
            {
                m_isVisible = !m_isVisible;
                SetFieldVisible(m_isVisible && m_currentEnabled);

                // Force update on visibility toggle
                if (m_isVisible)
                {
                    m_lastCurrentVector = new Vector3(float.NaN, float.NaN, float.NaN);
                }
            }

            if (!m_isVisible || !m_currentEnabled) return;

            if (m_currentVector != m_lastCurrentVector)
            {
                UpdateArrows();
                m_lastCurrentVector = m_currentVector;
            }
        }

        private void OnDestroy()
        {
            m_arrowField?.Dispose();
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Ocean Current Data

        /// <summary>
        /// Called by <see cref="RosInterface"/> whenever a new ocean current message is received.
        /// Only x/y (ENU east/west, north/south) of linear_velocity are used — current has no
        /// vertical component. enable_current is the scenario's explicit on/off signal (false on
        /// scenario teardown), and is the only thing that hides the field — a genuinely zero
        /// linear_velocity with enable_current=true is left to WindArrowFieldRenderer's own
        /// zero-magnitude snapping rather than being treated as "off".
        /// </summary>
        public void OnOceanCurrentReceived(OceanCurrentMsg msg)
        {
            m_currentVector = new Vector3((float)msg.linear_velocity.x, (float)msg.linear_velocity.y, 0f);
            m_currentMagnitude = m_currentVector.magnitude;

            if (msg.enable_current != m_currentEnabled)
            {
                m_currentEnabled = msg.enable_current;
                SetFieldVisible(m_isVisible && m_currentEnabled);
            }
        }

        /// <summary>
        /// Centers this GameObject on the XZ midpoint of all WindTurbineController instances.
        /// </summary>
        private void AutoFitToTurbines()
        {
            WindTurbineController[] turbines = FindObjectsOfType<WindTurbineController>();

            if (turbines == null || turbines.Length == 0)
            {
                Debug.LogWarning("[OceanCurrentVisualizer] AutoFit: no WindTurbineController found in scene.");
                return;
            }

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (WindTurbineController t in turbines)
            {
                Vector3 pos = t.transform.position;
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.z < minZ) minZ = pos.z;
                if (pos.z > maxZ) maxZ = pos.z;
            }

            float centerX = (minX + maxX) * 0.5f;
            float centerZ = (minZ + maxZ) * 0.5f;
            transform.position = new Vector3(centerX, transform.position.y, centerZ);

            Debug.Log($"[OceanCurrentVisualizer] AutoFit: {turbines.Length} turbines — " +
                      $"center ({centerX:F1}, {centerZ:F1}).");
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Arrow Grid

        private void UpdateArrows()
        {
            float t = Mathf.Clamp01(m_currentMagnitude / maxCurrentSpeedForColor);
            Color arrowColor = Color.Lerp(colorLow, colorHigh, t);
            arrowColor.a = Mathf.Lerp(0.3f, 1f, t);

            // ENU -> Unity: current X -> world X, current Y (north/south) -> world Z. No vertical component.
            Vector3 currentDir = new Vector3(m_currentVector.x, 0f, m_currentVector.y);

            m_arrowField.UpdateArrows(currentDir, m_currentMagnitude, arrowColor);
        }

        /// <summary>
        /// Activates or deactivates the entire arrow grid.
        /// </summary>
        private void SetFieldVisible(bool visible)
        {
            m_arrowField?.SetVisible(visible);
        }

        #endregion
    }
}

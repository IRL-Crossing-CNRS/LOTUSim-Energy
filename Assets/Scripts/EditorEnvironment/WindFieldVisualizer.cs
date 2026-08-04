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
//  WindFieldVisualizer.cs
//
//  Description:
//  Visualizes the wind field as a grid of LineRenderer arrows. Each arrow shows the wind
//  direction; length and width scale with magnitude.
//
//  Setup:
//  1. Create an empty GameObject in the scene and add this component.
//  2. Assign 'windController' to the WindSliderController in the scene.
//  3. Optionally adjust Arrow Grid Settings and Visual Settings in the Inspector.
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;

namespace Lotusim
{
    /// <summary>
    /// Renders a wind field visualization as a grid of LineRenderer arrows.
    /// </summary>
    public class WindFieldVisualizer : MonoBehaviour
    {
        // ---------------------------------------------------------------------------------
        #region Inspector Fields

        [Header("Data Source")]
        [Tooltip("Reference to WindSliderController to read the current wind vector.")]
        [SerializeField] private WindSliderController windController;

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

        [Tooltip("Height (Y) at which arrows are drawn above sea level.")]
        [SerializeField] private float arrowHeight = 40f;

        [Tooltip("Base length of an arrow at wind speed = 1 m/s.")]
        [SerializeField] private float arrowLengthScale = 4f;

        [Tooltip("Maximum arrow length in world units — prevents arrows overlapping at high wind.")]
        [SerializeField] private float maxArrowLength = 50f;

        [Tooltip("Minimum line width of arrows (no wind).")]
        [SerializeField] private float arrowLineWidthMin = 0.5f;

        [Tooltip("Maximum line width of arrows (full wind).")]
        [SerializeField] private float arrowLineWidthMax = 4f;

        [Header("Visual Settings")]
        [Tooltip("Color of arrows at low wind speed.")]
        [SerializeField] private Color colorLow = new Color(0.4f, 0.8f, 1f, 0.4f);

        [Tooltip("Color of arrows at high wind speed.")]
        [SerializeField] private Color colorHigh = new Color(1f, 0.4f, 0.2f, 0.8f);

        [Tooltip("Wind speed at which color is fully shifted to colorHigh.")]
        [SerializeField] private float maxWindSpeedForColor = 20f;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Private Fields

        private WindArrowFieldRenderer m_arrowField;

        private Vector3 m_windVector = Vector3.zero;
        private Vector3 m_lastWindVector = new Vector3(float.NaN, float.NaN, float.NaN);
        private float m_windMagnitude = 0f;

        /// <summary>Runtime visibility state (toggled with <see cref="KeyBindings.ToggleWindField"/>).</summary>
        private bool m_isVisible;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Unity Lifecycle

        private void Awake()
        {
            if (autoFitToBounds) AutoFitToTurbines();

            m_arrowField = new WindArrowFieldRenderer(
                transform, "WindArrows",
                arrowLengthScale, maxArrowLength,
                arrowLineWidthMin, arrowLineWidthMax,
                maxWindSpeedForColor);

            // Always build the grid so toggling ON is instant.
            m_arrowField.BuildGrid(arrowGridSize, new Vector2(arrowSpacing, arrowSpacing), transform.position, arrowHeight);

            // Honour the Inspector default for initial visibility.
            m_isVisible = showArrows;
            SetFieldVisible(m_isVisible);
        }

        private void Update()
        {
            // Toggle visibility with the bound key (default: Alpha9).
            if (Input.GetKeyDown(KeyBindings.ToggleWindField))
            {
                m_isVisible = !m_isVisible;
                SetFieldVisible(m_isVisible);
                
                // Force update on visibility toggle
                if (m_isVisible)
                {
                    m_lastWindVector = new Vector3(float.NaN, float.NaN, float.NaN);
                }
            }

            if (!m_isVisible) return;

            ReadWindVector();

            if (m_windVector != m_lastWindVector)
            {
                UpdateArrows();
                m_lastWindVector = m_windVector;
            }
        }

        private void OnDestroy()
        {
            m_arrowField?.Dispose();
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Wind Data

        private void ReadWindVector()
        {
            if (windController != null)
            {
                m_windVector = windController.CurrentWindVector;
            }
            // Wind vector from sliders is ENU: X = east/west, Y = north/south, Z = up/down (height).
            // In Unity (Y-up): slider X -> world X, slider Z -> world Y (height), slider Y -> world Z (forward).
            m_windMagnitude = m_windVector.magnitude;
        }

        /// <summary>
        /// Centers this GameObject on the XZ midpoint of all WindTurbineController instances.
        /// </summary>
        private void AutoFitToTurbines()
        {
            WindTurbineController[] turbines = FindObjectsOfType<WindTurbineController>();

            if (turbines == null || turbines.Length == 0)
            {
                Debug.LogWarning("[WindFieldVisualizer] AutoFit: no WindTurbineController found in scene.");
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

            Debug.Log($"[WindFieldVisualizer] AutoFit: {turbines.Length} turbines — " +
                      $"center ({centerX:F1}, {centerZ:F1}).");
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Arrow Grid

        private void UpdateArrows()
        {
            float t = Mathf.Clamp01(m_windMagnitude / maxWindSpeedForColor);
            Color arrowColor = Color.Lerp(colorLow, colorHigh, t);
            arrowColor.a = Mathf.Lerp(0.3f, 1f, t);

            // ENU -> Unity: slider X -> world X, slider Z (up/down) -> world Y, slider Y (north/south) -> world Z.
            Vector3 windDir = new Vector3(m_windVector.x, m_windVector.z, m_windVector.y);

            m_arrowField.UpdateArrows(windDir, m_windMagnitude, arrowColor);
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

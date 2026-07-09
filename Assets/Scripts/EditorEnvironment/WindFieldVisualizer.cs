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

using System.Collections.Generic;
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

        private readonly List<LineRenderer> m_arrowLines = new List<LineRenderer>();
        private GameObject m_arrowRoot;
        private Material m_sharedMaterial;

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
            Shader shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Color");
            m_sharedMaterial = new Material(shader) { color = colorLow };

            if (autoFitToBounds) AutoFitToTurbines();

            // Always build the grid so toggling ON is instant.
            BuildArrowGrid();

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
            if (m_sharedMaterial != null)
                Destroy(m_sharedMaterial);

            if (m_arrowRoot != null)
                Destroy(m_arrowRoot);
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
            // Wind vector from sliders: X = east/west, Z = north/south (ROS/Gazebo XZ plane).
            // In Unity (Y-up): slider X -> world X, slider Z -> world Z (forward).
            m_windMagnitude = new Vector3(m_windVector.x, 0f, m_windVector.z).magnitude;
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

        private void BuildArrowGrid()
        {
            m_arrowRoot = new GameObject("WindArrows");
            m_arrowRoot.transform.SetParent(transform);
            
            int layerIndex = LayerMask.NameToLayer("Trajectories");
            if (layerIndex > -1) m_arrowRoot.layer = layerIndex;

            int cols = arrowGridSize.x;
            int rows = arrowGridSize.y;
            float offsetX = (cols - 1) * arrowSpacing * 0.5f;
            float offsetZ = (rows - 1) * arrowSpacing * 0.5f;

            for (int x = 0; x < cols; x++)
            {
                for (int z = 0; z < rows; z++)
                {
                    Vector3 origin = transform.position + new Vector3(
                        x * arrowSpacing - offsetX,
                        arrowHeight,
                        z * arrowSpacing - offsetZ
                    );

                    // Two LineRenderers per arrow:
                    //   shaft — constant-width segment from origin to the neck
                    //   head  — wide-to-zero taper from neck to tip → filled triangle arrowhead
                    LineRenderer shaft = CreateLine($"Arrow_{x}_{z}_shaft", 2);
                    m_arrowLines.Add(shaft);

                    LineRenderer head = CreateLine($"Arrow_{x}_{z}_head", 2);
                    m_arrowLines.Add(head);

                    shaft.SetPosition(0, origin);
                    shaft.SetPosition(1, origin);
                    head.SetPosition(0, origin);
                    head.SetPosition(1, origin);
                }
            }
        }

        private void UpdateArrows()
        {
            if (m_arrowLines.Count == 0) return;

            float t = Mathf.Clamp01(m_windMagnitude / maxWindSpeedForColor);
            Color arrowColor = Color.Lerp(colorLow, colorHigh, t);
            arrowColor.a = Mathf.Lerp(0.3f, 1f, t);

            Vector3 windDir = m_windMagnitude > 0.01f
                ? new Vector3(m_windVector.x, 0f, m_windVector.z).normalized
                : Vector3.forward;

            float rawLength = Mathf.Sqrt(m_windMagnitude) * arrowLengthScale;
            float arrowLength = Mathf.Min(rawLength, maxArrowLength);

            float lineWidth = Mathf.Lerp(arrowLineWidthMin, arrowLineWidthMax, t);

            // Arrowhead is the last 30% of the arrow; its base is ~2.4x the shaft width
            // so it visibly flares out before tapering to the tip.
            const float kHeadFraction = 0.3f;
            const float kHeadFlare    = 2.4f;
            float headLen  = arrowLength * kHeadFraction;
            float shaftLen = arrowLength - headLen;

            if (m_sharedMaterial != null)
            {
                m_sharedMaterial.color = arrowColor;
            }

            for (int i = 0; i < m_arrowLines.Count; i += 2)
            {
                if (i + 1 >= m_arrowLines.Count) break;

                LineRenderer shaft = m_arrowLines[i];
                LineRenderer head  = m_arrowLines[i + 1];

                Vector3 origin = shaft.GetPosition(0);
                Vector3 neck   = origin + windDir * shaftLen;
                Vector3 tip    = origin + windDir * arrowLength;

                // Shaft: constant width from origin to neck
                shaft.startWidth = lineWidth;
                shaft.endWidth   = lineWidth;
                shaft.SetPosition(1, neck);
                shaft.startColor = arrowColor;
                shaft.endColor   = arrowColor;

                // Head: wide flare at the neck, tapers to a point — filled triangle
                head.startWidth = lineWidth * kHeadFlare;
                head.endWidth   = 0f;
                head.SetPosition(0, neck);
                head.SetPosition(1, tip);
                head.startColor = arrowColor;
                head.endColor   = arrowColor;
            }
        }

        private LineRenderer CreateLine(string goName, int pointCount)
        {
            GameObject go = new GameObject(goName);
            go.transform.SetParent(m_arrowRoot.transform);
            
            int layerIndex = LayerMask.NameToLayer("Trajectories");
            if (layerIndex > -1) go.layer = layerIndex;

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = pointCount;
            lr.startWidth = arrowLineWidthMin;
            lr.endWidth = arrowLineWidthMin;
            lr.useWorldSpace = true;

            lr.sharedMaterial = m_sharedMaterial;

            return lr;
        }

        /// <summary>
        /// Activates or deactivates the entire arrow grid.
        /// </summary>
        private void SetFieldVisible(bool visible)
        {
            if (m_arrowRoot != null)
                m_arrowRoot.SetActive(visible);
        }

        #endregion
    }
}

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
//  WindArrowFieldRenderer.cs
//
//  Description:
//  Reusable grid-of-arrows renderer for a UNIFORM wind vector over a rectangular area
//  (one direction + magnitude shared by every arrow in the grid). Originally embedded in
//  WindFieldVisualizer; extracted so both the global wind field and per-zone WindRegion
//  boxes (see WindRegionVisualizer) can share the exact same arrow visuals.
//
//  Not a MonoBehaviour: instantiate one per field/zone, call BuildGrid() once (or whenever
//  the covered area changes), then UpdateArrows() whenever the vector or color changes.
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Lotusim
{
    /// <summary>
    /// Renders a grid of arrow-shaped LineRenderers over a rectangular XZ area, all sharing
    /// one direction/magnitude (i.e. one uniform vector field). Arrow length and width scale
    /// with magnitude; color is supplied by the caller so speed-gradients, per-zone tints,
    /// etc. can be layered on top without this class knowing about them.
    /// </summary>
    public class WindArrowFieldRenderer
    {
        // Arrowhead is the last 30% of the arrow; its base is ~2.4x the shaft width so it
        // visibly flares out before tapering to the tip.
        private const float kHeadFraction = 0.3f;
        private const float kHeadFlare = 2.4f;

        // Below this wind speed (m/s), treat the vector as exactly zero — see UpdateArrows().
        private const float kZeroMagnitudeEpsilon = 0.01f;

        private readonly List<LineRenderer> m_arrowLines = new List<LineRenderer>();
        private readonly GameObject m_root;
        private readonly Material m_material;

        private readonly float m_arrowLengthScale;
        private readonly float m_maxArrowLength;
        private readonly float m_lineWidthMin;
        private readonly float m_lineWidthMax;
        private readonly float m_maxSpeedForScale;

        private Vector2Int m_gridSize;
        private Vector2 m_spacing;
        private Vector3 m_centerWorldPos;
        private float m_height;
        private bool m_hasGrid;

        // Origins used by the last BuildFromPoints() call (arbitrary-layout path — e.g. arrows
        // strung along a cone segment's centerline instead of a rectangular grid).
        private List<Vector3> m_lastPoints;

        /// <param name="parent">Transform the arrow grid root is parented under.</param>
        /// <param name="rootName">Name of the created root GameObject (for hierarchy readability).</param>
        /// <param name="arrowLengthScale">Base arrow length at wind speed = 1 m/s (length grows with sqrt(speed)).</param>
        /// <param name="maxArrowLength">Clamp on arrow length so arrows don't overlap at high wind.</param>
        /// <param name="lineWidthMin">Shaft width at zero wind.</param>
        /// <param name="lineWidthMax">Shaft width at/above <paramref name="maxSpeedForScale"/>.</param>
        /// <param name="maxSpeedForScale">Wind speed (m/s) at which width interpolation saturates.</param>
        /// <param name="layerName">Unity layer for the generated line objects; ignored if not found.</param>
        public WindArrowFieldRenderer(
            Transform parent,
            string rootName,
            float arrowLengthScale,
            float maxArrowLength,
            float lineWidthMin,
            float lineWidthMax,
            float maxSpeedForScale,
            string layerName = "Trajectories")
        {
            m_arrowLengthScale = arrowLengthScale;
            m_maxArrowLength = maxArrowLength;
            m_lineWidthMin = lineWidthMin;
            m_lineWidthMax = lineWidthMax;
            m_maxSpeedForScale = Mathf.Max(0.01f, maxSpeedForScale);

            m_root = new GameObject(rootName);
            m_root.transform.SetParent(parent, false);

            int layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex > -1) m_root.layer = layerIndex;

            Shader shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Color");
            m_material = new Material(shader);
            WindVisualUtils.ConfigureTransparent(m_material, Color.white);
        }

        /// <summary>Root GameObject of this arrow field — parent it, move it, or read its transform.</summary>
        public GameObject Root => m_root;

        /// <summary>
        /// (Re)builds the arrow grid covering a rectangular area centered on <paramref name="centerWorldPos"/>.
        /// Cheap to skip: callers should only invoke this when the covered area actually changes
        /// (grid resolution, spacing, center or height), not on every vector update.
        /// </summary>
        public void BuildGrid(Vector2Int gridSize, Vector2 spacing, Vector3 centerWorldPos, float height)
        {
            m_gridSize = new Vector2Int(Mathf.Max(1, gridSize.x), Mathf.Max(1, gridSize.y));
            m_spacing = spacing;
            m_centerWorldPos = centerWorldPos;
            m_height = height;
            m_hasGrid = true;

            int cols = m_gridSize.x;
            int rows = m_gridSize.y;
            float offsetX = (cols - 1) * spacing.x * 0.5f;
            float offsetZ = (rows - 1) * spacing.y * 0.5f;

            List<Vector3> origins = new List<Vector3>(cols * rows);
            for (int x = 0; x < cols; x++)
            {
                for (int z = 0; z < rows; z++)
                {
                    origins.Add(centerWorldPos + new Vector3(
                        x * spacing.x - offsetX,
                        height,
                        z * spacing.y - offsetZ
                    ));
                }
            }

            BuildFromPoints(origins);
        }

        /// <summary>True if the grid parameters differ from the last BuildGrid() call — callers
        /// use this to decide whether a rebuild is actually needed.</summary>
        public bool GridMatches(Vector2Int gridSize, Vector2 spacing, Vector3 centerWorldPos, float height)
        {
            return m_hasGrid
                && m_gridSize == gridSize
                && m_spacing == spacing
                && (m_centerWorldPos - centerWorldPos).sqrMagnitude < 0.0001f
                && Mathf.Approximately(m_height, height);
        }

        /// <summary>
        /// (Re)builds the arrow field at an arbitrary set of world-space origins — for layouts a
        /// rectangular grid can't express, e.g. arrows strung along a cone segment's centerline.
        /// BuildGrid() is just a rectangular-grid convenience wrapper around this.
        /// </summary>
        public void BuildFromPoints(IReadOnlyList<Vector3> origins)
        {
            ClearGrid();

            m_hasGrid = true;
            m_lastPoints = new List<Vector3>(origins);

            for (int i = 0; i < origins.Count; i++)
            {
                Vector3 origin = origins[i];

                LineRenderer shaft = CreateLine($"Arrow_{i}_shaft");
                LineRenderer head = CreateLine($"Arrow_{i}_head");

                shaft.SetPosition(0, origin);
                shaft.SetPosition(1, origin);
                head.SetPosition(0, origin);
                head.SetPosition(1, origin);

                m_arrowLines.Add(shaft);
                m_arrowLines.Add(head);
            }
        }

        /// <summary>True if 'origins' differs from the last BuildFromPoints() call — callers use
        /// this to decide whether a rebuild is actually needed.</summary>
        public bool PointsMatch(IReadOnlyList<Vector3> origins)
        {
            if (m_lastPoints == null || m_lastPoints.Count != origins.Count) return false;

            for (int i = 0; i < origins.Count; i++)
            {
                if ((m_lastPoints[i] - origins[i]).sqrMagnitude > 0.0001f) return false;
            }

            return true;
        }

        /// <summary>
        /// Updates every arrow in the grid to point along <paramref name="windDirWorld"/> (full
        /// 3D direction — a nonzero Y tilts arrows up/down, e.g. for a vertical wind component)
        /// with length/width driven by <paramref name="magnitude"/>, tinted with <paramref name="color"/>.
        /// </summary>
        public void UpdateArrows(Vector3 windDirWorld, float magnitude, Color color)
        {
            if (m_arrowLines.Count == 0) return;

            // Snap near-zero magnitudes to exactly zero. Some Gazebo wind publishers keep a
            // tiny non-zero epsilon alive on purpose as a "don't let the plugin sleep" workaround
            // (see WindSliderController.PublishWind) instead of a clean 0 — without this, that
            // epsilon would otherwise survive sqrt() and render as a stray near-invisible arrow.
            if (magnitude < kZeroMagnitudeEpsilon) magnitude = 0f;

            float t = Mathf.Clamp01(magnitude / m_maxSpeedForScale);

            Vector3 dir = magnitude > 0f && windDirWorld.sqrMagnitude > 0.0001f
                ? windDirWorld.normalized
                : Vector3.forward;

            float rawLength = Mathf.Sqrt(magnitude) * m_arrowLengthScale;
            float arrowLength = Mathf.Min(rawLength, m_maxArrowLength);
            float lineWidth = Mathf.Lerp(m_lineWidthMin, m_lineWidthMax, t);

            float headLen = arrowLength * kHeadFraction;
            float shaftLen = arrowLength - headLen;

            WindVisualUtils.SetColor(m_material, color);

            for (int i = 0; i + 1 < m_arrowLines.Count; i += 2)
            {
                LineRenderer shaft = m_arrowLines[i];
                LineRenderer head = m_arrowLines[i + 1];

                Vector3 origin = shaft.GetPosition(0);
                Vector3 neck = origin + dir * shaftLen;
                Vector3 tip = origin + dir * arrowLength;

                shaft.startWidth = lineWidth;
                shaft.endWidth = lineWidth;
                shaft.SetPosition(1, neck);
                shaft.startColor = color;
                shaft.endColor = color;

                head.startWidth = lineWidth * kHeadFlare;
                head.endWidth = 0f;
                head.SetPosition(0, neck);
                head.SetPosition(1, tip);
                head.startColor = color;
                head.endColor = color;
            }
        }

        public void SetVisible(bool visible)
        {
            if (m_root != null) m_root.SetActive(visible);
        }

        public void Dispose()
        {
            if (m_material != null) Object.Destroy(m_material);
            if (m_root != null) Object.Destroy(m_root);
        }

        private void ClearGrid()
        {
            foreach (LineRenderer lr in m_arrowLines)
            {
                if (lr != null) Object.Destroy(lr.gameObject);
            }
            m_arrowLines.Clear();
        }

        private LineRenderer CreateLine(string goName)
        {
            GameObject go = new GameObject(goName);
            go.transform.SetParent(m_root.transform, false);
            go.layer = m_root.layer;

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = m_lineWidthMin;
            lr.endWidth = m_lineWidthMin;
            lr.useWorldSpace = true;
            lr.sharedMaterial = m_material;

            return lr;
        }
    }
}

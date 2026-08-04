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
//  BoxShapeRenderer.cs
//
//  Description:
//  IWindRegionShapeRenderer for WindRegion.BOX: a wireframe box — bottom + top rectangle
//  outline joined by 4 vertical edges, no fill — with an arrow grid inside showing the region's
//  wind vector. This is the original (and still default) wind region visual.
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using RosMessageTypes.Lotusim;

namespace Lotusim
{
    [System.Serializable]
    public class BoxShapeRenderer : IWindRegionShapeRenderer
    {
        [Tooltip("World-space Y (height) of the box's bottom edge — typically sea level.")]
        [SerializeField] private float groundHeight = 0f;

        [Tooltip("Vertical extent of the box's wireframe.")]
        [SerializeField] private float wallHeight = 95f;

        [Header("Arrow Grid")]
        [Tooltip("Target spacing between arrows in world units — grid resolution adapts to box size around this.")]
        [SerializeField] private float arrowSpacing = 15f;
        [SerializeField] private Vector2Int minArrowGrid = new Vector2Int(2, 2);
        [SerializeField] private Vector2Int maxArrowGrid = new Vector2Int(6, 6);

        private class State
        {
            public LineRenderer bottomOutline;
            public LineRenderer topOutline;
            public LineRenderer[] verticalEdges;
            public WindArrowFieldRenderer arrows;
        }

        public object Build(Transform root, WindZoneRenderSettings settings)
        {
            var s = new State
            {
                bottomOutline = WindZoneGeometry.CreateLoopLine(root, "BottomOutline", settings.OutlineMaterial, settings.OutlineWidth, settings.LayerIndex, 4),
                topOutline = WindZoneGeometry.CreateLoopLine(root, "TopOutline", settings.OutlineMaterial, settings.OutlineWidth * 0.7f, settings.LayerIndex, 4)
            };

            s.verticalEdges = new LineRenderer[4];
            for (int i = 0; i < 4; i++)
                s.verticalEdges[i] = WindZoneGeometry.CreateEdgeLine(root, $"Edge_{i}", settings.OutlineMaterial, settings.OutlineWidth * 0.5f, settings.LayerIndex);

            s.arrows = new WindArrowFieldRenderer(
                root, "Arrows",
                settings.ArrowLengthScale, settings.MaxArrowLength,
                settings.ArrowLineWidthMin, settings.ArrowLineWidthMax,
                settings.MaxWindSpeedForScale);

            return s;
        }

        public Vector3 UpdateGeometry(object state, WindRegionMsg region, WindZoneRenderSettings settings, out float windMagnitude)
        {
            var s = (State)state;
            WindRegionBoxMsg box = region.box ?? new WindRegionBoxMsg();

            float wx1 = (float)box.x1;
            float wy1 = (float)box.y1;
            float wx2 = (float)box.x2;
            float wy2 = (float)box.y2;

            // Region corners are in the same ground-plane (x, y) frame as vessel/turbine
            // positions: world X <- x, world Z <- y (Gazebo Z-up -> Unity Y-up ground plane).
            float minX = Mathf.Min(wx1, wx2);
            float maxX = Mathf.Max(wx1, wx2);
            float minZ = Mathf.Min(wy1, wy2);
            float maxZ = Mathf.Max(wy1, wy2);
            float width = Mathf.Max(0.01f, maxX - minX);
            float depth = Mathf.Max(0.01f, maxZ - minZ);

            Vector3 c00 = new Vector3(minX, groundHeight, minZ);
            Vector3 c10 = new Vector3(maxX, groundHeight, minZ);
            Vector3 c11 = new Vector3(maxX, groundHeight, maxZ);
            Vector3 c01 = new Vector3(minX, groundHeight, maxZ);

            float topY = groundHeight + wallHeight;
            Vector3 t00 = new Vector3(c00.x, topY, c00.z);
            Vector3 t10 = new Vector3(c10.x, topY, c10.z);
            Vector3 t11 = new Vector3(c11.x, topY, c11.z);
            Vector3 t01 = new Vector3(c01.x, topY, c01.z);

            WindZoneGeometry.SetPositions(s.bottomOutline, c00, c10, c11, c01);
            WindZoneGeometry.SetPositions(s.topOutline, t00, t10, t11, t01);

            s.verticalEdges[0].SetPosition(0, c00); s.verticalEdges[0].SetPosition(1, t00);
            s.verticalEdges[1].SetPosition(0, c10); s.verticalEdges[1].SetPosition(1, t10);
            s.verticalEdges[2].SetPosition(0, c11); s.verticalEdges[2].SetPosition(1, t11);
            s.verticalEdges[3].SetPosition(0, c01); s.verticalEdges[3].SetPosition(1, t01);

            Vector3 center = new Vector3((minX + maxX) * 0.5f, groundHeight, (minZ + maxZ) * 0.5f);

            // Arrow grid resolution adapts to box size; only rebuild when it actually changes.
            int cols = Mathf.Clamp(Mathf.RoundToInt(width / arrowSpacing) + 1, minArrowGrid.x, maxArrowGrid.x);
            int rows = Mathf.Clamp(Mathf.RoundToInt(depth / arrowSpacing) + 1, minArrowGrid.y, maxArrowGrid.y);
            Vector2 gridSpacing = new Vector2(
                cols > 1 ? width / (cols - 1) : width,
                rows > 1 ? depth / (rows - 1) : depth);
            Vector3 arrowCenter = new Vector3(center.x, 0f, center.z);
            float arrowHeight = groundHeight + wallHeight * 0.5f;
            Vector2Int gridSize = new Vector2Int(cols, rows);

            if (!s.arrows.GridMatches(gridSize, gridSpacing, arrowCenter, arrowHeight))
                s.arrows.BuildGrid(gridSize, gridSpacing, arrowCenter, arrowHeight);

            windMagnitude = WindZoneGeometry.UpdateArrowsFromVelocity(s.arrows, region.linear_velocity, settings);

            return new Vector3(center.x, topY, center.z);
        }

        public void Dispose(object state)
        {
            ((State)state)?.arrows?.Dispose();
        }
    }
}

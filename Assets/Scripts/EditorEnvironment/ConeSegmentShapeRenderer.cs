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
//  ConeSegmentShapeRenderer.cs
//
//  Description:
//  IWindRegionShapeRenderer for WindRegion.CONE_SEGMENT: a wireframe frustum (truncated cone) —
//  a circle of radius r_start at 'origin', a circle of radius r_end at origin + length*axis, and
//  generatrix lines joining them, no fill — with a small line of arrows along the segment's
//  centerline showing the region's wind vector.
//
//  Orientation: the message's axis/origin are horizontal-only (Gazebo x/y, no height/z), matching
//  a turbine's downstream wake direction. The circular cross-sections are therefore drawn
//  perpendicular to that horizontal axis, i.e. standing vertically (spanning up/down around
//  'axisHeight' and side-to-side) — the standard way a wake cone is visualized, with the flow
//  axis running through the middle of each circle. Chained segments (one's r_end == the next's
//  r_start) land exactly edge-to-edge with no extra work here, per the source message's design.
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using RosMessageTypes.Lotusim;

namespace Lotusim
{
    [System.Serializable]
    public class ConeSegmentShapeRenderer : IWindRegionShapeRenderer
    {
        [Tooltip("World-space Y (height) of the cone's centerline. The message only carries a " +
                 "horizontal (x/y) axis and origin — there's no height data — so this is the " +
                 "single reference height every cone segment is centered on (tune to your " +
                 "turbines' hub height).")]
        [SerializeField] private float axisHeight = 85f;

        [Tooltip("Points per circle (start/end cross-sections). Higher = smoother, more expensive.")]
        [SerializeField] private int circleSegments = 24;

        [Tooltip("Number of lengthwise lines connecting the two circles.")]
        [SerializeField] private int generatrixCount = 6;

        [Header("Arrow Spine")]
        [Tooltip("Target spacing between arrows along the segment's length.")]
        [SerializeField] private float arrowSpacing = 15f;
        [SerializeField] private int minArrowCount = 2;
        [SerializeField] private int maxArrowCount = 6;

        private class State
        {
            public LineRenderer startCircle;
            public LineRenderer endCircle;
            public LineRenderer[] generatrices;
            public WindArrowFieldRenderer arrows;
        }

        public object Build(Transform root, WindZoneRenderSettings settings)
        {
            var s = new State
            {
                startCircle = WindZoneGeometry.CreateLoopLine(root, "StartCircle", settings.OutlineMaterial, settings.OutlineWidth, settings.LayerIndex, circleSegments),
                endCircle = WindZoneGeometry.CreateLoopLine(root, "EndCircle", settings.OutlineMaterial, settings.OutlineWidth, settings.LayerIndex, circleSegments)
            };

            int genCount = Mathf.Clamp(generatrixCount, 2, circleSegments);
            s.generatrices = new LineRenderer[genCount];
            for (int i = 0; i < genCount; i++)
                s.generatrices[i] = WindZoneGeometry.CreateEdgeLine(root, $"Generatrix_{i}", settings.OutlineMaterial, settings.OutlineWidth * 0.6f, settings.LayerIndex);

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
            WindRegionConeSegmentMsg cone = region.cone ?? new WindRegionConeSegmentMsg();

            // Horizontal flow axis in Unity world space (Gazebo x/y -> Unity x/z — same
            // ground-plane convention as WindRegionBox; the message carries no height component).
            Vector2 axisXY = new Vector2((float)cone.axis.x, (float)cone.axis.y);
            Vector3 axisWorld = axisXY.sqrMagnitude > 0.0001f
                ? new Vector3(axisXY.x, 0f, axisXY.y).normalized
                : Vector3.forward;

            // Horizontal vector perpendicular to the axis — together with world-up, spans the
            // (vertical) circle plane.
            Vector3 right = Vector3.Cross(Vector3.up, axisWorld).normalized;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;

            Vector3 start = new Vector3((float)cone.origin.x, axisHeight, (float)cone.origin.y);
            float length = Mathf.Max(0.01f, (float)cone.length);
            Vector3 end = start + axisWorld * length;

            float rStart = Mathf.Max(0f, (float)cone.r_start);
            float rEnd = Mathf.Max(0f, (float)cone.r_end);

            BuildCircle(s.startCircle, start, right, rStart);
            BuildCircle(s.endCircle, end, right, rEnd);

            int genCount = s.generatrices.Length;
            for (int i = 0; i < genCount; i++)
            {
                int idx = Mathf.RoundToInt(i * (circleSegments / (float)genCount)) % circleSegments;
                float theta = (idx / (float)circleSegments) * Mathf.PI * 2f;

                s.generatrices[i].SetPosition(0, CirclePoint(start, right, rStart, theta));
                s.generatrices[i].SetPosition(1, CirclePoint(end, right, rEnd, theta));
            }

            // A small line of arrows along the centerline, evenly spaced by target arrowSpacing.
            int arrowCount = Mathf.Clamp(Mathf.RoundToInt(length / arrowSpacing) + 1, minArrowCount, maxArrowCount);
            List<Vector3> origins = new List<Vector3>(arrowCount);
            for (int i = 0; i < arrowCount; i++)
            {
                float t = arrowCount > 1 ? i / (float)(arrowCount - 1) : 0.5f;
                origins.Add(Vector3.Lerp(start, end, t));
            }

            if (!s.arrows.PointsMatch(origins))
                s.arrows.BuildFromPoints(origins);

            windMagnitude = WindZoneGeometry.UpdateArrowsFromVelocity(s.arrows, region.linear_velocity, settings);

            // Label anchor: above the wider of the two circles, at the segment's midpoint.
            return Vector3.Lerp(start, end, 0.5f) + Vector3.up * Mathf.Max(rStart, rEnd);
        }

        public void Dispose(object state)
        {
            ((State)state)?.arrows?.Dispose();
        }

        private static Vector3 CirclePoint(Vector3 center, Vector3 right, float radius, float theta)
        {
            return center + radius * (Mathf.Cos(theta) * right + Mathf.Sin(theta) * Vector3.up);
        }

        private void BuildCircle(LineRenderer lr, Vector3 center, Vector3 right, float radius)
        {
            for (int i = 0; i < circleSegments; i++)
            {
                float theta = (i / (float)circleSegments) * Mathf.PI * 2f;
                lr.SetPosition(i, CirclePoint(center, right, radius, theta));
            }
        }
    }
}

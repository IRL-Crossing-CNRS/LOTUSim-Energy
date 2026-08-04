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
//  WindZoneGeometry.cs
//
//  Description:
//  Small helpers shared by IWindRegionShapeRenderer implementations (BoxShapeRenderer,
//  ConeSegmentShapeRenderer, ...) so each shape doesn't reimplement wireframe line creation or
//  the region-velocity -> arrow-field update boilerplate.
// --------------------------------------------------------------------------------------------------------------------

using RosMessageTypes.Geometry;
using UnityEngine;

namespace Lotusim
{
    internal static class WindZoneGeometry
    {
        /// <summary>Creates a closed (looped) wireframe LineRenderer with 'pointCount' vertices — e.g. a box's rectangle or a cone's circle.</summary>
        public static LineRenderer CreateLoopLine(Transform parent, string name, Material mat, float width, int layerIndex, int pointCount)
        {
            LineRenderer lr = CreateLine(parent, name, mat, width, layerIndex, pointCount);
            lr.loop = true;
            return lr;
        }

        /// <summary>Creates a straight two-point wireframe LineRenderer — e.g. a box's vertical edge or a cone's generatrix.</summary>
        public static LineRenderer CreateEdgeLine(Transform parent, string name, Material mat, float width, int layerIndex)
        {
            return CreateLine(parent, name, mat, width, layerIndex, 2);
        }

        private static LineRenderer CreateLine(Transform parent, string name, Material mat, float width, int layerIndex, int pointCount)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (layerIndex > -1) go.layer = layerIndex;

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = pointCount;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.useWorldSpace = true;
            lr.sharedMaterial = mat;
            return lr;
        }

        /// <summary>Sets every position of a loop/edge LineRenderer in one call.</summary>
        public static void SetPositions(LineRenderer lr, params Vector3[] points)
        {
            for (int i = 0; i < points.Length; i++)
                lr.SetPosition(i, points[i]);
        }

        /// <summary>Speed (m/s) of a region's linear_velocity, ignoring direction/frame conversion.</summary>
        public static float ComputeMagnitude(Vector3Msg linearVelocity)
        {
            float vx = (float)linearVelocity.x;
            float vy = (float)linearVelocity.y;
            float vz = (float)linearVelocity.z;
            return new Vector3(vx, vy, vz).magnitude;
        }

        /// <summary>
        /// Converts a region's linear_velocity to Unity world space using the same ENU-ish ->
        /// Unity mapping shared by every wind visual in this project (x -> world X, z (up/down)
        /// -> world Y, y (north/south) -> world Z), feeds it to 'arrows', and returns the speed
        /// magnitude so callers can format/color labels consistently across shapes.
        /// </summary>
        public static float UpdateArrowsFromVelocity(WindArrowFieldRenderer arrows, Vector3Msg linearVelocity, WindZoneRenderSettings settings)
        {
            float vx = (float)linearVelocity.x;
            float vy = (float)linearVelocity.y;
            float vz = (float)linearVelocity.z;
            float magnitude = new Vector3(vx, vy, vz).magnitude;

            Vector3 windDirWorld = new Vector3(vx, vz, vy);

            Color arrowColor = settings.OutlineColor;
            arrowColor.a = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(magnitude / settings.MaxWindSpeedForScale));
            arrows.UpdateArrows(windDirWorld, magnitude, arrowColor);

            return magnitude;
        }
    }
}

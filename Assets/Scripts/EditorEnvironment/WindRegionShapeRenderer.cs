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
//  WindRegionShapeRenderer.cs
//
//  Description:
//  Extension point for lotusim_msgs/WindRegion shapes. WindRegionVisualizer keeps a
//  shape_type -> IWindRegionShapeRenderer registry (one stateless renderer instance per shape,
//  built once in Awake) and looks a region up by its shape_type on every update. Each renderer
//  owns per-zone geometry through an opaque state object it creates in Build() and receives back
//  in UpdateGeometry()/Dispose() — the renderer itself stays a singleton, only the state is
//  per-zone.
//
//  Adding a new shape later: implement this interface in a new class, register one instance for
//  its shape_type constant in WindRegionVisualizer.Awake(). The per-frame update loop and the
//  rest of WindRegionVisualizer (color, label, staleness, visibility) never need to change.
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using RosMessageTypes.Lotusim;

namespace Lotusim
{
    /// <summary>
    /// Visual knobs every shape renders with, kept in one place so all wind region shapes share
    /// a consistent look — tuned once on WindRegionVisualizer regardless of how many shapes exist.
    /// </summary>
    public readonly struct WindZoneRenderSettings
    {
        public readonly Material OutlineMaterial;
        public readonly Color OutlineColor;
        public readonly float OutlineWidth;
        public readonly int LayerIndex;
        public readonly float ArrowLengthScale;
        public readonly float MaxArrowLength;
        public readonly float ArrowLineWidthMin;
        public readonly float ArrowLineWidthMax;
        public readonly float MaxWindSpeedForScale;

        public WindZoneRenderSettings(
            Material outlineMaterial,
            Color outlineColor,
            float outlineWidth,
            int layerIndex,
            float arrowLengthScale,
            float maxArrowLength,
            float arrowLineWidthMin,
            float arrowLineWidthMax,
            float maxWindSpeedForScale)
        {
            OutlineMaterial = outlineMaterial;
            OutlineColor = outlineColor;
            OutlineWidth = outlineWidth;
            LayerIndex = layerIndex;
            ArrowLengthScale = arrowLengthScale;
            MaxArrowLength = maxArrowLength;
            ArrowLineWidthMin = arrowLineWidthMin;
            ArrowLineWidthMax = arrowLineWidthMax;
            MaxWindSpeedForScale = maxWindSpeedForScale;
        }
    }

    /// <summary>
    /// One implementation per lotusim_msgs/WindRegion shape_type constant (WindRegionMsg.BOX,
    /// WindRegionMsg.CONE_SEGMENT, ...). Implementations are stateless singletons — all per-zone
    /// GameObjects/LineRenderers live in the opaque 'state' object each instance creates.
    /// </summary>
    public interface IWindRegionShapeRenderer
    {
        /// <summary>
        /// Creates the persistent GameObjects/LineRenderers for one zone, parented under 'root'.
        /// Called once, the first time a region id is seen with this shape_type. Returns an
        /// opaque per-zone state object to pass back into UpdateGeometry()/Dispose().
        /// </summary>
        object Build(Transform root, WindZoneRenderSettings settings);

        /// <summary>
        /// Updates this zone's geometry and arrow field from the latest region data. Returns a
        /// world-space anchor point for the zone's floating label (e.g. the shape's top-center) —
        /// the caller adds its own label height offset on top.
        /// </summary>
        Vector3 UpdateGeometry(object state, WindRegionMsg region, WindZoneRenderSettings settings, out float windMagnitude);

        /// <summary>Releases anything Build() allocated that Destroy(root) wouldn't already clean up (e.g. the arrow field's material).</summary>
        void Dispose(object state);
    }
}

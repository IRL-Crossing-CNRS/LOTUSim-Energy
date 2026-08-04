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
//  WindRegionVisualizer.cs
//
//  Description:
//  Visualizes lotusim_msgs/WindRegionArray as wireframe 3D shapes — solid perimeter only, no
//  fill. Each region's shape_type (WindRegionMsg.BOX, WindRegionMsg.CONE_SEGMENT, ...) selects
//  an IWindRegionShapeRenderer from a registry built once in Awake(); that renderer owns the
//  shape-specific geometry and its arrow field (see WindRegionShapeRenderer.cs). This script
//  owns everything shape-agnostic: the speed-based color gradient, the floating label, visibility toggling and
//  the stale-data timeout. Adding a new shape later never touches this file's update loop —
//  just a new IWindRegionShapeRenderer implementation plus one line in Awake().
//
//  Data flow: RosInterface subscribes to "/aerialWorld/wind/regions" once at startup and pushes
//  every WindRegionArrayMsg to all WindRegionVisualizer instances in the scene via
//  OnWindRegionsReceived() — this script never touches ROS directly.
//
//  Setup:
//  1. Create an empty GameObject in the scene (e.g. "WindRegions") and add this component.
//  2. That's it — RosInterface finds this component automatically and starts feeding it once
//     "/aerialWorld/wind/regions" publishes. Zones are created/updated/removed on the fly as
//     regions appear, move or disappear in the message stream (an empty "regions: []" array,
//     or no message at all for staleTimeoutSeconds, clears everything).
//  3. Toggle visibility at runtime with KeyBindings.ToggleWindRegions (default: Alpha3).
//  4. Tune outline width, arrow style and labels here; tune each shape's own geometry (box
//     height, cone circle resolution, etc.) under its own foldout below.
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using RosMessageTypes.Lotusim;
using TMPro;
using UnityEngine;

namespace Lotusim
{
    /// <summary>
    /// Renders each entry of a WindRegionArrayMsg as a wireframe 3D shape (dispatched by
    /// shape_type to an IWindRegionShapeRenderer), colored by current wind speed, with an
    /// arrow field showing its wind vector.
    /// </summary>
    public class WindRegionVisualizer : MonoBehaviour
    {
        // ---------------------------------------------------------------------------------
        #region Inspector Fields

        [Header("Visibility")]
        [Tooltip("Show wind region shapes. Toggle at runtime with KeyBindings.ToggleWindRegions.")]
        [SerializeField] private bool showRegions = true;

        [Header("Outline")]
        [Tooltip("Width of the perimeter outline lines — shared by every shape for a consistent look.")]
        [SerializeField] private float outlineWidth = 2.5f;

        [Header("Arrow Style")]
        [Tooltip("Base arrow length at wind speed = 1 m/s — shared by every shape.")]
        [SerializeField] private float arrowLengthScale = 2.5f;
        [SerializeField] private float maxArrowLength = 18f;
        [SerializeField] private float arrowLineWidthMin = 0.4f;
        [SerializeField] private float arrowLineWidthMax = 2.2f;
        [Tooltip("Wind speed (m/s) at which arrow width/alpha scaling saturates.")]
        [SerializeField] private float maxWindSpeedForScale = 20f;

        [Header("Speed Color")]
        [Tooltip("Color at zero/calm wind.")]
        [SerializeField] private Color colorCalm = new Color(0.25f, 0.85f, 0.30f);
        [Tooltip("Color at half of maxWindSpeedForScale.")]
        [SerializeField] private Color colorModerate = new Color(0.95f, 0.85f, 0.15f);
        [Tooltip("Color at/above maxWindSpeedForScale.")]
        [SerializeField] private Color colorStrong = new Color(0.95f, 0.20f, 0.15f);

        [Header("Labels")]
        [SerializeField] private bool showLabels = true;
        [SerializeField] private float labelHeightOffset = 6f;
        [SerializeField] private float labelFontSize = 10f;

        [Header("Staleness")]
        [Tooltip("If no WindRegionArray message arrives for this long, every zone is cleared. " +
                 "Safety net for when the scenario is killed/crashes instead of publishing a final " +
                 "empty regions array — otherwise zones would linger forever with nothing left to " +
                 "tell Unity they should disappear. Set above the scenario's normal publish interval.")]
        [SerializeField] private float staleTimeoutSeconds = 60f;

        [Header("Box Shape (shape_type = BOX)")]
        [SerializeField] private BoxShapeRenderer boxShape = new BoxShapeRenderer();

        [Header("Cone Segment Shape (shape_type = CONE_SEGMENT)")]
        [SerializeField] private ConeSegmentShapeRenderer coneShape = new ConeSegmentShapeRenderer();

        #endregion
        // ---------------------------------------------------------------------------------

        #region Private Fields

        // Below this wind speed (m/s), a zone is hidden entirely rather than drawn calm-green —
        // an empty box/wake with no actual wind in it is just visual noise. Matches the snap-to-
        // zero threshold used elsewhere (WindSliderController.OnWindMsgReceived, WindArrowFieldRenderer).
        private const float kZeroWindEpsilon = 0.01f;

        private class ZoneInstance
        {
            public GameObject root;
            public Material outlineMat;
            public TextMeshPro label;
            public Color color;
            public bool regionEnabled;
            public bool hasWind;
            public byte shapeType;
            public IWindRegionShapeRenderer shapeRenderer;
            public object shapeState;
        }

        // shape_type -> renderer. One stateless singleton per shape; per-zone state lives in
        // ZoneInstance.shapeState. Adding a new shape: implement IWindRegionShapeRenderer and
        // add one line here — nothing else in this file needs to change.
        private readonly Dictionary<byte, IWindRegionShapeRenderer> m_shapeRenderers = new Dictionary<byte, IWindRegionShapeRenderer>();

        private readonly Dictionary<string, ZoneInstance> m_zones = new Dictionary<string, ZoneInstance>();
        private int m_layerIndex;

        // -1 = no message ever received yet (don't time out before the scenario has even started).
        private float m_lastMessageRealtime = -1f;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Unity Lifecycle

        private void Awake()
        {
            m_layerIndex = LayerMask.NameToLayer("Trajectories");

            m_shapeRenderers[WindRegionMsg.BOX] = boxShape;
            m_shapeRenderers[WindRegionMsg.CONE_SEGMENT] = coneShape;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyBindings.ToggleWindRegions))
            {
                showRegions = !showRegions;
                ApplyVisibility();
            }

            if (m_zones.Count == 0) return;

            // Scenario went silent (killed/crashed) without publishing a final empty regions
            // array — nothing left to tell us to clean up, so time out and clear everything.
            if (m_lastMessageRealtime >= 0f && Time.realtimeSinceStartup - m_lastMessageRealtime > staleTimeoutSeconds)
            {
                Debug.Log($"[WindRegionVisualizer] No wind region update for {staleTimeoutSeconds:F1}s — clearing all zones.");
                ClearAllZones();
                return;
            }

            if (!showRegions) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // Billboard each zone's label so it always faces the viewer.
            foreach (ZoneInstance zone in m_zones.Values)
            {
                if (!zone.root.activeSelf || zone.label == null) continue;
                zone.label.transform.rotation = cam.transform.rotation;
            }
        }

        private void OnDestroy()
        {
            ClearAllZones();
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region ROS Data Entry Point

        /// <summary>
        /// Called by RosInterface whenever a lotusim_msgs/WindRegionArray message arrives.
        /// Creates/updates a zone per region id, and tears down zones for ids no longer present.
        /// </summary>
        public void OnWindRegionsReceived(WindRegionArrayMsg msg)
        {
            if (msg == null) return;

            m_lastMessageRealtime = Time.realtimeSinceStartup;

            // Treat a null array the same as an empty one — some ROS2 bindings deserialize an
            // empty list as null rather than a zero-length array. Either way, "regions: []" (the
            // scenario's clean-shutdown signal) must still clear every existing zone below.
            WindRegionMsg[] regions = msg.regions ?? System.Array.Empty<WindRegionMsg>();

            HashSet<string> seenIds = new HashSet<string>();

            foreach (WindRegionMsg region in regions)
            {
                if (string.IsNullOrEmpty(region.id)) continue;
                seenIds.Add(region.id);

                if (!m_zones.TryGetValue(region.id, out ZoneInstance zone) || zone.shapeType != region.shape_type)
                {
                    // New id, or its shape_type changed underneath us (rare) — (re)build fresh.
                    if (zone != null) DestroyZone(zone);
                    zone = CreateZone(region);
                    m_zones[region.id] = zone;
                    Debug.Log($"[WindRegionVisualizer] New wind region '{region.id}' (shape_type {region.shape_type}).");
                }

                UpdateZone(zone, region);
            }

            List<string> stale = null;
            foreach (string id in m_zones.Keys)
            {
                if (!seenIds.Contains(id)) (stale ??= new List<string>()).Add(id);
            }

            if (stale != null)
            {
                foreach (string id in stale)
                {
                    DestroyZone(m_zones[id]);
                    m_zones.Remove(id);
                    Debug.Log($"[WindRegionVisualizer] Removed wind region '{id}'.");
                }
            }
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Zone Lifecycle

        private ZoneInstance CreateZone(WindRegionMsg region)
        {
            var zone = new ZoneInstance
            {
                color = colorCalm, // placeholder — UpdateZone() below sets the real speed color before the first render
                shapeType = region.shape_type
            };

            GameObject root = new GameObject($"WindZone_{region.id}");
            root.transform.SetParent(transform, false);
            if (m_layerIndex > -1) root.layer = m_layerIndex;
            zone.root = root;

            // Solid (opaque) outline — every shape is wireframe-only, no translucent fill.
            Shader shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Color");
            zone.outlineMat = new Material(shader);
            WindVisualUtils.SetColor(zone.outlineMat, zone.color);

            if (m_shapeRenderers.TryGetValue(region.shape_type, out IWindRegionShapeRenderer renderer))
            {
                zone.shapeRenderer = renderer;
                zone.shapeState = renderer.Build(root.transform, MakeSettings(zone));
            }
            else
            {
                Debug.LogWarning($"[WindRegionVisualizer] No renderer registered for shape_type " +
                                  $"{region.shape_type} (region '{region.id}') — nothing will be drawn for it.");
            }

            if (showLabels)
                zone.label = CreateLabel(root.transform, region.id, zone.color);

            return zone;
        }

        private void UpdateZone(ZoneInstance zone, WindRegionMsg region)
        {
            zone.regionEnabled = region.enable_wind;

            float speed = WindZoneGeometry.ComputeMagnitude(region.linear_velocity);
            zone.hasWind = speed >= kZeroWindEpsilon;

            zone.root.SetActive(showRegions && zone.regionEnabled && zone.hasWind);
            if (!zone.regionEnabled || !zone.hasWind || zone.shapeRenderer == null) return;

            // Color by current wind speed rather than a fixed per-id color: chained cone segments
            // of the same wake carry near-identical speed, so they read as one continuous shape
            // instead of a patchwork of unrelated colors — and it doubles as an at-a-glance
            // severity indicator for boxes too.
            zone.color = SpeedToColor(speed);
            WindVisualUtils.SetColor(zone.outlineMat, zone.color);

            Vector3 labelAnchor = zone.shapeRenderer.UpdateGeometry(zone.shapeState, region, MakeSettings(zone), out float magnitude);

            if (zone.label != null)
            {
                zone.label.transform.position = labelAnchor + Vector3.up * labelHeightOffset;
                zone.label.text = $"{region.id}\n{magnitude:F1} m/s";
                zone.label.color = Color.Lerp(zone.color, Color.white, 0.55f);
            }
        }

        private void DestroyZone(ZoneInstance zone)
        {
            zone.shapeRenderer?.Dispose(zone.shapeState);
            if (zone.outlineMat != null) Destroy(zone.outlineMat);
            if (zone.root != null) Destroy(zone.root);
        }

        private void ApplyVisibility()
        {
            foreach (ZoneInstance zone in m_zones.Values)
                zone.root.SetActive(showRegions && zone.regionEnabled && zone.hasWind);
        }

        private void ClearAllZones()
        {
            foreach (ZoneInstance zone in m_zones.Values)
                DestroyZone(zone);
            m_zones.Clear();
        }

        private WindZoneRenderSettings MakeSettings(ZoneInstance zone)
        {
            return new WindZoneRenderSettings(
                zone.outlineMat, zone.color, outlineWidth, m_layerIndex,
                arrowLengthScale, maxArrowLength, arrowLineWidthMin, arrowLineWidthMax, maxWindSpeedForScale);
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Label Helper

        private TextMeshPro CreateLabel(Transform parent, string id, Color color)
        {
            GameObject go = new GameObject($"Label_{id}");
            go.transform.SetParent(parent, false);

            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = labelFontSize;
            tmp.text = id;
            tmp.color = Color.Lerp(color, Color.white, 0.55f); // brighten for legibility against the background
            tmp.enableWordWrapping = false;
            tmp.outlineWidth = 0.15f;
            tmp.outlineColor = new Color(0f, 0f, 0f, 0.6f);
            return tmp;
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Color Assignment

        /// <summary>
        /// Green -> yellow -> red gradient by wind speed (a straight green-to-red lerp passes
        /// through a muddy grey/brown at the midpoint, hence the 3-stop ramp through yellow).
        /// </summary>
        private Color SpeedToColor(float speed)
        {
            float t = Mathf.Clamp01(speed / Mathf.Max(0.01f, maxWindSpeedForScale));
            return t < 0.5f
                ? Color.Lerp(colorCalm, colorModerate, t / 0.5f)
                : Color.Lerp(colorModerate, colorStrong, (t - 0.5f) / 0.5f);
        }

        #endregion
    }
}

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
//  CoordinateExporter.cs
//
//  Description:
//  Editor utility to extract and export world coordinates of selected GameObjects as JSON.
//  Useful for exporting wind turbine positions to external tools or scenario files.
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CoordinateExporter : EditorWindow
{
    // ---------------------------------------------------------------------------------
    #region Data

    [Serializable]
    private class ObjectEntry
    {
        public string name;
        public double x;
        public double y;
        public double z;
    }

    [Serializable]
    private class EntryList
    {
        public List<ObjectEntry> objects = new List<ObjectEntry>();
    }

    #endregion
    // ---------------------------------------------------------------------------------

    #region Fields

    private Vector2 scrollPosition;
    private string jsonOutput = "";
    private bool includeRotation = false;
    private bool includeChildren = false;
    private string nameFilter = "";
    private int coordinateSystem = 0; // 0 = Unity, 1 = ROS/Gazebo
    private static readonly string[] coordinateLabels = { "Unity (Y-Up)", "ROS/Gazebo (Z-Up)" };

    #endregion
    // ---------------------------------------------------------------------------------

    #region Window

    [MenuItem("LOTUSim/Utilities/Coordinate Exporter")]
    public static void ShowWindow()
    {
        var window = GetWindow<CoordinateExporter>("Coordinate Exporter");
        window.minSize = new Vector2(400, 350);
    }

    #endregion
    // ---------------------------------------------------------------------------------

    #region GUI

    void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Coordinate Exporter", EditorStyles.boldLabel);
        GUILayout.Label("Select objects in the Hierarchy, then generate JSON.", EditorStyles.miniLabel);
        GUILayout.Space(10);

        // --- Options ---
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Options", EditorStyles.boldLabel);

        coordinateSystem = EditorGUILayout.Popup("Coordinate System", coordinateSystem, coordinateLabels);
        includeRotation = EditorGUILayout.Toggle("Include Rotation", includeRotation);
        includeChildren = EditorGUILayout.Toggle("Include Children", includeChildren);
        nameFilter = EditorGUILayout.TextField("Name Filter (contains)", nameFilter);

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // --- Selection info ---
        int selectionCount = Selection.gameObjects.Length;
        EditorGUILayout.HelpBox(
            selectionCount > 0
                ? $"{selectionCount} object(s) selected in Hierarchy."
                : "No objects selected. Select one or more GameObjects in the Hierarchy.",
            selectionCount > 0 ? MessageType.Info : MessageType.Warning
        );

        GUILayout.Space(5);

        // --- Buttons ---
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Generate JSON", GUILayout.Height(35)))
        {
            GenerateJson();
        }

        GUI.backgroundColor = new Color(0.4f, 0.6f, 1f);
        GUI.enabled = !string.IsNullOrEmpty(jsonOutput);
        if (GUILayout.Button("Copy to Clipboard", GUILayout.Height(35)))
        {
            EditorGUIUtility.systemCopyBuffer = jsonOutput;
            Debug.Log("[CoordinateExporter] JSON copied to clipboard.");
            ShowNotification(new GUIContent("Copied to clipboard!"));
        }

        GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
        if (GUILayout.Button("Export to File", GUILayout.Height(35)))
        {
            ExportToFile();
        }
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        // --- JSON preview ---
        if (!string.IsNullOrEmpty(jsonOutput))
        {
            GUILayout.Label("JSON Output:", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(jsonOutput, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }

    #endregion
    // ---------------------------------------------------------------------------------

    #region Logic

    private void GenerateJson()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0)
        {
            Debug.LogWarning("[CoordinateExporter] No objects selected.");
            return;
        }

        List<GameObject> targets = new List<GameObject>();

        foreach (var go in selected)
        {
            CollectTargets(go, targets);
        }

        // Apply name filter
        if (!string.IsNullOrEmpty(nameFilter))
        {
            targets.RemoveAll(go => !go.name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (targets.Count == 0)
        {
            jsonOutput = "[]";
            Debug.LogWarning("[CoordinateExporter] No objects matched after filtering.");
            return;
        }

        // Build JSON manually for clean formatting
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[");

        for (int i = 0; i < targets.Count; i++)
        {
            GameObject go = targets[i];
            Vector3 pos = go.transform.position;

            // Convert coordinate system if needed
            double x, y, z;
            if (coordinateSystem == 1) // ROS/Gazebo (Z-Up, right-handed)
            {
                x = pos.x;
                y = pos.z;
                z = pos.y;
            }
            else // Unity (Y-Up)
            {
                x = pos.x;
                y = pos.y;
                z = pos.z;
            }

            var inv = CultureInfo.InvariantCulture;
            sb.Append("  {");
            sb.Append($" \"name\": \"{EscapeJson(go.name)}\"");
            sb.Append(string.Format(inv, ", \"x\": {0:F4}", x));
            sb.Append(string.Format(inv, ", \"y\": {0:F4}", y));
            sb.Append(string.Format(inv, ", \"z\": {0:F4}", z));

            if (includeRotation)
            {
                Vector3 rot = go.transform.eulerAngles;
                if (coordinateSystem == 1)
                {
                    sb.Append(string.Format(inv, ", \"roll\": {0:F4}", rot.z));
                    sb.Append(string.Format(inv, ", \"pitch\": {0:F4}", -rot.x));
                    sb.Append(string.Format(inv, ", \"yaw\": {0:F4}", rot.y));
                }
                else
                {
                    sb.Append(string.Format(inv, ", \"rx\": {0:F4}", rot.x));
                    sb.Append(string.Format(inv, ", \"ry\": {0:F4}", rot.y));
                    sb.Append(string.Format(inv, ", \"rz\": {0:F4}", rot.z));
                }
            }

            sb.Append(" }");

            if (i < targets.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }

        sb.Append("]");
        jsonOutput = sb.ToString();

        Debug.Log($"[CoordinateExporter] Generated JSON for {targets.Count} object(s).");
    }

    private void CollectTargets(GameObject go, List<GameObject> list)
    {
        if (!list.Contains(go))
            list.Add(go);

        if (includeChildren)
        {
            foreach (Transform child in go.transform)
            {
                CollectTargets(child.gameObject, list);
            }
        }
    }

    private void ExportToFile()
    {
        if (string.IsNullOrEmpty(jsonOutput))
            return;

        string path = EditorUtility.SaveFilePanel(
            "Export Coordinates",
            Application.dataPath,
            "coordinates",
            "json"
        );

        if (string.IsNullOrEmpty(path))
            return;

        File.WriteAllText(path, jsonOutput);
        Debug.Log($"[CoordinateExporter] Exported to: {path}");
        ShowNotification(new GUIContent($"Saved to {Path.GetFileName(path)}"));
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    #endregion
}

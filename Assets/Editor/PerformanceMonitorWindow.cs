using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Lotusim.Editor
{
    public class PerformanceMonitorWindow : EditorWindow
    {
        private float deltaTime = 0.0f;
        private float msec;
        private float fps;

        // Graph data
        private List<float> fpsHistory = new List<float>();
        private float graphDuration = 30f; // 30 seconds
        private float sampleRate = 0.1f; // 10 samples per second
        private float timeSinceLastSample = 0f;

        [MenuItem("LOTUSim/Utilities/Performance Monitor")]
        public static void ShowWindow()
        {
            GetWindow<PerformanceMonitorWindow>("Perf Monitor");
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            // Smoothed framerate calculation
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            msec = deltaTime * 1000.0f;
            fps = 1.0f / deltaTime;
            
            // Record history
            timeSinceLastSample += Time.unscaledDeltaTime;
            if (timeSinceLastSample >= sampleRate)
            {
                timeSinceLastSample = 0f;
                fpsHistory.Add(fps);

                // Keep only the last N seconds of data
                int maxSamples = Mathf.CeilToInt(graphDuration / sampleRate);
                if (fpsHistory.Count > maxSamples)
                {
                    fpsHistory.RemoveAt(0);
                }
            }

            // Force Unity to repaint this window so the counter updates
            Repaint();
        }

        private void OnGUI()
        {
            GUILayout.Label("LOTUSim Performance", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to see real-time performance.", MessageType.Info);
                return;
            }

            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.fontSize = 24;
            
            // Dynamic colors based on FPS
            if (fps < 30) style.normal.textColor = Color.red;
            else if (fps < 60) style.normal.textColor = Color.yellow;
            else style.normal.textColor = Color.green;

            GUILayout.BeginVertical("box");
            GUILayout.Label($"FPS: {Mathf.RoundToInt(fps)}", style);
            GUILayout.Label($"Frame Time: {msec:F1} ms");
            GUILayout.EndVertical();

            EditorGUILayout.Space();
            DrawFPSGraph();

            EditorGUILayout.Space();
            GUILayout.Label("System Variables", EditorStyles.boldLabel);
            GUILayout.BeginVertical("box");
            GUILayout.Label($"TimeScale: {Time.timeScale:F2}");
            GUILayout.Label($"DeltaTime: {Time.deltaTime:F4}");
            GUILayout.EndVertical();
        }

        private void DrawFPSGraph()
        {
            GUILayout.Label("FPS History (Last 30 seconds)", EditorStyles.boldLabel);
            Rect graphRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(100), GUILayout.ExpandWidth(true));
            
            // Draw background
            EditorGUI.DrawRect(graphRect, new Color(0.15f, 0.15f, 0.15f, 1f));

            if (fpsHistory.Count < 2) return;

            // Define scale
            float maxFPS = 120f; // Scale up to 120 FPS
            float minFPS = 0f;

            // Draw grid lines
            Handles.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            float y60 = graphRect.yMax - (60f / maxFPS) * graphRect.height;
            float y30 = graphRect.yMax - (30f / maxFPS) * graphRect.height;
            Handles.DrawLine(new Vector3(graphRect.x, y60, 0), new Vector3(graphRect.xMax, y60, 0));
            Handles.DrawLine(new Vector3(graphRect.x, y30, 0), new Vector3(graphRect.xMax, y30, 0));

            // Labels
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
            labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            GUI.Label(new Rect(graphRect.x + 2, y60 - 15, 30, 15), "60", labelStyle);
            GUI.Label(new Rect(graphRect.x + 2, y30 - 15, 30, 15), "30", labelStyle);

            // Create points
            Vector3[] points = new Vector3[fpsHistory.Count];
            float stepX = graphRect.width / Mathf.Max(1, (graphDuration / sampleRate));

            for (int i = 0; i < fpsHistory.Count; i++)
            {
                // Draw from right to left
                float x = graphRect.xMax - (fpsHistory.Count - 1 - i) * stepX;
                float normalizedY = Mathf.Clamp01((fpsHistory[i] - minFPS) / (maxFPS - minFPS));
                float y = graphRect.yMax - normalizedY * graphRect.height;
                points[i] = new Vector3(x, y, 0);
            }

            // Draw line based on current FPS performance color
            Handles.color = fpsHistory[fpsHistory.Count - 1] > 60 ? Color.green : 
                            fpsHistory[fpsHistory.Count - 1] > 30 ? Color.yellow : Color.red;
            
            Handles.DrawAAPolyLine(2.0f, points);
        }
    }
}

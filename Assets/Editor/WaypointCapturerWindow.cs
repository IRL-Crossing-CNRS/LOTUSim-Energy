using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System;

public class WaypointCapturerWindow : EditorWindow
{
    // ─── Coordinate Origin (from .world file, same as UnityPatrolExporter) ───────
    private const double ORIGIN_LATITUDE  = 50.32879166666667;
    private const double ORIGIN_LONGITUDE = -4.195226666666667;
    private const double EARTH_RADIUS     = 6378137.0;

    // ─── Internal State ──────────────────────────────────────────────────────────
    private bool isCapturing = false;

    // Each entry stores the Unity XYZ position (for drawing) + computed lat/lon
    private struct GeoWaypoint
    {
        public Vector3 unityPos;
        public double  lat;
        public double  lon;
    }
    private List<GeoWaypoint> waypoints = new List<GeoWaypoint>();

    // ─── Visual Settings ─────────────────────────────────────────────────────────
    private float indicatorSize  = 2f;
    private Color indicatorColor = new Color(0f, 1f, 1f, 1f); // cyan
    private bool  drawLines      = true;

    // ─── Export Settings ─────────────────────────────────────────────────────────
    public enum ExportFormat { JSONArray, JSONWithMmsi }
    private ExportFormat exportFormat = ExportFormat.JSONArray;
    private uint   mmsi            = 999000001;
    private string baseTimestamp   = ""; // leave empty → use UTC now

    private Vector2 scrollPos;

    [MenuItem("LOTUSim/Utilities/Waypoint Capturer")]
    public static void ShowWindow()
    {
        GetWindow<WaypointCapturerWindow>("Waypoint Capturer");
    }

    private void OnEnable()  => SceneView.duringSceneGui += OnSceneGUI;
    private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    // ─── Editor GUI ───────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        // Title
        GUILayout.Space(8);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        GUILayout.Label("  Waypoint Capturer  (Lat / Lon)", titleStyle);

        EditorGUILayout.HelpBox(
            $"Origin: {ORIGIN_LATITUDE:F6}, {ORIGIN_LONGITUDE:F6}  (same as UnityPatrolExporter)",
            MessageType.None);

        GUILayout.Space(6);

        // ── Capture toggle ──────────────────────────────────────────────────────
        GUI.backgroundColor = isCapturing ? new Color(1f, 0.35f, 0.35f) : new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button(isCapturing ? "■  Stop Capturing  [C]" : "●  Start Capturing  [C]",
                             GUILayout.Height(38)))
        {
            isCapturing = !isCapturing;
            if (isCapturing && SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Focus();
        }
        GUI.backgroundColor = Color.white;

        if (isCapturing)
            EditorGUILayout.HelpBox(
                "Survole la Scene View et appuie sur [C] pour capturer un waypoint.\n" +
                "Le rayon vise les colliders ; sinon il tombe sur le plan Y=0.",
                MessageType.Info);

        // ── Visual Settings ────────────────────────────────────────────────────
        GUILayout.Space(8);
        GUILayout.Label("Affichage", EditorStyles.boldLabel);
        indicatorColor = EditorGUILayout.ColorField("Couleur des indicateurs", indicatorColor);
        indicatorSize  = EditorGUILayout.Slider("Taille des sphères", indicatorSize, 0.1f, 20f);
        drawLines      = EditorGUILayout.Toggle("Tracer les lignes", drawLines);

        // ── Export Settings ────────────────────────────────────────────────────
        GUILayout.Space(8);
        GUILayout.Label("Export", EditorStyles.boldLabel);
        exportFormat = (ExportFormat)EditorGUILayout.EnumPopup("Format", exportFormat);

        if (exportFormat == ExportFormat.JSONWithMmsi)
            mmsi = (uint)EditorGUILayout.LongField("MMSI", mmsi);

        baseTimestamp = EditorGUILayout.TextField("Timestamp de départ (optionnel)", baseTimestamp);
        EditorGUILayout.HelpBox(
            "Laisser vide → timestamp UTC actuel. Format attendu : yyyy-MM-ddTHH:mm:ss",
            MessageType.None);

        // ── Actions ────────────────────────────────────────────────────────────
        GUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copier JSON", GUILayout.Height(30)))
            CopyToClipboard();
        if (GUILayout.Button("Effacer tout", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Effacer", "Supprimer tous les waypoints ?", "Oui", "Annuler"))
            {
                waypoints.Clear();
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.EndHorizontal();

        // ── Waypoint List ─────────────────────────────────────────────────────
        GUILayout.Space(8);
        GUILayout.Label($"Points capturés : {waypoints.Count}", EditorStyles.boldLabel);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, "box");
        for (int i = 0; i < waypoints.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"WP {i}", GUILayout.Width(38));
            EditorGUILayout.LabelField(
                $"lat {waypoints[i].lat:F9}   lon {waypoints[i].lon:F9}",
                EditorStyles.miniLabel);
            if (GUILayout.Button("✕", GUILayout.Width(24)))
            {
                waypoints.RemoveAt(i);
                SceneView.RepaintAll();
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    // ─── Scene GUI ────────────────────────────────────────────────────────────────
    private void OnSceneGUI(SceneView sceneView)
    {
        // Draw existing waypoints
        Handles.color = indicatorColor;
        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 pos = waypoints[i].unityPos;

            Handles.SphereHandleCap(0, pos, Quaternion.identity, indicatorSize, EventType.Repaint);

            GUIStyle lbl = new GUIStyle { fontStyle = FontStyle.Bold };
            lbl.normal.textColor = Color.white;
            Handles.Label(pos + Vector3.up * indicatorSize * 0.8f,
                $"  WP {i}\n  {waypoints[i].lat:F6}\n  {waypoints[i].lon:F6}", lbl);

            if (drawLines && i > 0)
                Handles.DrawLine(waypoints[i - 1].unityPos, pos);
        }

        // Capture
        if (!isCapturing) return;

        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.C)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 hitPos;

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                hitPos = hit.point;
            }
            else
            {
                Plane hPlane = new Plane(Vector3.up, Vector3.zero);
                if (hPlane.Raycast(ray, out float dist))
                    hitPos = ray.GetPoint(dist);
                else
                    return;
            }

            var (lat, lon) = UnityToLatLon(hitPos);
            waypoints.Add(new GeoWaypoint { unityPos = hitPos, lat = lat, lon = lon });
            e.Use();

            Debug.Log($"<color=cyan>WP {waypoints.Count - 1}</color> → lat={lat:F9}  lon={lon:F9}");
            Repaint();
            SceneView.RepaintAll();
        }
    }

    // ─── Coordinate Conversion ────────────────────────────────────────────────────
    private static (double lat, double lon) UnityToLatLon(Vector3 unityPos)
    {
        // Unity: X = East, Z = North, Y = Up  (same convention as UnityPatrolExporter)
        double metersEast  = unityPos.x;
        double metersNorth = unityPos.z;

        double latRad = ORIGIN_LATITUDE * Math.PI / 180.0;

        double deltaLat = metersNorth / EARTH_RADIUS * (180.0 / Math.PI);
        double deltaLon = metersEast  / (EARTH_RADIUS * Math.Cos(latRad)) * (180.0 / Math.PI);

        return (ORIGIN_LATITUDE + deltaLat, ORIGIN_LONGITUDE + deltaLon);
    }

    // ─── Clipboard Export ─────────────────────────────────────────────────────────
    private void CopyToClipboard()
    {
        if (waypoints.Count == 0)
        {
            EditorUtility.DisplayDialog("Aucun point", "Capturez d'abord au moins un waypoint !", "OK");
            return;
        }

        DateTime t0;
        if (string.IsNullOrWhiteSpace(baseTimestamp) ||
            !DateTime.TryParse(baseTimestamp, out t0))
            t0 = DateTime.UtcNow;

        StringBuilder sb = new StringBuilder();

        if (exportFormat == ExportFormat.JSONWithMmsi)
        {
            sb.AppendLine("{");
            sb.AppendLine($"    \"mmsi\": {mmsi},");
            sb.AppendLine("    \"waypoints\": [");
            for (int i = 0; i < waypoints.Count; i++)
            {
                AppendWaypointJSON(sb, i, t0, i < waypoints.Count - 1);
            }
            sb.AppendLine("    ]");
            sb.AppendLine("}");
        }
        else // JSONArray
        {
            sb.AppendLine("[");
            for (int i = 0; i < waypoints.Count; i++)
            {
                AppendWaypointJSON(sb, i, t0, i < waypoints.Count - 1);
            }
            sb.AppendLine("]");
        }

        GUIUtility.systemCopyBuffer = sb.ToString();
        EditorUtility.DisplayDialog("Copié !",
            $"{waypoints.Count} waypoints copiés dans le presse-papier (format {exportFormat}).", "OK");
    }

    private void AppendWaypointJSON(StringBuilder sb, int i, DateTime t0, bool addComma)
    {
        string ts = t0.AddMinutes(i * 2).ToString("yyyy-MM-ddTHH:mm:ss");
        string ic = new string(' ', exportFormat == ExportFormat.JSONWithMmsi ? 8 : 4);
        string bc = new string(' ', exportFormat == ExportFormat.JSONWithMmsi ? 4 : 0);

        sb.AppendLine($"{bc}    {{");
        sb.AppendLine($"{ic}\"timestamp\": \"{ts}\",");
        sb.AppendLine($"{ic}\"lat\": {waypoints[i].lat.ToString("R", System.Globalization.CultureInfo.InvariantCulture)},");
        sb.AppendLine($"{ic}\"lon\": {waypoints[i].lon.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}");
        sb.Append($"{bc}    }}");
        if (addComma) sb.Append(",");
        sb.AppendLine();
    }
}

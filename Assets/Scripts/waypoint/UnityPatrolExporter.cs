using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

public class UnityPatrolExporter : MonoBehaviour
{
    // From the .world file
    private const double ORIGIN_LATITUDE = 50.32879166666667;
    private const double ORIGIN_LONGITUDE = -4.195226666666667;
    private const double EARTH_RADIUS = 6378137.0;
    
    [Header("Patrol Configuration")]
    public List<Transform> patrolWaypoints = new List<Transform>();
    public string exportFileName = "unity_patrol_waypoints.json";
    public uint mmsi = 999000001;  // WAMV MMSI
    
    [Header("Export Path")]
    public string exportPath = "";  // Leave empty for default
    
    [System.Serializable]
    public class WaypointData
    {
        public string timestamp;
        public double lat;
        public double lon;
    }
    
    [System.Serializable]
    public class PatrolExport
    {
        public uint mmsi;
        public List<WaypointData> waypoints;
    }
    
    void Start()
    {
        if (string.IsNullOrEmpty(exportPath))
        {
            exportPath = Application.dataPath;
        }
    }
    
    // Call this to export waypoints (attach to UI button or use [ContextMenu])
    [ContextMenu("Export Patrol Waypoints")]
    public void ExportPatrolWaypoints()
    {
        PatrolExport export = new PatrolExport
        {
            mmsi = mmsi,
            waypoints = new List<WaypointData>()
        };
        
        DateTime startTime = DateTime.UtcNow;
        
        for (int i = 0; i < patrolWaypoints.Count; i++)
        {
            Vector3 unityPos = patrolWaypoints[i].position;
            var (lat, lon) = UnityToLatLong(unityPos);
            
            export.waypoints.Add(new WaypointData
            {
                timestamp = startTime.AddMinutes(i * 2).ToString("yyyy-MM-ddTHH:mm:ss"),
                lat = lat,
                lon = lon
            });
        }
        
        string json = JsonUtility.ToJson(export, true);
        string fullPath = Path.Combine(exportPath, exportFileName);
        File.WriteAllText(fullPath, json);
        
        Debug.Log($"Exported {export.waypoints.Count} waypoints to: {fullPath}");
    }
    
    private (double lat, double lon) UnityToLatLong(Vector3 unityPos)
    {
        // Unity typically uses X=East, Z=North, Y=Up
        double metersEast = unityPos.x;
        double metersNorth = unityPos.z;
        
        double latRadians = ORIGIN_LATITUDE * Math.PI / 180.0;
        
        double deltaLat = metersNorth / EARTH_RADIUS * (180.0 / Math.PI);
        double deltaLon = metersEast / (EARTH_RADIUS * Math.Cos(latRadians)) * (180.0 / Math.PI);
        
        double latitude = ORIGIN_LATITUDE + deltaLat;
        double longitude = ORIGIN_LONGITUDE + deltaLon;
        
        return (latitude, longitude);
    }
    
    // Visual helper in Scene view
    void OnDrawGizmos()
    {
        if (patrolWaypoints == null || patrolWaypoints.Count == 0) return;
        
        Gizmos.color = Color.cyan;
        for (int i = 0; i < patrolWaypoints.Count; i++)
        {
            if (patrolWaypoints[i] == null) continue;
            
            Gizmos.DrawSphere(patrolWaypoints[i].position, 2f);
            
            if (i < patrolWaypoints.Count - 1 && patrolWaypoints[i + 1] != null)
            {
                Gizmos.DrawLine(patrolWaypoints[i].position, patrolWaypoints[i + 1].position);
            }
        }
    }
}
using System.Collections.Generic;
using Lotusim;
using UnityEngine;
using RosMessageTypes.Lotusim;
using Unity.Robotics.ROSTCPConnector;

/// <summary>
/// Ultra-smooth pose follower that uses Snapshot Interpolation.
/// It buffers the incoming ROS poses and plays them back with a slight delay,
/// completely eliminating stuttering from low-frequency Gazebo updates.
/// </summary>
public class RendererPosesWaypointFollower : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Constrain movement to the horizontal plane (useful for surface vessels).")]
    public bool constrainToHorizontal = false;
    
    [Tooltip("Delay in seconds to allow smooth interpolation. Increase if stutter remains (e.g. 0.1 to 0.25). 0.15 is usually perfect for 10Hz.")]
    public float interpolationDelay = 0.15f;
    
    [Header("Debug")]
    [Tooltip("The automatically detected vessel name. Leave empty to auto-detect.")]
    public string vesselName = "";

    private struct PoseSnapshot
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
    }

    private List<PoseSnapshot> poseBuffer = new List<PoseSnapshot>();

    private void Start()
    {
        // Automatically find the vessel name based on the GameObject's name.
        if (string.IsNullOrEmpty(vesselName))
        {
            vesselName = gameObject.name.Replace("(Clone)", "").Trim();
        }

        // Subscribe to the topic directly to fetch poses smoothly
        if (RosInterface.Instance != null && RosInterface.Instance.IsConnected)
        {
            string topicName = $"{RosInterface.Instance.RosNamespace}/renderer_poses";
            ROSConnection.GetOrCreateInstance().Subscribe<VesselPositionArrayMsg>(
                topicName, OnVesselPositionsReceived);
        }
        else
        {
            Debug.LogWarning($"[RendererPosesWaypointFollower] RosInterface is not connected. {gameObject.name} will not receive poses.");
        }
        
        // Add initial snapshot to start from current position
        poseBuffer.Add(new PoseSnapshot { time = Time.time, position = transform.position, rotation = transform.rotation });
    }

    private void OnVesselPositionsReceived(VesselPositionArrayMsg poses)
    {
        foreach (var vessel in poses.vessels)
        {
            if (vessel.vessel_name == vesselName)
            {
                // Extract pose from ROS message
                Vector3 rosPos = new Vector3((float)vessel.pose.position.x, (float)vessel.pose.position.y, (float)vessel.pose.position.z);
                Quaternion rosRot = new Quaternion((float)vessel.pose.orientation.x, (float)vessel.pose.orientation.y, (float)vessel.pose.orientation.z, (float)vessel.pose.orientation.w);
                
                Pose rawPose = new Pose(rosPos, rosRot);
                Pose unityPose = CoordinateSystemUtils.GzPoseToUnityPose(rawPose);

                Vector3 newPos = unityPose.position;
                if (constrainToHorizontal)
                    newPos.y = 0f;

                // Add to buffer with the current Unity time
                poseBuffer.Add(new PoseSnapshot { time = Time.time, position = newPos, rotation = unityPose.rotation });

                // Prune by AGE, not count: at a high real-time factor poses arrive
                // far faster than the native 10 Hz, so a fixed 15-entry cap would
                // span less wall-time than interpolationDelay — renderTime then falls
                // before the whole buffer and the vessel freezes. Keeping a fixed
                // time window guarantees the buffer always brackets renderTime.
                float minSpan = Mathf.Max(1f, interpolationDelay * 4f);
                float cutoff = Time.time - minSpan;
                while (poseBuffer.Count > 2 && poseBuffer[0].time < cutoff)
                    poseBuffer.RemoveAt(0);

                break;
            }
        }
    }

    private void Update()
    {
        if (poseBuffer.Count < 2) return;

        // We render the object slightly in the past so we always have a "future" point to interpolate towards.
        float renderTime = Time.time - interpolationDelay;

        // Find the two snapshots to interpolate between
        for (int i = 0; i < poseBuffer.Count - 1; i++)
        {
            PoseSnapshot snap1 = poseBuffer[i];
            PoseSnapshot snap2 = poseBuffer[i + 1];

            if (renderTime >= snap1.time && renderTime <= snap2.time)
            {
                float t = (renderTime - snap1.time) / (snap2.time - snap1.time);
                
                transform.position = Vector3.Lerp(snap1.position, snap2.position, t);
                transform.rotation = Quaternion.Slerp(snap1.rotation, snap2.rotation, t);
                return; // We found the exact window, we can exit
            }
        }

        // If we are past the latest snapshot (e.g. simulation paused or message dropped)
        // We gently ease towards the very latest known position to avoid stopping abruptly
        PoseSnapshot latest = poseBuffer[poseBuffer.Count - 1];
        if (renderTime > latest.time)
        {
             transform.position = Vector3.Lerp(transform.position, latest.position, Time.deltaTime * 10f);
             transform.rotation = Quaternion.Slerp(transform.rotation, latest.rotation, Time.deltaTime * 10f);
        }
    }
}

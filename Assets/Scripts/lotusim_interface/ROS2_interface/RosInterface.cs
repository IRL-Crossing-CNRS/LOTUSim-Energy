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
//  RosInterface.cs
//
//  Description:
//  Singleton interface for ROS2 communication in Lotusim.
//  Handles vessel pose updates, renderer commands, dynamic vessel commands, and simulation stats.
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;
using RosMessageTypes.Geometry;
using RosMessageTypes.Lotusim;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

namespace Lotusim
{
    /// <summary>
    /// Singleton interface for ROS2 communication.
    /// </summary>
    public class RosInterface : LotusimBaseInterface
    {
        #region Singleton

        private static RosInterface _instance;

        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static RosInterface Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new RosInterface();
                return _instance;
            }
        }

        // Private constructor to enforce singleton
        private RosInterface() { }

        #endregion

        #region Public Properties

        /// <summary>
        /// ROS namespace used for all topics.
        /// </summary>
        public string RosNamespace { get; private set; }

        /// <summary>
        /// Reference to the ROS connection instance.
        /// </summary>
        public ROSConnection RosInstance => m_rosConnection;

        /// <summary>
        /// Returns true if ROS connection is initialized.
        /// </summary>
        public bool IsConnected => m_rosConnection != null;

        #endregion

        #region Private Fields

        private ROSConnection m_rosConnection;

        private VesselPositionArrayMsg m_vessel_array_msg;
        private Mutex m_vessel_array_mutex = new Mutex();
        private VesselPositionArrayMsg m_prev_vessel_array_msg = null;

        private readonly List<RendererCmdMsg> m_render_commands = new List<RendererCmdMsg>();
        private Mutex m_render_commands_mutex = new Mutex();

        private readonly Dictionary<string, VesselCmdMsg> m_xdyn_cmds = new Dictionary<string, VesselCmdMsg>();
        private Mutex m_xdyn_cmds_mutex = new Mutex();

        private SimStatsMsg m_sim_stats_msg;
        private Mutex m_sim_stats_mutex = new Mutex();

        #endregion

        #region Lifecycle

        /// <summary>
        /// Initializes ROS subscriptions with the given namespace.
        /// </summary>
        /// <param name="_namespace">Namespace for ROS topics.</param>
        public override void Start(string _namespace)
        {
            Debug.Log("[RosInterface] Starting ROS2 interface with namespace: " + _namespace);

            RosNamespace = _namespace;
            m_rosConnection = ROSConnection.GetOrCreateInstance();

            // Subscribe to vessel poses
            m_rosConnection.Subscribe<VesselPositionArrayMsg>(
                $"{_namespace}/renderer_poses",
                OnVesselPositionsReceived
            );

            // Subscribe to renderer commands
            m_rosConnection.Subscribe<RendererCmdMsg>(
                $"{_namespace}/renderer_cmd",
                OnRendererCommandReceived
            );

            // Subscribe to dynamic vessel commands
            m_rosConnection.Subscribe<VesselCmdArrayMsg>(
                $"{_namespace}/lotusim_vessel_array_cmd",
                OnXdynCmdReceived
            );

            // Subscribe to simulation statistics
            m_rosConnection.Subscribe<SimStatsMsg>(
                $"{_namespace}/sim_stats",
                OnSimStatsReceived
            );

            Debug.Log("[RosInterface] Subscribed to all ROS topics.");
        }

        public override void Destroy() { }

        public override void Update()
        {
            ProcessRenderCommands();
            UpdateVesselPoses();
            ProcessXdynCommands();
        }

        #endregion

        #region Message Handlers

        private void OnVesselPositionsReceived(VesselPositionArrayMsg poses)
        {
            m_vessel_array_mutex.WaitOne();
            m_vessel_array_msg = poses;
            m_vessel_array_mutex.ReleaseMutex();
        }

        private void OnRendererCommandReceived(RendererCmdMsg command)
        {
            m_render_commands_mutex.WaitOne();
            m_render_commands.Add(command);
            m_render_commands_mutex.ReleaseMutex();
        }

        private void OnXdynCmdReceived(VesselCmdArrayMsg array)
        {
            m_xdyn_cmds_mutex.WaitOne();
            foreach (VesselCmdMsg cmd in array.cmds)
            {
                m_xdyn_cmds[cmd.vessel_name] = cmd;
            }
            m_xdyn_cmds_mutex.ReleaseMutex();
        }

        private void OnSimStatsReceived(SimStatsMsg msg)
        {
            m_sim_stats_mutex.WaitOne();
            m_sim_stats_msg = msg;
            m_sim_stats_mutex.ReleaseMutex();
        }

        #endregion

        #region Vessel Pose Handling

        /// <summary>
        /// Computes interpolation ratio between previous and current vessel positions.
        /// </summary>
        /// <returns>Interpolation ratio in [0.2,0.8].</returns>
        public override float GetInterpolationRatio()
        {
            float ratio = 0.5f;

            m_vessel_array_mutex.WaitOne();

            if (m_vessel_array_msg != null)
            {
                float deltaTime = Time.deltaTime;
                float currentTime = Time.time;

                float simTime =
                    m_vessel_array_msg.header.stamp.sec +
                    m_vessel_array_msg.header.stamp.nanosec * 1e-9f;

                if (currentTime > simTime)
                {
                    ratio = Mathf.Clamp((simTime - currentTime) / deltaTime, 0.2f, 0.8f);
                }
            }

            m_vessel_array_mutex.ReleaseMutex();
            return ratio;
        }

        private void UpdateVesselPoses()
        {
            if (m_vessel_array_msg == null) return;

            float interpolationRatio = GetInterpolationRatio();
            if (interpolationRatio <= 0) return;

            m_vessel_array_mutex.WaitOne();

            if (m_prev_vessel_array_msg == null)
                m_prev_vessel_array_msg = m_vessel_array_msg;

            Dictionary<string, Pose> currentFrame = new Dictionary<string, Pose>();
            Dictionary<string, Pose> previousFrame = new Dictionary<string, Pose>();

            foreach (var vessel in m_vessel_array_msg.vessels)
                currentFrame[vessel.vessel_name] = PoseMsgToPose(vessel.pose);

            foreach (var vessel in m_prev_vessel_array_msg.vessels)
                previousFrame[vessel.vessel_name] = PoseMsgToPose(vessel.pose);

            foreach (var kvp in currentFrame)
            {
                string name = kvp.Key;
                Pose currentPose = kvp.Value;
                Pose previousPose = previousFrame.ContainsKey(name) ? previousFrame[name] : currentPose;

                Vector3 pos = Vector3.Lerp(previousPose.position, currentPose.position, interpolationRatio);
                Quaternion rot = Quaternion.Slerp(previousPose.rotation, currentPose.rotation, interpolationRatio);

                m_vesselPoses[name] = new Pose(pos, rot);
            }

            m_prev_vessel_array_msg = m_vessel_array_msg;

            m_vessel_array_mutex.ReleaseMutex();
        }

        private Pose PoseMsgToPose(PoseMsg msg)
        {
            return new Pose(
                new Vector3((float)msg.position.x, (float)msg.position.y, (float)msg.position.z),
                new Quaternion((float)msg.orientation.x, (float)msg.orientation.y,
                               (float)msg.orientation.z, (float)msg.orientation.w)
            );
        }

        #endregion

        #region Renderer Commands

        private void ProcessRenderCommands()
        {
            m_render_commands_mutex.WaitOne();

            foreach (var command in m_render_commands)
            {
                Debug.Log($"[RosInterface] HandleCommand: {command.cmd_type} {command.vessel_name}");

                switch (command.cmd_type)
                {
                    case (byte)RendererCmdMsg.CREATE_CMD:
                        Pose poseUnity = CoordinateSystemUtils.GzPoseToUnityPose(PoseMsgToPose(command.vessel_position));
                        m_vesselToCreate[command.vessel_name] = new Tuple<string, Pose>(command.renderer_obj_name, poseUnity);
                        break;
                    case (byte)RendererCmdMsg.DELETE_CMD:
                        m_vesselsToDestroy.Add(command.vessel_name);
                        break;
                    case (byte)RendererCmdMsg.EXPLODE_CMD:
                        m_vesselsToExplode.Add(command.vessel_name);
                        break;
                    default:
                        Debug.LogError($"[RosInterface] Invalid command type {command.cmd_type}");
                        break;
                }
            }

            m_render_commands.Clear();
            m_render_commands_mutex.ReleaseMutex();
        }

        #endregion

        #region Dynamic Vessel Commands

        private void ProcessXdynCommands()
        {
            m_xdyn_cmds_mutex.WaitOne();

            foreach (var (_, command) in m_xdyn_cmds)
            {
                Dictionary<string, float> thrusters = JsonConvert.DeserializeObject<Dictionary<string, float>>(command.cmd_string);

                foreach (var (name, value) in thrusters)
                {
                    string cleanedName = name.Contains("(rpm)") ? name.Replace("(rpm)", "").Trim() : name;
                    m_propellerSpinRatios[cleanedName] = value;
                }
            }

            m_xdyn_cmds.Clear();
            m_xdyn_cmds_mutex.ReleaseMutex();
        }

        #endregion

        #region Simulation Stats

        /// <summary>
        /// Thread-safe retrieval of the latest simulation statistics message.
        /// </summary>
        /// <returns>Latest <see cref="SimStatsMsg"/> or null if unavailable.</returns>
        public SimStatsMsg GetSimStats()
        {
            m_sim_stats_mutex.WaitOne();
            var copy = m_sim_stats_msg;
            m_sim_stats_mutex.ReleaseMutex();
            return copy;
        }

        #endregion
    }
}

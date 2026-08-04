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
using RosMessageTypes.Sensor;
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

        /// <summary>
        /// True only while the TCP link to the ROS endpoint is established and error-free.
        /// Goes false the moment the socket drops (e.g. remote killed), letting the
        /// connector wipe spawned agents before reconnect replays latched commands.
        /// </summary>
        public override bool IsHealthy => m_rosConnection != null && !m_rosConnection.HasConnectionError;

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

        private WindTurbineArrayMsg m_wind_turbine_msg;
        private Mutex m_wind_turbine_mutex = new Mutex();

        // Wind vector: subscribed lazily per-topic (WindSliderController may use a custom topic
        // name), same idempotent-registration pattern as the inspection detections subscription.
        private WindMsg m_wind_msg;
        private Mutex m_wind_mutex = new Mutex();
        private readonly HashSet<string> m_windSubscribedTopics = new HashSet<string>();

        // Wind regions: fixed topic, broadcast by the simulator core (like wind turbines).
        private WindRegionArrayMsg m_wind_region_msg;
        private Mutex m_wind_region_mutex = new Mutex();

        // Ocean current: single latched 2-axis (ENU x/y) vector, scoped under this scene's
        // vessel namespace (unlike wind, which is a fixed-topic global aerial concept).
        // enable_current is the scenario's explicit on/off signal — generic_scenario publishes
        // enable_current=false on teardown (ctrl+c / agent delete), which is how the visualizer
        // knows to hide the field instead of relying on a stale-data timeout (this topic is
        // latched and, in practice, only ever published once at scenario start).
        private OceanCurrentMsg m_ocean_current_msg;
        private Mutex m_ocean_current_mutex = new Mutex();

        // Inspection: image transport (Unity → remote) and detections (remote → Unity).
        private readonly Dictionary<string, CompressedImageMsg> m_inspectionImageMsgs = new Dictionary<string, CompressedImageMsg>();
        private readonly Dictionary<string, string> m_inspectionDetections = new Dictionary<string, string>();
        private readonly HashSet<string> m_inspectionSubscribed = new HashSet<string>();
        // Tracks which image publisher topics are currently registered with ROS TCP Connector, so we
        // skip RegisterPublisher on re-spawn (avoids the "registered twice" warning). Entries are
        // dropped in CleanupInspection, which also tears the publisher down on the endpoint.
        private readonly HashSet<string> m_registeredImagePublishers = new HashSet<string>();
        private Mutex m_inspection_mutex = new Mutex();

        // --- Stale-command suppression (TRANSIENT_LOCAL safety) ----------------------------------
        // renderer_cmd is a single persistent publisher, KEEP_LAST depth 10, TRANSIENT_LOCAL. So the
        // broker re-delivers the last ~10 latched commands on every (re)subscribe — e.g. after the TCP
        // link drops and ROS TCP Connector reconnects. Without a guard, a replayed CREATE that predates
        // a DELETE resurrects an agent the simulator already removed.
        //
        // render_interface stamps DELETE/EXPLODE with its system clock (no use_sim_time → monotonic
        // across sim restarts) and — once patched — stamps CREATE the same way. We then suppress a
        // CREATE whose stamp is NOT newer than the most recent removal of that vessel: the latched
        // pre-DELETE CREATE (older stamp) is dropped, while a genuine relaunch (newer wall-clock stamp)
        // passes. Comparing stamps, not buffer position, is robust to the depth-10 ring evicting old
        // entries. A CREATE with stamp 0 (publisher not yet patched) bypasses the guard so respawn
        // still works — at the cost of not filtering replays until the host stamps CREATEs.
        private readonly Dictionary<string, long> m_lastRemoveStampNs = new Dictionary<string, long>();

        #endregion

        #region Events

        /// <summary>
        /// Raised on the main thread the moment a DELETE/EXPLODE command for a vessel is processed,
        /// so per-agent listeners (e.g. <see cref="AgentBatteryHUD"/>) can release their ROS
        /// subscriptions immediately instead of waiting for their next discovery scan.
        /// </summary>
        public static event Action<string> VesselRemoved;

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

            // Subscribe to wind turbine simulation data (effective speed + power per turbine)
            m_rosConnection.Subscribe<WindTurbineArrayMsg>(
                $"{_namespace}/wind/turbines",
                OnWindTurbinesReceived
            );

            // Subscribe to wind regions (localized wind boxes, distinct from the ambient Wind slider).
            // Not namespaced under _namespace: like WindSliderController's ambient WindMsg topic
            // ("/aerialWorld/wind"), wind regions are a global environment concept published on a
            // fixed absolute topic, not scoped to this scene's vessel namespace ("energy", etc.).
            m_rosConnection.Subscribe<WindRegionArrayMsg>(
                "/aerialWorld/wind/regions",
                OnWindRegionsReceived
            );

            // Subscribe to ocean current (latched, single fixed vector for now, 2-axis ENU).
            // Namespaced under this scene, unlike wind, since current is specific to this
            // vessel/world's underwater environment rather than a shared aerial-world concept.
            m_rosConnection.Subscribe<OceanCurrentMsg>(
                $"{_namespace}/ocean_current",
                OnOceanCurrentReceived
            );

            Debug.Log("[RosInterface] Subscribed to all ROS topics.");
        }

        public override void Destroy() { }

        public override void Update()
        {
            ProcessRenderCommands();
            UpdateVesselPoses();
            ProcessXdynCommands();
            ProcessWindTurbines();
            ProcessWind();
            ProcessWindRegions();
            ProcessOceanCurrent();
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

        private void OnWindTurbinesReceived(WindTurbineArrayMsg msg)
        {
            m_wind_turbine_mutex.WaitOne();
            m_wind_turbine_msg = msg;
            m_wind_turbine_mutex.ReleaseMutex();
        }

        private void OnWindRegionsReceived(WindRegionArrayMsg msg)
        {
            m_wind_region_mutex.WaitOne();
            m_wind_region_msg = msg;
            m_wind_region_mutex.ReleaseMutex();
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
                string vesselName = command.vessel_name;
                long stampNs = StampNanos(command.header);

                switch (command.cmd_type)
                {
                    case (byte)RendererCmdMsg.CREATE_CMD:
                        // Drop a latched CREATE that predates the vessel's removal (a TRANSIENT_LOCAL
                        // replay on reconnect). A genuine relaunch carries a newer wall-clock stamp and
                        // passes. stamp 0 = publisher not yet patched to stamp CREATEs → can't order, so
                        // honor it (respawn keeps working; replays just aren't filtered yet).
                        if (stampNs != 0 &&
                            m_lastRemoveStampNs.TryGetValue(vesselName, out long removedAtNs) && stampNs <= removedAtNs)
                        {
                            Debug.Log($"[RosInterface] Ignoring stale CREATE for '{vesselName}' " +
                                      $"(latched replay predating its removal; create {stampNs} <= remove {removedAtNs} ns).");
                            break;
                        }
                        Debug.Log($"[RosInterface] HandleCommand: CREATE {vesselName}");
                        Pose poseUnity = CoordinateSystemUtils.GzPoseToUnityPose(PoseMsgToPose(command.vessel_position));
                        m_vesselToCreate[vesselName] = new Tuple<string, Pose>(command.renderer_obj_name, poseUnity);
                        break;
                    case (byte)RendererCmdMsg.DELETE_CMD:
                        Debug.Log($"[RosInterface] HandleCommand: DELETE {vesselName}");
                        HandleVesselRemoved(vesselName, stampNs);
                        m_vesselsToDestroy.Add(vesselName);
                        break;
                    case (byte)RendererCmdMsg.EXPLODE_CMD:
                        Debug.Log($"[RosInterface] HandleCommand: EXPLODE {vesselName}");
                        HandleVesselRemoved(vesselName, stampNs);
                        m_vesselsToExplode.Add(vesselName);
                        break;
                    default:
                        Debug.LogError($"[RosInterface] Invalid command type {command.cmd_type}");
                        break;
                }
            }

            m_render_commands.Clear();
            m_render_commands_mutex.ReleaseMutex();
        }

        /// <summary>
        /// Centralised bookkeeping when a vessel is removed (DELETE/EXPLODE): records the removal stamp
        /// so an older latched CREATE can't resurrect it on a reconnect, and notifies per-agent listeners
        /// (battery HUD, …) so they drop their subscriptions immediately rather than waiting for a
        /// discovery scan. Inspection ROS resources are released by the agent's InspectionDetector.OnDestroy
        /// (which carries the correctly-derived agent name) when the GameObject is destroyed this tick.
        /// Main thread only.
        /// </summary>
        private void HandleVesselRemoved(string vesselName, long stampNs)
        {
            // Keep the newest removal stamp: a later DELETE must win over an earlier (possibly replayed) one.
            if (!m_lastRemoveStampNs.TryGetValue(vesselName, out long prev) || stampNs > prev)
                m_lastRemoveStampNs[vesselName] = stampNs;

            VesselRemoved?.Invoke(vesselName);
        }

        private static long StampNanos(HeaderMsg header)
        {
            return (long)header.stamp.sec * 1_000_000_000L + header.stamp.nanosec;
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
        /// <summary>
        /// Publishes a JPEG-encoded inspection frame for the given agent as a
        /// sensor_msgs/CompressedImage on {namespace}/{agentName}/inspection/image.
        /// The publisher and a reusable message object are registered lazily on first use.
        /// Call from the main thread (publishing is not thread-safe in ROS TCP Connector).
        /// </summary>
        public void PublishInspectionImage(string agentName, byte[] jpgBytes)
        {
            m_inspection_mutex.WaitOne();
            if (!m_inspectionImageMsgs.TryGetValue(agentName, out CompressedImageMsg msg))
            {
                string topic = $"{RosNamespace}/{agentName}/inspection/image";
                if (m_registeredImagePublishers.Add(agentName))
                {
                    m_rosConnection.RegisterPublisher<CompressedImageMsg>(topic);
                    Debug.Log($"[RosInterface] Registered inspection image publisher: {topic}");
                }
                msg = new CompressedImageMsg { format = "jpeg" };
                msg.header.frame_id = agentName;
                m_inspectionImageMsgs[agentName] = msg;
            }
            m_inspection_mutex.ReleaseMutex();

            msg.data = jpgBytes;
            m_rosConnection.Publish($"{RosNamespace}/{agentName}/inspection/image", msg);
        }

        /// <summary>
        /// Registers a ROS subscription for the inspection detections of the given agent.
        /// Idempotent. Topic: {namespace}/{agentName}/inspection/detections (std_msgs/String,
        /// carrying the same JSON shape the Flask server used to return: {"detections":[...],"count":N}).
        /// </summary>
        public void RegisterInspectionDetectionsSubscription(string agentName)
        {
            m_inspection_mutex.WaitOne();
            bool alreadySubscribed = !m_inspectionSubscribed.Add(agentName);
            m_inspection_mutex.ReleaseMutex();

            if (alreadySubscribed) return;

            string topic = $"{RosNamespace}/{agentName}/inspection/detections";
            m_rosConnection.Subscribe<StringMsg>(topic, msg =>
            {
                m_inspection_mutex.WaitOne();
                m_inspectionDetections[agentName] = msg.data;
                m_inspection_mutex.ReleaseMutex();
            });

            Debug.Log($"[RosInterface] Subscribed to {topic}");
        }

        /// Tears down a vessel's inspection ROS resources when it despawns: the detections
        /// subscription and the image publisher (so neither topic lingers on the endpoint).
        public void CleanupInspection(string agentName)
        {
            m_inspection_mutex.WaitOne();
            bool wasSubscribed = m_inspectionSubscribed.Remove(agentName);
            bool wasPublishing = m_registeredImagePublishers.Remove(agentName);
            m_inspectionDetections.Remove(agentName);
            m_inspectionImageMsgs.Remove(agentName);
            m_inspection_mutex.ReleaseMutex();

            if (wasSubscribed)
                m_rosConnection.Unsubscribe($"{RosNamespace}/{agentName}/inspection/detections");

            if (wasPublishing)
                UnregisterPublisher($"{RosNamespace}/{agentName}/inspection/image");
        }

        // ROS TCP Connector exposes no public API to unregister a publisher, so a per-agent
        // inspection/image topic would otherwise linger on the endpoint forever — and re-advertise on
        // every reconnect — after the agent is deleted. We drive the connector's internal
        // "__remove_publisher" path and drop the cached RosTopicState (so it isn't re-registered) by
        // reflection. Reflection targets the pinned ros-tcp-connector package; failures are non-fatal.
        private static System.Reflection.MethodInfo s_sendSysCommand;
        private static System.Reflection.FieldInfo s_topicsField;

        private void UnregisterPublisher(string topic)
        {
            if (m_rosConnection == null) return;
            try
            {
                Type connType = m_rosConnection.GetType();
                s_sendSysCommand ??= connType.GetMethod("SendSysCommand",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                s_topicsField ??= connType.GetField("m_Topics",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Tell the endpoint to destroy the ROS2 publisher.
                s_sendSysCommand?.Invoke(m_rosConnection, new object[]
                {
                    Unity.Robotics.ROSTCPConnector.SysCommand.k_SysCommand_RemovePublisher,
                    new Unity.Robotics.ROSTCPConnector.SysCommand_Topic { topic = topic },
                    null
                });

                // Drop the local topic state so it isn't re-advertised on the next reconnect.
                if (s_topicsField?.GetValue(m_rosConnection) is System.Collections.IDictionary topics)
                {
                    lock (topics) topics.Remove(topic);
                }

                Debug.Log($"[RosInterface] Unregistered inspection image publisher: {topic}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RosInterface] Could not unregister publisher '{topic}': {e.Message}");
            }
        }

        /// <summary>
        /// Returns the latest detections JSON received for the agent and clears it (consume-once),
        /// or null if nothing new arrived since the last call.
        /// </summary>
        public string ConsumeInspectionDetections(string agentName)
        {
            m_inspection_mutex.WaitOne();
            m_inspectionDetections.TryGetValue(agentName, out string json);
            if (json != null) m_inspectionDetections.Remove(agentName);
            m_inspection_mutex.ReleaseMutex();
            return json;
        }

        public SimStatsMsg GetSimStats()
        {
            m_sim_stats_mutex.WaitOne();
            var copy = m_sim_stats_msg;
            m_sim_stats_mutex.ReleaseMutex();
            return copy;
        }

        #endregion

        #region Wind Turbine Handling

        /// <summary>
        /// Dispatches the latest wind turbine data to all WindTurbineController components in the scene.
        /// Matches turbines by name field.
        /// </summary>
        private void ProcessWindTurbines()
        {
            m_wind_turbine_mutex.WaitOne();
            var msg = m_wind_turbine_msg;
            m_wind_turbine_msg = null; // consume
            m_wind_turbine_mutex.ReleaseMutex();

            if (msg == null || msg.turbines == null || msg.turbines.Length == 0)
                return;

            // Find all turbine controllers in the scene (cached implicitly by Unity)
            WindTurbineController[] controllers = UnityEngine.Object.FindObjectsOfType<WindTurbineController>();

            foreach (WindTurbineStateMsg turbineData in msg.turbines)
            {
                foreach (WindTurbineController ctrl in controllers)
                {
                    if (ctrl.TurbineName == turbineData.name)
                    {
                        ctrl.OnWindDataReceived(turbineData.effective_wind_speed, turbineData.power_w);
                        break;
                    }
                }
            }
        }

        #endregion

        #region Wind Vector Handling

        /// <summary>
        /// Subscribes to the given wind topic if not already subscribed. Idempotent, so every
        /// <see cref="WindSliderController"/> instance can call this on its own topic without
        /// duplicating the subscription.
        /// </summary>
        public void RegisterWindSubscription(string topicName)
        {
            if (!m_windSubscribedTopics.Add(topicName)) return;

            m_rosConnection.Subscribe<WindMsg>(topicName, msg =>
            {
                m_wind_mutex.WaitOne();
                m_wind_msg = msg;
                m_wind_mutex.ReleaseMutex();
            });

            Debug.Log($"[RosInterface] Subscribed to wind topic: {topicName}");
        }

        /// <summary>
        /// Dispatches the latest received wind vector to all WindSliderController components in the scene.
        /// </summary>
        private void ProcessWind()
        {
            m_wind_mutex.WaitOne();
            var msg = m_wind_msg;
            m_wind_msg = null; // consume
            m_wind_mutex.ReleaseMutex();

            if (msg == null) return;

            WindSliderController[] controllers = UnityEngine.Object.FindObjectsOfType<WindSliderController>();
            foreach (WindSliderController ctrl in controllers)
            {
                ctrl.OnWindMsgReceived(msg);
            }
        }

        #endregion

        #region Wind Region Handling

        /// <summary>
        /// Dispatches the latest received wind region array to all WindRegionVisualizer
        /// components in the scene (there is typically just one, acting as a manager).
        /// </summary>
        private void ProcessWindRegions()
        {
            m_wind_region_mutex.WaitOne();
            var msg = m_wind_region_msg;
            m_wind_region_msg = null; // consume
            m_wind_region_mutex.ReleaseMutex();

            if (msg == null) return;

            WindRegionVisualizer[] visualizers = UnityEngine.Object.FindObjectsOfType<WindRegionVisualizer>();
            foreach (WindRegionVisualizer visualizer in visualizers)
            {
                visualizer.OnWindRegionsReceived(msg);
            }
        }

        #endregion

        #region Ocean Current Handling

        /// <summary>
        /// Latches the most recent ocean current message (called on the ROS receive thread).
        /// </summary>
        private void OnOceanCurrentReceived(OceanCurrentMsg msg)
        {
            m_ocean_current_mutex.WaitOne();
            m_ocean_current_msg = msg;
            m_ocean_current_mutex.ReleaseMutex();
        }

        /// <summary>
        /// Dispatches the latest received ocean current vector to all OceanCurrentVisualizer
        /// components in the scene.
        /// </summary>
        private void ProcessOceanCurrent()
        {
            m_ocean_current_mutex.WaitOne();
            var msg = m_ocean_current_msg;
            m_ocean_current_msg = null; // consume
            m_ocean_current_mutex.ReleaseMutex();

            if (msg == null) return;

            OceanCurrentVisualizer[] visualizers = UnityEngine.Object.FindObjectsOfType<OceanCurrentVisualizer>();
            foreach (OceanCurrentVisualizer visualizer in visualizers)
            {
                visualizer.OnOceanCurrentReceived(msg);
            }
        }

        #endregion
    }
}

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
//  TCPIPInterface.cs
//
//  Description:
//  Handles UDP/TCP communication with external clients for vessel updates and commands.
//  Supports thread-safe data reception and processing, including vessel positions and commands.
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Lotusim.TCPIP;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Lotusim
{
    /// <summary>
    /// Interface for TCP/IP communication with external clients for Lotusim.
    /// Handles UDP vessel info messages and TCP command messages.
    /// </summary>
    public class TCPIPInterface : LotusimBaseInterface
    {
        #region UDP Fields

        private UdpClient m_udp_client;
        private Thread m_receive_udp_thread;
        private Mutex m_vessel_info_mutex = new Mutex();
        private List<TCPIP.VesselInfo> m_vessel_info_msg = new List<TCPIP.VesselInfo>();

        #endregion

        #region TCP Fields

        private TcpClient m_tcp_client;
        private TcpListener m_tcp_listener;
        private Thread m_receive_tcp_thread;
        private List<UnityCmd> m_cmd_queue = new List<UnityCmd>();
        private Mutex m_cmd_queue_mutex = new Mutex();

        #endregion

        #region Public Properties

        /// <summary>
        /// TCP/UDP port for connections.
        /// </summary>
        public int port = 23457;

        #endregion

        #region Private Fields

        private bool m_shutdown = false;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Initializes the TCP/IP interface and starts UDP and TCP threads.
        /// </summary>
        /// <param name="_namespace">Namespace (unused here, required by base).</param>
        public override void Start(string _namespace)
        {
            Debug.Log("[TCPIPInterface] Starting TCP/IP interface");
            InitializeConnection();
        }

        /// <summary>
        /// Starts UDP and TCP listener threads.
        /// </summary>
        private void InitializeConnection()
        {
            // --- UDP ---
            m_udp_client = new UdpClient(port);
            m_receive_udp_thread = new Thread(ReceiveUDPData)
            {
                IsBackground = true
            };
            m_receive_udp_thread.Start();

            // --- TCP ---
            m_tcp_listener = new TcpListener(IPAddress.Any, port);
            m_tcp_listener.Start();

            m_receive_tcp_thread = new Thread(ReceiveTCPData)
            {
                IsBackground = true
            };
            m_receive_tcp_thread.Start();
        }

        /// <summary>
        /// Shuts down threads and closes connections.
        /// </summary>
        public override void Destroy()
        {
            m_shutdown = true;

            if (m_udp_client != null)
            {
                m_udp_client.Close();
                m_receive_udp_thread.Join();
            }

            if (m_tcp_listener != null)
            {
                m_tcp_listener.Stop();
                Debug.Log("[TCPIPInterface] TCP Listener stopped");
            }

            if (m_tcp_client != null)
            {
                m_tcp_client.Close();
                Debug.Log("[TCPIPInterface] TCP Client closed");
            }

            if (m_receive_tcp_thread != null)
            {
                m_receive_tcp_thread.Join();
                Debug.Log("[TCPIPInterface] TCP receive thread joined");
            }
        }

        #endregion

        #region Update Loop

        public override void Update()
        {
            ProcessCommands();
            UpdateVesselPoses();
        }

        #endregion

        #region UDP Handling

        /// <summary>
        /// Receives vessel info messages over UDP.
        /// </summary>
        private void ReceiveUDPData()
        {
            while (!m_shutdown)
            {
                try
                {
                    IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = m_udp_client.Receive(ref anyIP);
                    string text = Encoding.UTF8.GetString(data);

                    m_vessel_info_mutex.WaitOne();

                    if (text.Contains("VesselsInfo"))
                    {
                        TCPIP.VesselInfoArrayWrapper wrapper =
                            JsonUtility.FromJson<TCPIP.VesselInfoArrayWrapper>(text);

                        m_vessel_info_msg = new List<TCPIP.VesselInfo>(wrapper.VesselsInfo);

                        // Replace "/" with "." in vessel names for consistency
                        for (int i = 0; i < m_vessel_info_msg.Count; i++)
                        {
                            m_vessel_info_msg[i].name = m_vessel_info_msg[i].name.Replace('/', '.');
                        }
                    }

                    m_vessel_info_mutex.ReleaseMutex();
                }
                catch (Exception err)
                {
                    Debug.LogError("[TCPIPInterface][UDP] Error: " + err);
                }
            }
        }

        #endregion

        #region TCP Handling

        /// <summary>
        /// Receives Unity commands via TCP and acknowledges them.
        /// </summary>
        private void ReceiveTCPData()
        {
            while (!m_shutdown)
            {
                try
                {
                    m_tcp_client = m_tcp_listener.AcceptTcpClient();
                    Debug.Log("[TCPIPInterface][TCP] Client connected");

                    using (NetworkStream stream = m_tcp_client.GetStream())
                    {
                        int length;
                        byte[] bytes = new byte[1024];

                        while ((length = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            byte[] incomingData = new byte[length];
                            Array.Copy(bytes, 0, incomingData, 0, length);

                            string clientMessage = Encoding.ASCII.GetString(incomingData);
                            Debug.Log("[TCPIPInterface][TCP] Received: " + clientMessage);

                            try
                            {
                                DeserializeJson(clientMessage);

                                byte[] response = Encoding.ASCII.GetBytes("ACK\r\n");
                                stream.Write(response, 0, response.Length);
                            }
                            catch (Exception err)
                            {
                                Debug.LogError($"[TCPIPInterface][TCP] Error: {err.Message} | msg: {clientMessage}");
                                byte[] response = Encoding.ASCII.GetBytes("FAILED\r\n");
                                stream.Write(response, 0, response.Length);
                            }
                        }
                    }
                }
                catch (SocketException socketException)
                {
                    Debug.LogError("[TCPIPInterface][TCP] SocketException: " + socketException);
                }
            }
        }

        /// <summary>
        /// Converts JSON messages to UnityCmd objects and queues them for processing.
        /// </summary>
        /// <param name="json">JSON message from TCP client.</param>
        private void DeserializeJson(string json)
        {
            JObject jObject = JObject.Parse(json);

            if (jObject["cmd"] != null)
            {
                UnityCmd cmd = JsonConvert.DeserializeObject<UnityCmd>(json, new UnityCmdConverter());

                m_cmd_queue_mutex.WaitOne();
                m_cmd_queue.Add(cmd);
                m_cmd_queue_mutex.ReleaseMutex();
            }
            else
            {
                throw new Exception("[TCPIPInterface] Unknown JSON format");
            }
        }

        #endregion

        #region Command Processing

        /// <summary>
        /// Processes queued Unity commands and updates vessels to create, destroy, or explode.
        /// </summary>
        private void ProcessCommands()
        {
            m_cmd_queue_mutex.WaitOne();

            foreach (UnityCmd cmd in m_cmd_queue)
            {
                Debug.Log("[TCPIPInterface] HandleCmd: " + cmd.cmd + " " + cmd.name);

                switch (cmd.cmd)
                {
                    case "create":
                        m_vesselToCreate[cmd.name] = new Tuple<string, Pose>(
                            cmd.type,
                            CoordinateSystemUtils.GzPoseToUnityPose(new Pose(cmd.position, Quaternion.identity))
                        );
                        break;
                    case "delete":
                        m_vesselsToDestroy.Add(cmd.name);
                        break;
                    case "explode":
                        m_vesselsToExplode.Add(cmd.name);
                        break;
                    default:
                        Debug.LogWarning("[TCPIPInterface] Unknown command type: " + cmd.cmd);
                        break;
                }
            }

            m_cmd_queue.Clear();
            m_cmd_queue_mutex.ReleaseMutex();
        }

        #endregion

        #region Vessel Updates

        /// <summary>
        /// Updates vessel poses and thruster values from the latest UDP messages.
        /// </summary>
        private void UpdateVesselPoses()
        {
            if (m_vessel_info_msg == null || m_vessel_info_msg.Count == 0) return;

            if (GetInterpolationRatio() > 0f)
            {
                m_vessel_info_mutex.WaitOne();

                for (int i = 0; i < m_vessel_info_msg.Count; i++)
                {
                    var vessel = m_vessel_info_msg[i];

                    // Update vessel pose
                    m_vesselPoses[vessel.name] = CoordinateSystemUtils.GzPoseToUnityPose(
                        new Pose(vessel.position, vessel.rotation)
                    );

                    // Update thrusters
                    foreach (ThrusterInfo thruster in vessel.thrusters)
                    {
                        string thrusterFullName = vessel.name + "/" + thruster.name;
                        m_propellerSpinRatios[thrusterFullName] = thruster.rpm * 0.01f;
                    }
                }

                m_vessel_info_mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Calculates interpolation ratio between simulation time and Unity time for smooth movement.
        /// </summary>
        /// <returns>Interpolation ratio (0-1).</returns>
        public override float GetInterpolationRatio()
        {
            m_vessel_info_mutex.WaitOne();

            float ratio = 0f;

            if (m_vessel_info_msg != null && m_vessel_info_msg.Count > 0)
            {
                float deltaTime = Time.deltaTime;
                float currentTime = Time.time;
                float poseTime = m_vessel_info_msg[0].time;

                if (currentTime < poseTime)
                    ratio = (poseTime - currentTime) / deltaTime;
            }

            m_vessel_info_mutex.ReleaseMutex();
            return ratio;
        }

        #endregion
    }
}

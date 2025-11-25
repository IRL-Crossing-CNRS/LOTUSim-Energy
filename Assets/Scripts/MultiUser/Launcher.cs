/*
 * Copyright (c) 2025 Naval Group
 *
 * This program and the accompanying materials are made available under the
 * terms of the Eclipse Public License 2.0 which is available at
 * https://www.eclipse.org/legal/epl-2.0.
 *
 * SPDX-License-Identifier: EPL-2.0
 */
﻿// --------------------------------------------------------------------------------------------------------------------
//  Launcher.cs (Hybrid Online/Offline)
//
//  Description:
//  - Tries Photon if internet is available
//  - Falls back to Photon OfflineMode if no internet or if connection fails
//  - Preserves your original UI, spectator, ROS, and loader logic
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using Photon.Realtime;
using Photon.Pun;
using TMPro;
using System.Net.NetworkInformation;
using System;

namespace Com.MyCompany.MyGame
{
    #pragma warning disable 649

    /// <summary>
    /// Launch manager. Connect, join or create room automatically on player or spectator mode.
    /// Hybrid: uses Photon when available, falls back to offline mode otherwise.
    /// </summary>
    public class Launcher : MonoBehaviourPunCallbacks
    {
        #region Public Fields

        public static bool IsSpectator = false;

        #endregion

        #region Private Serializable Fields

        [Tooltip("The Ui Panel to let the user enter name, connect and play")]
        [SerializeField] private GameObject controlPanel;

        [Tooltip("The Ui Text to inform the user about the connection progress")]
        [SerializeField] private TMP_Text feedbackText;

        [Tooltip("The maximum number of players per room")]
        [SerializeField] private byte maxPlayersPerRoom = 20;

        [SerializeField] private Toggle spectatorToggle;

        [SerializeField] private TMP_InputField rosIpInputField;
        [SerializeField] private TMP_InputField rosPortInputField;

        [Tooltip("Name of the room to join/create (used both online and offline)")]
        [SerializeField] private string roomName = "defenseScenario";

        #endregion

        #region Private Fields

        /// <summary>
        /// Keep track of the current process. Since connection is asynchronous and is based on several callbacks from Photon,
        /// we need to keep track of this to properly adjust the behavior when we receive call back by Photon.
        /// Typically this is used for the OnConnectedToMaster() callback.
        /// </summary>
        private bool isConnecting;

        /// <summary>
        /// This client's version number. Users are separated from each other by gameVersion (which allows you to make breaking changes).
        /// </summary>
        [SerializeField] private string gameVersion = "1";

        /// <summary>
        /// True when we intentionally started offline mode (so we don't try to fallback repeatedly).
        /// </summary>
        private bool startedOfflineMode = false;

        #endregion

        #region MonoBehaviour CallBacks

        void Awake()
        {
            Debug.Log("Launcher Awake");
            // Ensure scene sync on master client
            PhotonNetwork.AutomaticallySyncScene = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Start the connection process.
        /// - If internet available: attempt to connect to Photon Cloud and join/create the room
        /// - If no internet: starts Photon OfflineMode and create local room
        /// </summary>
        public void Connect()
        {
            // clear feedback UI
            if (feedbackText != null) feedbackText.text = "";

            isConnecting = true;
            startedOfflineMode = false;

            // hide control panel while connecting
            if (controlPanel != null)
                controlPanel.SetActive(false);

            // Determine spectator mode from toggle
            if (spectatorToggle != null)
            {
                IsSpectator = spectatorToggle.isOn;
            }
            else
            {
                Debug.LogWarning("spectatorToggle is not assigned in the Inspector.");
            }

            // Player nickname: try saved name, else random with prefix
            string rolePrefix = IsSpectator ? "Spectator_" : "Player_";
            string savedName = PlayerPrefs.HasKey("PlayerName") ? PlayerPrefs.GetString("PlayerName") : null;

            if (!string.IsNullOrEmpty(savedName))
            {
                PhotonNetwork.NickName = savedName;
            }
            else
            {
                PhotonNetwork.NickName = rolePrefix + UnityEngine.Random.Range(1000, 9999);
            }
            Debug.Log("Connecting as: " + PhotonNetwork.NickName);

            // Save ROS Settings
            if (rosIpInputField != null && rosPortInputField != null)
            {
                string rosIp = rosIpInputField.text.Trim();
                string rosPort = rosPortInputField.text.Trim();

                PlayerPrefs.SetString("ROS_IP", rosIp);
                PlayerPrefs.SetString("ROS_Port", rosPort);
                PlayerPrefs.Save();

                Debug.Log($"Saved ROS IP: {rosIp}, Port: {rosPort}");
            }
            else
            {
                Debug.LogWarning("ROS IP or Port input field is missing!");
            }

            // Decide: try Photon (online) or fallback to offline
            bool hasInternet = HasInternetConnection();

            if (hasInternet)
            {
                LogFeedback("Internet detected. Trying Photon...");
                TryConnectPhoton();
            }
            else
            {
                LogFeedback("No internet detected. Starting offline mode...");
                StartOfflineMode();
            }
        }

        /// <summary>
        /// Write a message in the feedback text UI.
        /// </summary>
        void LogFeedback(string message)
        {
            if (feedbackText == null)
            {
                Debug.Log(message);
                return;
            }
            feedbackText.text += Environment.NewLine + message;
        }

        #endregion

        #region Photon / Internet Helpers

        /// <summary>
        /// Try to connect to Photon using settings. Photon callbacks will be used after.
        /// </summary>
        private void TryConnectPhoton()
        {
            // If already connected to Photon, just join/create the room
            if (PhotonNetwork.IsConnected)
            {
                LogFeedback("Joining Room...");
                PhotonNetwork.JoinOrCreateRoom(roomName, new RoomOptions { MaxPlayers = this.maxPlayersPerRoom }, TypedLobby.Default);
            }
            else
            {
                LogFeedback("Connecting to Photon...");
                // set game version first
                PhotonNetwork.GameVersion = this.gameVersion;
                PhotonNetwork.ConnectUsingSettings();
            }
        }

        /// <summary>
        /// Start offline mode using Photon OfflineMode API and create a local room with the same name.
        /// </summary>
        private void StartOfflineMode()
        {
            startedOfflineMode = true;
            isConnecting = true; // we still consider we are in the connect flow; OnJoinedRoom will be called
            PhotonNetwork.OfflineMode = true;
            LogFeedback("Photon OfflineMode enabled. Creating local room...");
            PhotonNetwork.CreateRoom(roomName, new RoomOptions { MaxPlayers = this.maxPlayersPerRoom });
        }

        /// <summary>
        /// Quick detection of internet availability.
        /// Uses NetworkInterface check and a fast ping to 8.8.8.8 (Google DNS). Might be blocked on some networks,
        /// in which case it falls back to NetworkInterface result.
        /// </summary>
        private bool HasInternetConnection()
        {
            try
            {
                // Quick check: any network interface up?
                if (!NetworkInterface.GetIsNetworkAvailable())
                    return false;

                try
                {
                    using (var p = new System.Net.NetworkInformation.Ping())
                    {
                        var reply = p.Send("8.8.8.8", 200);
                        if (reply != null && reply.Status == IPStatus.Success)
                            return true;
                    }
                }
                catch
                {
                    // Ping could fail due to platform/network restrictions — fallback to network interface result
                    return NetworkInterface.GetIsNetworkAvailable();
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region MonoBehaviourPunCallbacks CallBacks

        /// <summary>
        /// Called after the connection to the master is established and authenticated
        /// </summary>
        public override void OnConnectedToMaster()
        {
            Debug.Log("OnConnectedToMaster callback received.");
            if (isConnecting)
            {
                LogFeedback("OnConnectedToMaster: joining/creating room...");
                PhotonNetwork.JoinOrCreateRoom(roomName, new RoomOptions { MaxPlayers = this.maxPlayersPerRoom }, TypedLobby.Default);
            }
        }

        /// <summary>
        /// Called when a JoinRandom() call failed. We'll create a new room in that case.
        /// </summary>
        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            LogFeedback("<Color=Red>OnJoinRandomFailed</Color>: Creating a new Room");
            Debug.Log("OnJoinRandomFailed() called by PUN. Creating new room.");
            PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = this.maxPlayersPerRoom });
        }

        /// <summary>
        /// Called when entering a room (by creating or joining it). Called on all clients (including the Master Client).
        /// </summary>
        public override void OnJoinedRoom()
        {
            LogFeedback("<Color=Green>OnJoinedRoom</Color> with " + PhotonNetwork.CurrentRoom.PlayerCount + " Player(s)");
            Debug.Log("Launcher: OnJoinedRoom() called by PUN. Now this client is in a room.");

            isConnecting = false;
            controlPanel?.SetActive(true);

            Debug.Log("Loading the 'defenseScenario' level (or your configured level).");

            // Load the Room Level (this will work both online and offline with Photon)
            PhotonNetwork.LoadLevel("defenseScenario");
        }

        /// <summary>
        /// Called after disconnecting from the Photon server.
        /// </summary>
        public override void OnDisconnected(DisconnectCause cause)
        {
            LogFeedback("<Color=Red>OnDisconnected</Color> " + cause);
            Debug.LogError("Launcher:Disconnected -> " + cause);

            isConnecting = false;
            controlPanel?.SetActive(true);

            // If we lost connection but we didn't intentionally start offline mode, fallback to offline mode
            if (!startedOfflineMode)
            {
                LogFeedback("Falling back to offline mode after disconnect.");
                StartOfflineMode();
            }
        }

        /// <summary>
        /// Called when JoinRoom/CreateRoom fails.
        /// </summary>
        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            LogFeedback("<Color=Red>OnJoinRoomFailed</Color>: " + message);
            Debug.LogWarning($"OnJoinRoomFailed: {message}");

            if (!startedOfflineMode)
            {
                LogFeedback("Switching to offline mode due to join room failure.");
                StartOfflineMode();
            }
        }

        #endregion
    }
}

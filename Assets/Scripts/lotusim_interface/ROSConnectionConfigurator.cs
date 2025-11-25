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
//  ROSConnectionConfigurator.cs
//
//  Description:
//  Reads ROS IP and port from PlayerPrefs and configures the ROSConnection singleton.
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using Unity.Robotics.ROSTCPConnector;

namespace Lotusim
{
    /// <summary>
    /// Configures ROSConnection at startup using PlayerPrefs.
    /// Defaults to 127.0.0.1:10000 if not set.
    /// </summary>
    public class ROSConnectionConfigurator : MonoBehaviour
    {
        #region Unity Callbacks

        private void Awake()
        {
            ConfigureROSConnection();
        }

        #endregion

        #region Configuration

        private void ConfigureROSConnection()
        {
            string ip = PlayerPrefs.GetString("ROS_IP", "127.0.0.1");
            string portStr = PlayerPrefs.GetString("ROS_Port", "10000");

            if (!int.TryParse(portStr, out int port))
            {
                Debug.LogError($"[ROSConnectionConfigurator] Invalid ROS port: {portStr}");
                return;
            }

            ROSConnection rosConnection = ROSConnection.GetOrCreateInstance();
            rosConnection.RosIPAddress = ip;
            rosConnection.RosPort = port;

            Debug.Log($"[ROSConnectionConfigurator] Applied ROS IP: {ip}, Port: {port}");
        }

        #endregion
    }
}

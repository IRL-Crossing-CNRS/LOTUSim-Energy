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
//  CameraSensor.cs
//
//  Description:
//  Publishes RGB camera frames from Unity to ROS via the ROS–TCP Connector.
//  Designed for integration with Unity Robotics Hub and Lotusim's simulation framework.
// --------------------------------------------------------------------------------------------------------------------

using System.Linq;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;

namespace Lotusim
{
    /// <summary>
    /// Captures frames from a Unity Camera and publishes them as <see cref="ImageMsg"/> messages to a ROS topic.
    /// </summary>
    /// <remarks>
    /// - Captures images at a fixed rate using <see cref="publishRate"/>.<br/>
    /// - The ROS topic is automatically derived from the vessel (root GameObject) name and ROS namespace.<br/>
    /// - Supports ROS 1 or ROS 2 via <see cref="ROSConnection"/>.
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    public class CameraSensor : MonoBehaviour
    {
        // -------------------------------------------------------------------------------------
        #region Inspector Fields

        [Header("Camera Settings")]
        [Tooltip("Camera used to capture RGB images.")]
        public Camera snapCam;

        [Tooltip("Width of the captured image in pixels.")]
        public int imageWidth = 640;

        [Tooltip("Height of the captured image in pixels.")]
        public int imageHeight = 480;

        [Header("ROS Settings")]
        [Tooltip("Interval (in seconds) between image publications.")]
        [SerializeField] private float publishRate = 0.1f; // 10 FPS

        #endregion
        // -------------------------------------------------------------------------------------

        #region Private Fields

        private RosInterface rosInterface;
        private ROSConnection rosConnection;
        private bool isRosInitialized = false;

        private Texture2D snapshot;
        private float timeElapsed;
        private string vesselName;
        private string topicName;

        #endregion
        // -------------------------------------------------------------------------------------

        #region Unity Lifecycle

        private void Awake()
        {
            rosInterface = RosInterface.Instance;
            if (rosInterface == null)
            {
                Debug.LogError("[CameraSensor] RosInterface instance is null!");
                enabled = false;
                return;
            }

            // Automatically get camera if not assigned
            if (snapCam == null)
                snapCam = GetComponentInChildren<Camera>();

            Debug.Log($"[CameraSensor] Awake - Using camera: {snapCam}");
        }

        private void Start()
        {
            if (snapCam == null)
            {
                Debug.LogError("[CameraSensor] No camera assigned for image capture!");
                enabled = false;
                return;
            }

            // Determine vessel name (from root GameObject)
            vesselName = transform.root.name;

            // Retrieve ROS namespace
            string ns = RosInterface.Instance?.RosNamespace;
            topicName = !string.IsNullOrEmpty(ns)
                ? $"{ns}/{vesselName}/camera"
                : $"/{vesselName}/camera";

            Debug.Log($"[CameraSensor] Camera topic set to: {topicName}");
        }

        private void Update()
        {
            // Skip if ROS not available
            if (rosInterface == null || !rosInterface.IsConnected)
            {
                if (isRosInitialized)
                {
                    Debug.LogWarning("[CameraSensor] ROS connection lost.");
                    ResetRosConnection();
                }
                return;
            }

            // Initialize connection if needed
            if (rosConnection == null)
                rosConnection = rosInterface.RosInstance;

            if (!isRosInitialized)
                InitializeRosPublisher();

            // Publish at defined rate
            timeElapsed += Time.deltaTime;
            if (timeElapsed >= publishRate)
            {
                CaptureAndSendImage();
                timeElapsed = 0f;
            }
        }

        #endregion
        // -------------------------------------------------------------------------------------

        #region ROS Integration

        /// <summary>
        /// Registers this camera as an Image publisher on its topic.
        /// </summary>
        private void InitializeRosPublisher()
        {
            if (rosConnection == null) return;

            rosConnection.RegisterPublisher<ImageMsg>(topicName);
            isRosInitialized = true;
            Debug.Log($"[CameraSensor] Registered ImageMsg publisher on topic: {topicName}");
        }

        /// <summary>
        /// Resets connection flags when ROS disconnects.
        /// </summary>
        private void ResetRosConnection()
        {
            isRosInitialized = false;
            rosConnection = null;
        }

        #endregion
        // -------------------------------------------------------------------------------------

        #region Image Capture

        /// <summary>
        /// Captures the camera’s current frame and publishes it as a ROS <see cref="ImageMsg"/>.
        /// </summary>
        private void CaptureAndSendImage()
        {
            // Prepare render target
            RenderTexture renderTex = new RenderTexture(imageWidth, imageHeight, 24);
            snapCam.targetTexture = renderTex;
            RenderTexture.active = renderTex;

            // Render and read pixels into Texture2D
            snapshot = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
            snapCam.Render();
            snapshot.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
            snapshot.Apply();

            byte[] imageData = snapshot.GetRawTextureData();

            // Build ROS Image message
            ImageMsg imgMsg = new ImageMsg
            {
                header = new HeaderMsg
                {
                    stamp = new TimeMsg(0, 0),
                    frame_id = $"{vesselName}/camera"
                },
                height = (uint)imageHeight,
                width = (uint)imageWidth,
                encoding = "rgb8",
                is_bigendian = 0,
                step = (uint)(imageWidth * 3),
                data = imageData
            };

            // Publish to ROS
            rosConnection?.Publish(topicName, imgMsg);
            Debug.Log($"[CameraSensor] Published image to {topicName} ({imageData.Length} bytes)");

            // Cleanup
            RenderTexture.active = null;
            snapCam.targetTexture = null;
            Destroy(renderTex);
            Destroy(snapshot);
        }

        #endregion
    }
}

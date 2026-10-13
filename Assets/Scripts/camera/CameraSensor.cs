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

using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
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

        // RenderTexture reutilisee : la version precedente en allouait une
        // neuve, plus une Texture2D, a chaque capture -- dix fois par seconde.
        private RenderTexture renderTex;
        private bool captureInFlight;
        // Les images pretes reviennent par cette file, vidée depuis Update :
        // rien de cette chaine ne doit faire attendre LOTUSim, sinon les FPS
        // tombent et la simulation n'est plus temps reel.
        private readonly ConcurrentQueue<byte[]> pendingFrames = new ConcurrentQueue<byte[]>();
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

            // Determine vessel name (from root GameObject), sanitized for ROS topic rules
            // ROS topic names may only contain alphanumerics and '_'
            vesselName = System.Text.RegularExpressions.Regex.Replace(transform.root.name, @"[^A-Za-z0-9_]", "_");

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
            {
                InitializeRosPublisher();
                return; // give the endpoint a frame to process the registration
            }

            DrainPendingFrames();

            // Publish at defined rate
            timeElapsed += Time.deltaTime;
            if (timeElapsed >= publishRate && !captureInFlight)
            {
                StartCoroutine(CaptureAndSendImage());
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
        private IEnumerator CaptureAndSendImage()
        {
            captureInFlight = true;

            // Toute la charge GPU se fait en fin de frame. Capturer depuis
            // Update() rend la camera pendant que la frame est encore en cours
            // et fait courir le rendu manuel contre le thread de rendu : c'est
            // la cause du SIGSEGV deja rencontre ailleurs dans ce projet.
            yield return new WaitForEndOfFrame();

            if (renderTex == null)
            {
                renderTex = new RenderTexture(imageWidth, imageHeight, 24, RenderTextureFormat.ARGB32);
                renderTex.Create();
            }

            RenderTexture prevTarget = snapCam.targetTexture;
            snapCam.targetTexture = renderTex;
            snapCam.Render();
            snapCam.targetTexture = prevTarget;

            // Lecture asynchrone : aucune attente du GPU.
            AsyncGPUReadback.Request(renderTex, 0, TextureFormat.RGB24, OnCaptureReadback);
        }

        private void OnCaptureReadback(AsyncGPUReadbackRequest request)
        {
            captureInFlight = false;

            if (request.hasError)
            {
                Debug.LogError("[CameraSensor] AsyncGPUReadback failed.");
                return;
            }

            pendingFrames.Enqueue(request.GetData<byte>().ToArray());
        }

        // Envoi ROS : au main thread, mais sans jamais attendre.
        private void DrainPendingFrames()
        {
            while (pendingFrames.TryDequeue(out byte[] imageData))
            {
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

                rosConnection?.Publish(topicName, imgMsg);
            }
        }

        private void OnDestroy()
        {
            if (renderTex != null)
            {
                renderTex.Release();
                Destroy(renderTex);
            }
        }

        #endregion
    }
}

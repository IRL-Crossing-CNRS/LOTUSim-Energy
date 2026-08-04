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
//  WindSliderController.cs
//
//  Description:
//  Controls wind vector sliders along X, Y, Z axes and publishes their values to a ROS2 topic via TCP/IP.
//  Also subscribes to that same topic, so wind changes published by another machine (or another
//  Unity client) update the local sliders and, transitively, the wind field visualizer/HUD.
//  Supports keyboard shortcuts for increment/decrement and reset.
// --------------------------------------------------------------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RosMessageTypes.Geometry;
using RosMessageTypes.Lotusim;
using Unity.Robotics.ROSTCPConnector;

namespace Lotusim
{
    /// <summary>
    /// Handles UI sliders for wind control, publishing and subscribing to <see cref="WindMsg"/>
    /// messages on a ROS2 topic so local input and remote wind changes stay in sync.
    /// </summary>
    /// <remarks>
    /// - X-axis: keys 1 (decrease) / 2 (increase)<br/>
    /// - Y-axis: keys 4 (decrease) / 5 (increase)<br/>
    /// - Z-axis: keys 7 (decrease) / 8 (increase)<br/>
    /// - Reset all axes: key 0<br/>
    /// - Sliders automatically publish values at a configurable <see cref="publishRate"/> or when changed.
    /// </remarks>
    public class WindSliderController : MonoBehaviour
    {
        // ---------------------------------------------------------------------------------
        #region Inspector Fields

        [Header("Sliders")]
        [Tooltip("Slider controlling the X-axis wind component.")]
        [SerializeField] private Slider sliderX;

        [Tooltip("Slider controlling the Y-axis wind component (ENU north/south, surface plane).")]
        [SerializeField] private Slider sliderY;

        [Tooltip("Slider controlling the Z-axis wind component (ENU up/down, height).")]
        [SerializeField] private Slider sliderZ;

        [Header("Text Displays")]
        [Tooltip("Text display for X-axis slider value.")]
        [SerializeField] private TMP_Text textX;

        [Tooltip("Text display for Y-axis slider value.")]
        [SerializeField] private TMP_Text textY;

        [Tooltip("Text display for Z-axis slider value.")]
        [SerializeField] private TMP_Text textZ;

        [Header("ROS Settings")]
        [Tooltip("Increment applied when adjusting sliders via keyboard.")]
        [SerializeField] private float increment = 1f;

        [Tooltip("ROS2 topic to publish WindMsg messages.")]
        [SerializeField] private string topicName = "/aerialWorld/wind";

        [Tooltip("Time interval (seconds) between automatic ROS2 publishes.")]
        [SerializeField] private float publishRate = 0.5f;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Private Fields

        private float prevX, prevY, prevZ;
        private float timeElapsed;

        private RosInterface rosInterface;
        private ROSConnection m_rosConnection;
        private bool isRosInitialized = false;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Public Properties

        /// <summary>
        /// Current wind vector in ENU space (X = east/west, Y = north/south, Z = up/down) as set by the sliders.
        /// Note: this is NOT Unity world space — Unity is Y-up, so consumers must swap Y/Z to get Unity world coords.
        /// </summary>
        public Vector3 CurrentWindVector => sliderX != null
            ? new Vector3(sliderX.value, sliderY.value, sliderZ.value)
            : Vector3.zero;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Unity Lifecycle

        private void Awake()
        {
            rosInterface = RosInterface.Instance;
            if (rosInterface == null)
            {
                Debug.LogError("[WindSliderController] RosInterface instance is null!");
            }
        }

        private void Start()
        {
            if (sliderX == null || sliderY == null || sliderZ == null)
            {
                Debug.LogError("[WindSliderController] One or more sliders are not assigned!");
                enabled = false;
                return;
            }

            sliderX.value = 0f;
            sliderY.value = 0f;
            sliderZ.value = 0f;

            sliderX.onValueChanged.AddListener(OnSliderChanged);
            sliderY.onValueChanged.AddListener(OnSliderChanged);
            sliderZ.onValueChanged.AddListener(OnSliderChanged);

            UpdateText();
        }

        private void Update()
        {
            if (rosInterface == null) return;

            EnsureRosConnection();

            HandleKeyboardInput();

            timeElapsed += Time.deltaTime;
            if (timeElapsed >= publishRate)
            {
                CheckAndPublishWind();
                timeElapsed = 0f;
            }
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Core Behavior

        /// <summary>
        /// Ensures ROS connection is initialized and registers publisher if needed.
        /// </summary>
        private void EnsureRosConnection()
        {
            if (!rosInterface.IsConnected)
            {
                if (isRosInitialized)
                {
                    Debug.LogWarning("[WindSliderController] ROS connection lost, resetting state.");
                    isRosInitialized = false;
                    m_rosConnection = null;
                }
                return;
            }

            if (m_rosConnection == null)
            {
                m_rosConnection = rosInterface.RosInstance;
            }

            if (!isRosInitialized)
            {
                m_rosConnection.RegisterPublisher<WindMsg>(topicName);
                rosInterface.RegisterWindSubscription(topicName);
                Debug.Log($"[WindSliderController] Registered WindMsg publisher/subscriber on {topicName}.");

                // Send initial state
                PublishWind(sliderX.value, sliderY.value, sliderZ.value);

                prevX = sliderX.value;
                prevY = sliderY.value;
                prevZ = sliderZ.value;

                isRosInitialized = true;
            }
        }

        /// <summary>
        /// Applies a <see cref="WindMsg"/> received from ROS2 (e.g. published by another machine)
        /// to the local sliders, so every client stays in sync with the shared wind state.
        /// Called by <see cref="RosInterface"/>, which owns the actual ROS subscription.
        /// </summary>
        public void OnWindMsgReceived(WindMsg msg)
        {
            if (sliderX == null || sliderY == null || sliderZ == null) return;

            float x = (float)msg.linear_velocity.x;
            float y = (float)msg.linear_velocity.y;
            float z = (float)msg.linear_velocity.z;

            // Snap away the Gazebo keep-alive epsilon (see PublishWind) so a reset-to-zero
            // doesn't echo back as a barely-nonzero vector and leave a stray arrow rendered.
            if (Mathf.Abs(x) < 0.01f && Mathf.Abs(y) < 0.01f && Mathf.Abs(z) < 0.01f)
            {
                x = 0f;
                y = 0f;
                z = 0f;
            }

            sliderX.value = x;
            sliderY.value = y;
            sliderZ.value = z;

            // Record as already in sync so the next publish tick doesn't echo
            // this value straight back onto the topic.
            prevX = sliderX.value;
            prevY = sliderY.value;
            prevZ = sliderZ.value;

            UpdateText();
        }

        private void HandleKeyboardInput()
        {
            bool updated = false;
            // Scale increment by Time.deltaTime to make it framerate independent.
            // Multiplied by 25f so an increment of 1 means ~25 units per second.
            float step = increment * Time.deltaTime * 25f;

            if (Input.GetKey(KeyBindings.WindXDec)) { sliderX.value = Mathf.Max(sliderX.minValue, sliderX.value - step); updated = true; }
            if (Input.GetKey(KeyBindings.WindXInc)) { sliderX.value = Mathf.Min(sliderX.maxValue, sliderX.value + step); updated = true; }

            if (Input.GetKey(KeyBindings.WindYDec)) { sliderY.value = Mathf.Max(sliderY.minValue, sliderY.value - step); updated = true; }
            if (Input.GetKey(KeyBindings.WindYInc)) { sliderY.value = Mathf.Min(sliderY.maxValue, sliderY.value + step); updated = true; }

            if (Input.GetKey(KeyBindings.WindZDec)) { sliderZ.value = Mathf.Max(sliderZ.minValue, sliderZ.value - step); updated = true; }
            if (Input.GetKey(KeyBindings.WindZInc)) { sliderZ.value = Mathf.Min(sliderZ.maxValue, sliderZ.value + step); updated = true; }

            if (Input.GetKey(KeyBindings.WindReset))
            {
                sliderX.value = 0f;
                sliderY.value = 0f;
                sliderZ.value = 0f;
                updated = true;
            }

            if (updated)
            {
                UpdateText();
                PublishWind(sliderX.value, sliderY.value, sliderZ.value);
                prevX = sliderX.value;
                prevY = sliderY.value;
                prevZ = sliderZ.value;
                timeElapsed = 0f; // prevent double publish
            }
        }

        private void OnSliderChanged(float _)
        {
            UpdateText();
        }

        /// <summary>
        /// Updates TMP text displays for all sliders.
        /// </summary>
        private void UpdateText()
        {
            if (textX != null) textX.text = $"X: {sliderX.value:F2}";
            if (textY != null) textY.text = $"Y: {sliderY.value:F2}";
            if (textZ != null) textZ.text = $"Z: {sliderZ.value:F2}";
        }

        /// <summary>
        /// Publishes the wind message if values have changed since last publish.
        /// </summary>
        private void CheckAndPublishWind()
        {
            float x = sliderX.value;
            float y = sliderY.value;
            float z = sliderZ.value;

            if (x != prevX || y != prevY || z != prevZ)
            {
                PublishWind(x, y, z);
                prevX = x;
                prevY = y;
                prevZ = z;
            }
        }

        /// <summary>
        /// Publishes a <see cref="WindMsg"/> with the given X, Y, Z values.
        /// </summary>
        /// <param name="x">X-axis wind component.</param>
        /// <param name="y">Y-axis wind component.</param>
        /// <param name="z">Z-axis wind component.</param>
        private void PublishWind(float x, float y, float z)
        {
            if (m_rosConnection == null)
            {
                Debug.LogWarning("[WindSliderController] ROS connection not initialized, skipping publish.");
                return;
            }

            // Gazebo WindPlugin optimization bug workaround:
            // If the wind vector is exactly zero, Gazebo's plugin sleeps to save computation.
            // If it jumps instantly from 20 to 0 (e.g. pressing the 0 reset button), it sleeps
            // BEFORE broadcasting the new 0 speed. Unity gets stuck at 20 and the turbines keep spinning.
            // By keeping it slightly non-zero, the plugin stays alive and broadcasts the drop.
            if (Mathf.Approximately(x, 0f) && Mathf.Approximately(y, 0f) && Mathf.Approximately(z, 0f))
            {
                x = 0.001f;
            }

            var msg = new WindMsg
            {
                linear_velocity = new Vector3Msg(x, y, z),
                enable_wind = true // Ensure the 0-vector is actively applied by the simulator
            };

            m_rosConnection.Publish(topicName, msg);
            Debug.Log($"[ROS2] Published WindMsg: linear=({x:F2},{y:F2},{z:F2}), enable_wind={msg.enable_wind}");
        }

        #endregion
    }
}

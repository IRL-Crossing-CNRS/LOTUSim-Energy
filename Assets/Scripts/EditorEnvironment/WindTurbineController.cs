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
//  WindTurbineController.cs
//
//  Description:
//  MonoBehaviour attached to each wind turbine GameObject in the scene.
//  Drives the existing Animator speed so the rotating_blades animation plays at the correct rate.
//
//  How it works:
//  - The existing Animator + rotating_blades clip handles the actual rotation.
//  - This script sets animator.speed as a multiplier:
//      speed = 0     → blades stopped (below cut-in or above cut-out)
//      speed = 1     → animation plays at its baked rate (nominal wind)
//      speed = N     → N times faster than the baked animation
//  - The nominal RPM is computed via TSR formula: omega = TSR * V_eff / R
//  - animator.speed is smoothly interpolated to avoid sudden jumps.
//
//  Wind data source (two paths, both supported):
//  1. Direct topic subscription (NEW): this script subscribes straight to the wind ROS2 topic
//     (default /aerialWorld/wind), the same way WindSliderController does. This bypasses any
//     mission-gated relay (e.g. a "wake" node that only computes/publishes per-turbine effective
//     wind once a mission is active) — useful for manual/testing wind control via slider or
//     ros2 topic pub, since those already publish straight onto this topic.
//  2. OnWindDataReceived(...) (LEGACY): still callable directly by RosInterface/wake if/when
//     the per-turbine wake-model pipeline is active, e.g. once a mission is running. If both
//     paths are active at once, whichever calls last wins — this is fine for now since a wake-
//     model wind value should already reflect the same underlying topic anyway.
//
//  Setup:
//  1. Attach this script to the wind turbine root GameObject (or the blades child).
//  2. The Animator component will be found automatically on this GameObject or its children.
//  3. Set 'nominalWindSpeed' to the wind speed that matches the baked animation (Speed=1).
//  4. Set 'windTopicName' to match whatever topic your wind source actually publishes to.
//
//  Physics reference:
//  omega (rad/s) = TSR x V_eff / R
//  RPM = omega * 60 / (2 * PI)
//  Cut-in: 5 m/s, Cut-out: 25 m/s
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using RosMessageTypes.Lotusim;
using Unity.Robotics.ROSTCPConnector;

namespace Lotusim
{
    /// <summary>
    /// Drives the existing Animator speed based on effective wind speed, either received directly
    /// from a ROS2 wind topic subscription or pushed in via OnWindDataReceived by another system.
    /// </summary>
    public class WindTurbineController : MonoBehaviour
    {
        // ---------------------------------------------------------------------------------
        #region Inspector Fields

        [Header("References")]
        [Tooltip("Animator on the blades child. Auto-found in children if left empty.")]
        [SerializeField] private Animator bladesAnimator;

        [Tooltip("Wind speed below which blades do not spin (cut-in speed in m/s).")]
        [SerializeField] private float cutInSpeed = 5f;

        [Tooltip("Wind speed above which blades stop for protection (cut-out speed in m/s).")]
        [SerializeField] private float cutOutSpeed = 25f;

        [Tooltip("Wind speed at which the baked animation plays at speed=1 (nominal speed). " +
                 "Used to scale animator.speed proportionally.")]
        [SerializeField] private float nominalWindSpeed = 12f;

        [Tooltip("How fast animator.speed ramps toward the target value (units per second).")]
        [SerializeField] private float smoothingSpeed = 1.5f;

        [Header("Direct ROS Wind Subscription")]
        [Tooltip("If true, this turbine subscribes directly to windTopicName for wind speed, " +
                 "bypassing any mission-gated per-turbine relay. Uses the wind vector's magnitude.")]
        [SerializeField] private bool useDirectWindSubscription = true;

        [Tooltip("ROS2 topic to subscribe to for WindMsg (same topic WindSliderController publishes to).")]
        [SerializeField] private string windTopicName = "/aerialWorld/wind";

        #endregion
        // ---------------------------------------------------------------------------------

        #region Private Fields

        // Start at 0 — blades are stopped until wind data is received from either path.
        private float m_targetAnimSpeed = 0f;
        private float m_currentAnimSpeed = 0f;
        private float m_lastWindSpeed = 0f;
        private float m_powerW = 0f;

        private RosInterface m_rosInterface;
        private ROSConnection m_rosConnection;
        private bool m_isDirectSubActive = false;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Public Properties

        /// <summary>Current effective wind speed driving this turbine (m/s).</summary>
        public float EffectiveWindSpeed => m_lastWindSpeed;

        /// <summary>Current power output in Watts.</summary>
        public float PowerW => m_powerW;

        /// <summary>Current animator speed multiplier (0 = stopped, 1 = nominal).</summary>
        public float CurrentAnimSpeed => m_currentAnimSpeed;

        /// <summary>Turbine name identifier for ROS matching. Always matches the GameObject name.</summary>
        public string TurbineName => gameObject.name;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Unity Lifecycle

        private void Awake()
        {
            if (bladesAnimator == null)
            {
                // Search this GO and all children for an Animator
                bladesAnimator = GetComponentInChildren<Animator>();
                if (bladesAnimator != null)
                    Debug.Log($"[WindTurbineController] '{gameObject.name}': auto-found Animator on '{bladesAnimator.gameObject.name}'.");
                else
                    Debug.LogWarning($"[WindTurbineController] '{gameObject.name}': no Animator found. Blade speed control will not work.");
            }

            if (useDirectWindSubscription)
            {
                m_rosInterface = RosInterface.Instance;
                if (m_rosInterface == null)
                    Debug.LogWarning($"[WindTurbineController] '{gameObject.name}': RosInterface.Instance is null, direct wind subscription disabled.");
            }
        }

        private void Update()
        {
            if (useDirectWindSubscription)
                EnsureDirectWindSubscription();

            if (bladesAnimator == null) return;

            // Smoothly interpolate the animator speed toward the target
            m_currentAnimSpeed = Mathf.MoveTowards(
                m_currentAnimSpeed,
                m_targetAnimSpeed,
                smoothingSpeed * Time.deltaTime
            );

            bladesAnimator.speed = m_currentAnimSpeed;
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Direct ROS Subscription

        /// <summary>
        /// Lazily subscribes to windTopicName once the ROS connection is available, mirroring
        /// the pattern used by WindSliderController for publishing to the same topic.
        /// </summary>
        private void EnsureDirectWindSubscription()
        {
            if (m_rosInterface == null || !m_rosInterface.IsConnected)
                return;

            if (m_isDirectSubActive) return;

            m_rosConnection = m_rosInterface.RosInstance;
            if (m_rosConnection == null) return;

            m_rosConnection.Subscribe<WindMsg>(windTopicName, OnWindMsgReceived);
            m_isDirectSubActive = true;

            Debug.Log($"[WindTurbineController] '{gameObject.name}': subscribed directly to '{windTopicName}'.");
        }

        /// <summary>
        /// Handles a raw WindMsg from the direct topic subscription. Uses the vector's
        /// magnitude as the effective wind speed driving this turbine.
        /// </summary>
        private void OnWindMsgReceived(WindMsg msg)
        {
            float x = (float)msg.linear_velocity.x;
            float y = (float)msg.linear_velocity.y;
            float z = (float)msg.linear_velocity.z;

            float windSpeed = new Vector3(x, y, z).magnitude;

            // Ignore the Gazebo keep-alive epsilon used by WindSliderController's reset-to-zero
            // workaround, so it doesn't register as a nonzero wind speed here.
            if (windSpeed < 0.01f)
                windSpeed = 0f;

            OnWindDataReceived(windSpeed, m_powerW);
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Public Interface

        /// <summary>
        /// Called by RosInterface/wake (legacy path) when a matching WindTurbineMsg is received.
        /// Also called internally by OnWindMsgReceived when using direct topic subscription.
        /// Computes the target animator speed from the effective wind speed.
        /// </summary>
        /// <param name="windSpeed">Effective wind speed in m/s.</param>
        /// <param name="powerW">Power output in Watts.</param>
        public void OnWindDataReceived(float windSpeed, float powerW)
        {
            m_lastWindSpeed = windSpeed;
            m_powerW = powerW;
            m_targetAnimSpeed = ComputeTargetAnimSpeed(windSpeed);
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Private Logic

        /// <summary>
        /// Computes the animator speed multiplier from the effective wind speed.
        /// The multiplier is relative to 'nominalWindSpeed' where speed=1.
        /// </summary>
        private float ComputeTargetAnimSpeed(float windSpeed)
        {
            // Enforce cut-in / cut-out bounds
            if (windSpeed < cutInSpeed || windSpeed > cutOutSpeed)
                return 0f;

            // TSR formula: omega = TSR * V / R  →  RPM = omega * 60 / (2PI)
            // Speed multiplier = RPM(current) / RPM(nominal)
            //                  = V_eff / V_nominal  (TSR and R cancel out)
            float speedMultiplier = windSpeed / nominalWindSpeed;
            return speedMultiplier;
        }

        #endregion
    }
}
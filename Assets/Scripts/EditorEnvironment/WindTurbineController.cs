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
//  Receives effective_wind_speed from RosInterface and drives the existing Animator speed
//  so the rotating_blades animation plays at the correct rate.
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
//  Setup:
//  1. Attach this script to the wind turbine root GameObject (or the blades child).
//  2. The Animator component will be found automatically on this GameObject or its children.
//  3. Set 'nominalWindSpeed' to the wind speed that matches the baked animation (Speed=1).
//
//  Physics reference:
//  omega (rad/s) = TSR x V_eff / R
//  RPM = omega * 60 / (2 * PI)
//  Cut-in: 5 m/s, Cut-out: 25 m/s
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;

namespace Lotusim
{
    /// <summary>
    /// Drives the existing Animator speed based on effective wind speed received from ROS2.
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

        #endregion
        // ---------------------------------------------------------------------------------

        #region Private Fields

        // Start at 0 — blades are stopped until ROS wind data is received.
        private float m_targetAnimSpeed = 0f;
        private float m_currentAnimSpeed = 0f;
        private float m_lastWindSpeed = 0f;
        private float m_powerW = 0f;

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
        }

        private void Update()
        {
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

        #region Public Interface

        /// <summary>
        /// Called by RosInterface when a matching WindTurbineMsg is received.
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

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
//  LotusimBaseInterface.cs
//
//  Description:
//  Base abstract class for all interfaces in the Lotusim system.
//  Interfaces populate pose, creation, destruction, and propeller data for vessels.
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lotusim
{
    /// <summary>
    /// Base template for all Lotusim interfaces.
    /// All Pose objects are expected to use Unity conventions (left-handed, Y-up).
    /// Interfaces populate vessel state and commands to be processed by the main simulation scripts.
    /// </summary>
    public abstract class LotusimBaseInterface
    {
        /// <summary>
        /// Maps vessel names to their current Unity poses.
        /// </summary>
        public readonly Dictionary<string, Pose> m_vesselPoses = new();

        /// <summary>
        /// Maps vessel names to prefab names and spawn poses for creation.
        /// </summary>
        public readonly Dictionary<string, Tuple<string, Pose>> m_vesselToCreate = new();

        /// <summary>
        /// List of vessel names scheduled for destruction.
        /// </summary>
        public readonly List<string> m_vesselsToDestroy = new();

        /// <summary>
        /// List of vessel names scheduled for explosion.
        /// </summary>
        public readonly List<string> m_vesselsToExplode = new();

        /// <summary>
        /// Maps thruster or propeller names to spin ratios (0 = stopped, 1 = full speed).
        /// </summary>
        public readonly Dictionary<string, float> m_propellerSpinRatios = new();

        /// <summary>
        /// Called once to initialize the interface with a namespace or configuration.
        /// </summary>
        /// <param name="ns">Namespace or identifier for the interface.</param>
        public abstract void Start(string ns);

        /// <summary>
        /// Called every Unity Update() cycle to process incoming data and update vessel states.
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// Called to clean up resources and terminate the interface.
        /// </summary>
        public abstract void Destroy();

        /// <summary>
        /// Returns the simulation interpolation ratio between Unity and the source simulator (e.g., Gazebo).
        /// Ratio = 0 freezes Unity, ratio = 1 plays at normal speed.
        /// Can be overridden by derived interfaces if interpolation is needed.
        /// </summary>
        /// <returns>Simulation time ratio.</returns>
        public virtual float GetInterpolationRatio() => 1f;
    }
}

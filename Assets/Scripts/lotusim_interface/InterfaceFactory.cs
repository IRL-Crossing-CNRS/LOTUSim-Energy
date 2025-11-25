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
//  InterfaceFactory.cs
//
//  Description:
//  Factory and driver for creating and updating Lotusim interfaces (ROS2, TCPIP, etc.)
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lotusim
{
    /// <summary>
    /// Factory class to create Lotusim interfaces (e.g., ROS2, TCPIP) dynamically.
    /// </summary>
    public static class LotusimInterfaceFactory
    {
        // Maps interface type names to their constructors
        private static readonly Dictionary<string, Func<LotusimBaseInterface>> s_interfaceMap =
            new Dictionary<string, Func<LotusimBaseInterface>>()
            {
                { "ROS2", () => RosInterface.Instance },  // ROS2 uses singleton
                { "TCPIP", () => new TCPIPInterface() }   // TCPIP can be constructed normally
            };

        /// <summary>
        /// Creates and initializes a Lotusim interface of the specified type.
        /// </summary>
        /// <param name="type">Interface type ("ROS2", "TCPIP", etc.)</param>
        /// <param name="rosNamespace">Optional namespace for ROS2 interfaces.</param>
        /// <returns>The initialized interface instance, or null if type is not found.</returns>
        public static LotusimBaseInterface CreateInterface(string type, string rosNamespace = "")
        {
            if (s_interfaceMap.TryGetValue(type.ToUpperInvariant(), out var constructor))
            {
                var iface = constructor();
                iface.Start(rosNamespace); // Important for ROS2 interface initialization
                return iface;
            }

            Debug.LogError($"[LotusimInterfaceFactory] Interface type '{type}' not found.");
            return null;
        }

        /// <summary>
        /// Returns all available interface types that can be created.
        /// </summary>
        public static IEnumerable<string> GetAvailableInterfaceTypes()
        {
            return s_interfaceMap.Keys;
        }
    }

    /// <summary>
    /// MonoBehaviour driver to update a Lotusim interface each frame.
    /// Attach to a GameObject to have automatic Update calls.
    /// </summary>
    public class InterfaceDriver : MonoBehaviour
    {
        private LotusimBaseInterface _iface;

        /// <summary>
        /// Assigns the interface to be updated by this driver.
        /// </summary>
        /// <param name="iface">Interface instance to drive.</param>
        public void Init(LotusimBaseInterface iface)
        {
            _iface = iface;
        }

        private void Update()
        {
            _iface?.Update();
        }
    }
}

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
//  common.cs
//
//  Description:
//  Utility class for converting poses between Gazebo and Unity coordinate systems.
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;

namespace Lotusim
{
    /// <summary>
    /// Provides helper methods for coordinate system conversions between Gazebo (right-handed)
    /// and Unity (left-handed, Y-up) conventions.
    /// </summary>
    public static class CoordinateSystemUtils
    {
        /// <summary>
        /// Converts a Gazebo Pose to a Unity Pose.
        /// Adjusts position and rotation axes to account for different coordinate systems.
        /// </summary>
        /// <param name="pose">The Gazebo Pose to convert.</param>
        /// <returns>The corresponding Unity Pose.</returns>
        public static Pose GzPoseToUnityPose(Pose pose)
        {
            // Position: Swap Y and Z to match Unity's Y-up convention
            Vector3 unityPosition = new Vector3(
                pose.position.x,
                pose.position.z,
                pose.position.y
            );

            // Rotation: Negate X, Y, Z components as needed for left-handed system
            Quaternion unityRotation = new Quaternion(
                -pose.rotation.x,
                -pose.rotation.z,
                -pose.rotation.y,
                pose.rotation.w
            );

            return new Pose(unityPosition, unityRotation);
        }
    }
}

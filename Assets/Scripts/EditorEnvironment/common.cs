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
//  Utility for converting ROS/Gazebo coordinate system (right-handed) to Unity (left-handed) coordinate system
// --------------------------------------------------------------------------------------------------------------------

using RosMessageTypes.Geometry;

namespace lotusim_common
{
    public static class CoordinateSystemUtils
    {
        #region Public Methods

        /// <summary>
        /// Convert right-handed coordinate system to left-handed coordinate system.
        /// GZ to Unity frame
        /// </summary>
        /// Figure out ros2 pose to Unity
        public static PoseMsg CoordinateSystemConversion(PoseMsg msg)
        {
            return new PoseMsg(
                new PointMsg(msg.position.x, msg.position.z, msg.position.y),
                new QuaternionMsg(-msg.orientation.x, -msg.orientation.z, -msg.orientation.y, msg.orientation.w)
            );
        }

        #endregion
    }
}

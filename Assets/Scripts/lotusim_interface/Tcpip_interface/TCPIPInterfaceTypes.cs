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
//  TCPIPInterfaceTypes.cs
//
//  Description:
//  Contains data structures and JSON converters for vessel info and Unity commands
//  used in TCP/IP communication in Lotusim.
// --------------------------------------------------------------------------------------------------------------------

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Lotusim
{
    namespace TCPIP
    {
        /// <summary>
        /// Stores information about a thruster attached to a vessel.
        /// </summary>
        [Serializable]
        public class ThrusterInfo
        {
            /// <summary>Name of the thruster.</summary>
            public string name;

            /// <summary>RPM value of the thruster.</summary>
            public float rpm;

            /// <summary>
            /// Constructor to initialize thruster info.
            /// </summary>
            public ThrusterInfo(string name, float rpm)
            {
                this.name = name;
                this.rpm = rpm;
            }
        }

        /// <summary>
        /// Stores information about a vessel including pose and thrusters.
        /// </summary>
        [Serializable]
        public class VesselInfo
        {
            public float time;
            public string name;
            public Vector3 position;
            public Quaternion rotation;
            public ThrusterInfo[] thrusters;

            /// <summary>
            /// Constructor to initialize vessel info.
            /// </summary>
            public VesselInfo(
                float time,
                string name,
                Vector3 position,
                Quaternion rotation,
                ThrusterInfo[] thrusters
            )
            {
                this.name = name;
                this.time = time;
                this.position = position;
                this.rotation = rotation;
                this.thrusters = thrusters;
            }
        }

        /// <summary>
        /// Wrapper class for deserializing JSON arrays of VesselInfo objects.
        /// </summary>
        [Serializable]
        public class VesselInfoArrayWrapper
        {
            public VesselInfo[] VesselsInfo;
        }

        /// <summary>
        /// Represents a command sent from Unity clients via TCP.
        /// </summary>
        [Serializable]
        public class UnityCmd
        {
            public string cmd;
            public string name;
            public Vector3 position;
            public string type;

            /// <summary>
            /// Constructor for commands without position.
            /// </summary>
            public UnityCmd(string cmd, string name, string type = "")
                : this(cmd, name, Vector3.zero, type) { }

            /// <summary>
            /// Constructor for commands with a position vector.
            /// </summary>
            public UnityCmd(string cmd, string name, Vector3 position, string type = "")
            {
                this.cmd = cmd;
                this.name = name;
                this.position = position;
                this.type = type;
            }
        }

        /// <summary>
        /// Custom JSON converter to deserialize UnityCmd objects from JSON.
        /// Handles optional pose conversion from right-handed to Unity left-handed coordinate system.
        /// </summary>
        public class UnityCmdConverter : JsonConverter<UnityCmd>
        {
            /// <summary>
            /// Reads JSON and returns a UnityCmd object.
            /// </summary>
            public override UnityCmd ReadJson(
                JsonReader reader,
                Type objectType,
                UnityCmd existingValue,
                bool hasExistingValue,
                JsonSerializer serializer
            )
            {
                JObject jsonObject = JObject.Load(reader);

                string cmd = jsonObject["cmd"].ToString();
                string name = jsonObject["name"].ToString();
                string type = jsonObject["type"] != null ? jsonObject["type"].ToString() : "";

                if (jsonObject["pose"] != null)
                {
                    JObject poseObject = (JObject)jsonObject["pose"];
                    Vector3 position = new Vector3(
                        (float)poseObject["x"],
                        (float)poseObject["y"],
                        (float)poseObject["z"]
                    );

                    // Convert from right-handed to Unity left-handed coordinate system
                    position = CoordinateSystemConversion(position);

                    return new UnityCmd(cmd, name, position, type);
                }

                return new UnityCmd(cmd, name, type);
            }

            /// <summary>
            /// Writing JSON is not implemented for UnityCmd.
            /// </summary>
            public override void WriteJson(JsonWriter writer, UnityCmd value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Converts a right-handed coordinate system vector to Unity's left-handed system.
            /// </summary>
            private Vector3 CoordinateSystemConversion(Vector3 msg)
            {
                return new Vector3(msg.x, msg.z, msg.y);
            }
        }
    }
}

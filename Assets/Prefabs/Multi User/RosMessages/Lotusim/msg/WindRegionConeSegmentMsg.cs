// Hand-written to match lotusim_msgs/WindRegionConeSegment ROS2 message definition.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

namespace RosMessageTypes.Lotusim
{
    [Serializable]
    public class WindRegionConeSegmentMsg : Message
    {
        public const string k_RosMessageName = "lotusim_msgs/WindRegionConeSegment";
        public override string RosMessageName => k_RosMessageName;

        /// <summary>Start of the segment, world ENU x/y (z unused).</summary>
        public Geometry.PointMsg origin;

        /// <summary>Downstream unit direction, x/y (z unused).</summary>
        public Geometry.Vector3Msg axis;

        /// <summary>Length of the segment along axis.</summary>
        public double length;

        /// <summary>Radius at origin.</summary>
        public double r_start;

        /// <summary>Radius at origin + length*axis.</summary>
        public double r_end;

        public WindRegionConeSegmentMsg()
        {
            this.origin = new Geometry.PointMsg();
            this.axis = new Geometry.Vector3Msg();
            this.length = 0.0;
            this.r_start = 0.0;
            this.r_end = 0.0;
        }

        public WindRegionConeSegmentMsg(Geometry.PointMsg origin, Geometry.Vector3Msg axis, double length, double r_start, double r_end)
        {
            this.origin = origin;
            this.axis = axis;
            this.length = length;
            this.r_start = r_start;
            this.r_end = r_end;
        }

        public static WindRegionConeSegmentMsg Deserialize(MessageDeserializer deserializer) => new WindRegionConeSegmentMsg(deserializer);

        private WindRegionConeSegmentMsg(MessageDeserializer deserializer)
        {
            this.origin = Geometry.PointMsg.Deserialize(deserializer);
            this.axis = Geometry.Vector3Msg.Deserialize(deserializer);
            deserializer.Read(out this.length);
            deserializer.Read(out this.r_start);
            deserializer.Read(out this.r_end);
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.origin);
            serializer.Write(this.axis);
            serializer.Write(this.length);
            serializer.Write(this.r_start);
            serializer.Write(this.r_end);
        }

        public override string ToString()
        {
            return "WindRegionConeSegmentMsg: " +
            "\norigin: " + origin.ToString() +
            "\naxis: " + axis.ToString() +
            "\nlength: " + length.ToString() +
            "\nr_start: " + r_start.ToString() +
            "\nr_end: " + r_end.ToString();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod]
#endif
        public static void Register()
        {
            MessageRegistry.Register(k_RosMessageName, Deserialize);
        }
    }
}

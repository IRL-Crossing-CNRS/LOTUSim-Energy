// Hand-written to match lotusim_msgs/WindRegionBox ROS2 message definition.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

namespace RosMessageTypes.Lotusim
{
    [Serializable]
    public class WindRegionBoxMsg : Message
    {
        public const string k_RosMessageName = "lotusim_msgs/WindRegionBox";
        public override string RosMessageName => k_RosMessageName;

        /// <summary>Region corner 1, X (world frame, metres).</summary>
        public double x1;

        /// <summary>Region corner 1, Y (world frame, metres).</summary>
        public double y1;

        /// <summary>Region corner 2, X (world frame, metres).</summary>
        public double x2;

        /// <summary>Region corner 2, Y (world frame, metres).</summary>
        public double y2;

        public WindRegionBoxMsg()
        {
            this.x1 = 0.0;
            this.y1 = 0.0;
            this.x2 = 0.0;
            this.y2 = 0.0;
        }

        public WindRegionBoxMsg(double x1, double y1, double x2, double y2)
        {
            this.x1 = x1;
            this.y1 = y1;
            this.x2 = x2;
            this.y2 = y2;
        }

        public static WindRegionBoxMsg Deserialize(MessageDeserializer deserializer) => new WindRegionBoxMsg(deserializer);

        private WindRegionBoxMsg(MessageDeserializer deserializer)
        {
            deserializer.Read(out this.x1);
            deserializer.Read(out this.y1);
            deserializer.Read(out this.x2);
            deserializer.Read(out this.y2);
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.x1);
            serializer.Write(this.y1);
            serializer.Write(this.x2);
            serializer.Write(this.y2);
        }

        public override string ToString()
        {
            return "WindRegionBoxMsg: " +
            "\nx1: " + x1.ToString() +
            "\ny1: " + y1.ToString() +
            "\nx2: " + x2.ToString() +
            "\ny2: " + y2.ToString();
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

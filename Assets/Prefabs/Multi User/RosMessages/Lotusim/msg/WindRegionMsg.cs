// Hand-written to match lotusim_msgs/WindRegion ROS2 message definition.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

namespace RosMessageTypes.Lotusim
{
    [Serializable]
    public class WindRegionMsg : Message
    {
        public const string k_RosMessageName = "lotusim_msgs/WindRegion";
        public override string RosMessageName => k_RosMessageName;

        public const byte BOX = 0;
        public const byte CONE_SEGMENT = 1;

        /// <summary>Unique identifier for this wind region.</summary>
        public string id;

        /// <summary>Which of 'box'/'cone' is valid — one of <see cref="BOX"/>/<see cref="CONE_SEGMENT"/>.</summary>
        public byte shape_type;

        /// <summary>Valid only if shape_type == BOX.</summary>
        public WindRegionBoxMsg box;

        /// <summary>Valid only if shape_type == CONE_SEGMENT.</summary>
        public WindRegionConeSegmentMsg cone;

        /// <summary>Wind velocity applied inside this region.</summary>
        public Geometry.Vector3Msg linear_velocity;

        /// <summary>Whether this region actively overrides the ambient wind.</summary>
        public bool enable_wind;

        public WindRegionMsg()
        {
            this.id = "";
            this.shape_type = BOX;
            this.box = new WindRegionBoxMsg();
            this.cone = new WindRegionConeSegmentMsg();
            this.linear_velocity = new Geometry.Vector3Msg();
            this.enable_wind = false;
        }

        public WindRegionMsg(string id, byte shape_type, WindRegionBoxMsg box, WindRegionConeSegmentMsg cone, Geometry.Vector3Msg linear_velocity, bool enable_wind)
        {
            this.id = id;
            this.shape_type = shape_type;
            this.box = box;
            this.cone = cone;
            this.linear_velocity = linear_velocity;
            this.enable_wind = enable_wind;
        }

        public static WindRegionMsg Deserialize(MessageDeserializer deserializer) => new WindRegionMsg(deserializer);

        private WindRegionMsg(MessageDeserializer deserializer)
        {
            deserializer.Read(out this.id);
            deserializer.Read(out this.shape_type);
            this.box = WindRegionBoxMsg.Deserialize(deserializer);
            this.cone = WindRegionConeSegmentMsg.Deserialize(deserializer);
            this.linear_velocity = Geometry.Vector3Msg.Deserialize(deserializer);
            deserializer.Read(out this.enable_wind);
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.id);
            serializer.Write(this.shape_type);
            serializer.Write(this.box);
            serializer.Write(this.cone);
            serializer.Write(this.linear_velocity);
            serializer.Write(this.enable_wind);
        }

        public override string ToString()
        {
            return "WindRegionMsg: " +
            "\nid: " + id +
            "\nshape_type: " + shape_type.ToString() +
            "\nbox: " + box.ToString() +
            "\ncone: " + cone.ToString() +
            "\nlinear_velocity: " + linear_velocity.ToString() +
            "\nenable_wind: " + enable_wind.ToString();
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

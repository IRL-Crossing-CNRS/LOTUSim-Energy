using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

namespace RosMessageTypes.Lotusim
{
    [Serializable]
    public class LCOEStateMsg : Message
    {
        public const string k_RosMessageName = "lotusim_msgs/LCOEState";
        public override string RosMessageName => k_RosMessageName;

        public Std.HeaderMsg header;
        public float lcoe_aud_mwh;
        public float energy_produced_kwh;
        public float cost_total_aud;
        public float cost_maintenance_aud;
        public float cost_robot_time_aud;
        public float cost_robot_energy_aud;
        public float robot_operation_time_h;
        public float robot_energy_wh;

        public LCOEStateMsg()
        {
            this.header = new Std.HeaderMsg();
            this.lcoe_aud_mwh = 0f;
            this.energy_produced_kwh = 0f;
            this.cost_total_aud = 0f;
            this.cost_maintenance_aud = 0f;
            this.cost_robot_time_aud = 0f;
            this.cost_robot_energy_aud = 0f;
            this.robot_operation_time_h = 0f;
            this.robot_energy_wh = 0f;
        }

        public LCOEStateMsg(Std.HeaderMsg header, float lcoe_aud_mwh, float energy_produced_kwh, float cost_total_aud, float cost_maintenance_aud, float cost_robot_time_aud, float cost_robot_energy_aud, float robot_operation_time_h, float robot_energy_wh)
        {
            this.header = header;
            this.lcoe_aud_mwh = lcoe_aud_mwh;
            this.energy_produced_kwh = energy_produced_kwh;
            this.cost_total_aud = cost_total_aud;
            this.cost_maintenance_aud = cost_maintenance_aud;
            this.cost_robot_time_aud = cost_robot_time_aud;
            this.cost_robot_energy_aud = cost_robot_energy_aud;
            this.robot_operation_time_h = robot_operation_time_h;
            this.robot_energy_wh = robot_energy_wh;
        }

        public static LCOEStateMsg Deserialize(MessageDeserializer deserializer) => new LCOEStateMsg(deserializer);

        private LCOEStateMsg(MessageDeserializer deserializer)
        {
            this.header = Std.HeaderMsg.Deserialize(deserializer);
            deserializer.Read(out this.lcoe_aud_mwh);
            deserializer.Read(out this.energy_produced_kwh);
            deserializer.Read(out this.cost_total_aud);
            deserializer.Read(out this.cost_maintenance_aud);
            deserializer.Read(out this.cost_robot_time_aud);
            deserializer.Read(out this.cost_robot_energy_aud);
            deserializer.Read(out this.robot_operation_time_h);
            deserializer.Read(out this.robot_energy_wh);
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.header);
            serializer.Write(this.lcoe_aud_mwh);
            serializer.Write(this.energy_produced_kwh);
            serializer.Write(this.cost_total_aud);
            serializer.Write(this.cost_maintenance_aud);
            serializer.Write(this.cost_robot_time_aud);
            serializer.Write(this.cost_robot_energy_aud);
            serializer.Write(this.robot_operation_time_h);
            serializer.Write(this.robot_energy_wh);
        }

        public override string ToString()
        {
            return "LCOEStateMsg: " +
            "\nheader: " + header.ToString() +
            "\nlcoe_aud_mwh: " + lcoe_aud_mwh.ToString() +
            "\nenergy_produced_kwh: " + energy_produced_kwh.ToString() +
            "\ncost_total_aud: " + cost_total_aud.ToString() +
            "\ncost_maintenance_aud: " + cost_maintenance_aud.ToString() +
            "\ncost_robot_time_aud: " + cost_robot_time_aud.ToString() +
            "\ncost_robot_energy_aud: " + cost_robot_energy_aud.ToString() +
            "\nrobot_operation_time_h: " + robot_operation_time_h.ToString() +
            "\nrobot_energy_wh: " + robot_energy_wh.ToString();
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

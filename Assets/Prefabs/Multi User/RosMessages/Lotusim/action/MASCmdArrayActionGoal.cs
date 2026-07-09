using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using RosMessageTypes.Std;
using RosMessageTypes.Actionlib;

namespace RosMessageTypes.Lotusim
{
    public class MASCmdArrayActionGoal : ActionGoal<MASCmdArrayGoal>
    {
        public const string k_RosMessageName = "lotusim_msgs/MASCmdArrayActionGoal";
        public override string RosMessageName => k_RosMessageName;


        public MASCmdArrayActionGoal() : base()
        {
            this.goal = new MASCmdArrayGoal();
        }

        public MASCmdArrayActionGoal(HeaderMsg header, GoalIDMsg goal_id, MASCmdArrayGoal goal) : base(header, goal_id)
        {
            this.goal = goal;
        }
        public static MASCmdArrayActionGoal Deserialize(MessageDeserializer deserializer) => new MASCmdArrayActionGoal(deserializer);

        MASCmdArrayActionGoal(MessageDeserializer deserializer) : base(deserializer)
        {
            this.goal = MASCmdArrayGoal.Deserialize(deserializer);
        }
        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.header);
            serializer.Write(this.goal_id);
            serializer.Write(this.goal);
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

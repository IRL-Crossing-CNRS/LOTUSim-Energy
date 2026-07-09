using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using RosMessageTypes.Std;
using RosMessageTypes.Actionlib;

namespace RosMessageTypes.Lotusim
{
    public class MASCmdActionGoal : ActionGoal<MASCmdGoal>
    {
        public const string k_RosMessageName = "lotusim_msgs/MASCmdActionGoal";
        public override string RosMessageName => k_RosMessageName;


        public MASCmdActionGoal() : base()
        {
            this.goal = new MASCmdGoal();
        }

        public MASCmdActionGoal(HeaderMsg header, GoalIDMsg goal_id, MASCmdGoal goal) : base(header, goal_id)
        {
            this.goal = goal;
        }
        public static MASCmdActionGoal Deserialize(MessageDeserializer deserializer) => new MASCmdActionGoal(deserializer);

        MASCmdActionGoal(MessageDeserializer deserializer) : base(deserializer)
        {
            this.goal = MASCmdGoal.Deserialize(deserializer);
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

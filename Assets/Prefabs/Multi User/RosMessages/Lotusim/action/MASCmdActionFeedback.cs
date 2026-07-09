using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using RosMessageTypes.Std;
using RosMessageTypes.Actionlib;

namespace RosMessageTypes.Lotusim
{
    public class MASCmdActionFeedback : ActionFeedback<MASCmdFeedback>
    {
        public const string k_RosMessageName = "lotusim_msgs/MASCmdActionFeedback";
        public override string RosMessageName => k_RosMessageName;


        public MASCmdActionFeedback() : base()
        {
            this.feedback = new MASCmdFeedback();
        }

        public MASCmdActionFeedback(HeaderMsg header, GoalStatusMsg status, MASCmdFeedback feedback) : base(header, status)
        {
            this.feedback = feedback;
        }
        public static MASCmdActionFeedback Deserialize(MessageDeserializer deserializer) => new MASCmdActionFeedback(deserializer);

        MASCmdActionFeedback(MessageDeserializer deserializer) : base(deserializer)
        {
            this.feedback = MASCmdFeedback.Deserialize(deserializer);
        }
        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.header);
            serializer.Write(this.status);
            serializer.Write(this.feedback);
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

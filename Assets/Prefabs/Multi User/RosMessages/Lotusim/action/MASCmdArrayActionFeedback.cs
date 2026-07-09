using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using RosMessageTypes.Std;
using RosMessageTypes.Actionlib;

namespace RosMessageTypes.Lotusim
{
    public class MASCmdArrayActionFeedback : ActionFeedback<MASCmdArrayFeedback>
    {
        public const string k_RosMessageName = "lotusim_msgs/MASCmdArrayActionFeedback";
        public override string RosMessageName => k_RosMessageName;


        public MASCmdArrayActionFeedback() : base()
        {
            this.feedback = new MASCmdArrayFeedback();
        }

        public MASCmdArrayActionFeedback(HeaderMsg header, GoalStatusMsg status, MASCmdArrayFeedback feedback) : base(header, status)
        {
            this.feedback = feedback;
        }
        public static MASCmdArrayActionFeedback Deserialize(MessageDeserializer deserializer) => new MASCmdArrayActionFeedback(deserializer);

        MASCmdArrayActionFeedback(MessageDeserializer deserializer) : base(deserializer)
        {
            this.feedback = MASCmdArrayFeedback.Deserialize(deserializer);
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

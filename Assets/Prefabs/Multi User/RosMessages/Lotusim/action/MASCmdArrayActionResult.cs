using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using RosMessageTypes.Std;
using RosMessageTypes.Actionlib;

namespace RosMessageTypes.Lotusim
{
    public class MASCmdArrayActionResult : ActionResult<MASCmdArrayResult>
    {
        public const string k_RosMessageName = "lotusim_msgs/MASCmdArrayActionResult";
        public override string RosMessageName => k_RosMessageName;


        public MASCmdArrayActionResult() : base()
        {
            this.result = new MASCmdArrayResult();
        }

        public MASCmdArrayActionResult(HeaderMsg header, GoalStatusMsg status, MASCmdArrayResult result) : base(header, status)
        {
            this.result = result;
        }
        public static MASCmdArrayActionResult Deserialize(MessageDeserializer deserializer) => new MASCmdArrayActionResult(deserializer);

        MASCmdArrayActionResult(MessageDeserializer deserializer) : base(deserializer)
        {
            this.result = MASCmdArrayResult.Deserialize(deserializer);
        }
        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.header);
            serializer.Write(this.status);
            serializer.Write(this.result);
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

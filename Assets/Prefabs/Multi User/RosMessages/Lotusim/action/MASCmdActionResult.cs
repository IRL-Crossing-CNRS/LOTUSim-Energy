using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using RosMessageTypes.Std;
using RosMessageTypes.Actionlib;

namespace RosMessageTypes.Lotusim
{
    public class MASCmdActionResult : ActionResult<MASCmdResult>
    {
        public const string k_RosMessageName = "lotusim_msgs/MASCmdActionResult";
        public override string RosMessageName => k_RosMessageName;


        public MASCmdActionResult() : base()
        {
            this.result = new MASCmdResult();
        }

        public MASCmdActionResult(HeaderMsg header, GoalStatusMsg status, MASCmdResult result) : base(header, status)
        {
            this.result = result;
        }
        public static MASCmdActionResult Deserialize(MessageDeserializer deserializer) => new MASCmdActionResult(deserializer);

        MASCmdActionResult(MessageDeserializer deserializer) : base(deserializer)
        {
            this.result = MASCmdResult.Deserialize(deserializer);
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

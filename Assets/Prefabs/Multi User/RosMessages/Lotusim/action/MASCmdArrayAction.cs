using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;


namespace RosMessageTypes.Lotusim
{
    public class MASCmdArrayAction : Action<MASCmdArrayActionGoal, MASCmdArrayActionResult, MASCmdArrayActionFeedback, MASCmdArrayGoal, MASCmdArrayResult, MASCmdArrayFeedback>
    {
        public const string k_RosMessageName = "lotusim_msgs/MASCmdArrayAction";
        public override string RosMessageName => k_RosMessageName;


        public MASCmdArrayAction() : base()
        {
            this.action_goal = new MASCmdArrayActionGoal();
            this.action_result = new MASCmdArrayActionResult();
            this.action_feedback = new MASCmdArrayActionFeedback();
        }

        public static MASCmdArrayAction Deserialize(MessageDeserializer deserializer) => new MASCmdArrayAction(deserializer);

        MASCmdArrayAction(MessageDeserializer deserializer)
        {
            this.action_goal = MASCmdArrayActionGoal.Deserialize(deserializer);
            this.action_result = MASCmdArrayActionResult.Deserialize(deserializer);
            this.action_feedback = MASCmdArrayActionFeedback.Deserialize(deserializer);
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.action_goal);
            serializer.Write(this.action_result);
            serializer.Write(this.action_feedback);
        }

    }
}

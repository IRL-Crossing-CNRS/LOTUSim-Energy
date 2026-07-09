using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;


namespace RosMessageTypes.Lotusim
{
    public class MASCmdAction : Action<MASCmdActionGoal, MASCmdActionResult, MASCmdActionFeedback, MASCmdGoal, MASCmdResult, MASCmdFeedback>
    {
        public const string k_RosMessageName = "lotusim_msgs/MASCmdAction";
        public override string RosMessageName => k_RosMessageName;


        public MASCmdAction() : base()
        {
            this.action_goal = new MASCmdActionGoal();
            this.action_result = new MASCmdActionResult();
            this.action_feedback = new MASCmdActionFeedback();
        }

        public static MASCmdAction Deserialize(MessageDeserializer deserializer) => new MASCmdAction(deserializer);

        MASCmdAction(MessageDeserializer deserializer)
        {
            this.action_goal = MASCmdActionGoal.Deserialize(deserializer);
            this.action_result = MASCmdActionResult.Deserialize(deserializer);
            this.action_feedback = MASCmdActionFeedback.Deserialize(deserializer);
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.action_goal);
            serializer.Write(this.action_result);
            serializer.Write(this.action_feedback);
        }

    }
}

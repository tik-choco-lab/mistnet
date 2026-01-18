namespace MistNet.Evaluation
{
    public interface IEvalMessageSender
    {
        void Send(EvalMessageType type, object message);
        void RegisterReceive(EvalMessageType type, EvalMessageReceivedHandler receiver);
    }

    public delegate void EvalMessageReceivedHandler(string payload);
}

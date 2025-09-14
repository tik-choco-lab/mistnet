namespace MistNet.DNVE2
{
    public interface IDNVE2MessageSender
    {
        void Send(NodeId targetId, DNVE2Message message);
    }
}

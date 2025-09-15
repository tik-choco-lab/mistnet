namespace MistNet.DNVE2
{
    public interface IDNVE2MessageSender
    {
        void Send(DNVE2Message message);
        void RegisterReceive(DNVE2MessageType type, DNVE2MessageReceivedHandler receiver);
        void RegisterOnConnected(DNVE2OnConnectedHandler handler);
    }

    public delegate void DNVE2MessageReceivedHandler(DNVE2Message message);
    public delegate void DNVE2OnConnectedHandler(NodeId id);
}

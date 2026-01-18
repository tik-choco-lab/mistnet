namespace MistNet
{
    public interface IMessageSender
    {
        void Send(DNVEMessage message);
        void RegisterReceive(DNVEMessageType type, DNVEMessageReceivedHandler receiver);
        void RegisterOnConnected(DNVEOnConnectedHandler handler);
    }

    public delegate void DNVEMessageReceivedHandler(DNVEMessage message);
    public delegate void DNVEOnConnectedHandler(NodeId id);
}

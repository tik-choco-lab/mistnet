namespace MistNet
{
    public interface IDNVE1MessageSender
    {
        void Send(NodeId targetId, KademliaMessage message);
        void RegisterReceive(KademliaMessageType type, DNVE1MessageReceivedHandler receiver);
        void RegisterReceive(KademliaMessageType type, DNVE1MessageReceivedHandlerWithFromId receiver);
    }

    public delegate void DNVE1MessageReceivedHandler(KademliaMessage message);
    public delegate void DNVE1MessageReceivedHandlerWithFromId(KademliaMessage message, NodeId fromId);
}

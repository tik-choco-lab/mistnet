namespace MistNet
{
    public class KademliaMessage
    {
        public NodeInfo Sender;
        public KademliaMessageType Type;
        public string Payload;
    }

    public enum KademliaMessageType
    {
        Ping,
        Pong,
        Store,
        FindNode,
        FindValue,
        ResponseNode,
        ResponseValue,
    }
}

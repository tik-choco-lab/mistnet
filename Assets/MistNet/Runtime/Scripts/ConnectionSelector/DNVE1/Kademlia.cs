using System;

namespace MistNet
{
    public class Kademlia
    {
        private readonly Action<NodeId, string> _send;

        public Kademlia(Action<NodeId, string> send)
        {
            _send = send;
        }

        public void Ping(NodeId id)
        {
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.Ping,
            };

            _send?.Invoke(id, message.Payload);
        }

        public void Store(NodeId id, string key, string value)
        {
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.Store,
                Payload = $"{key}:{value}"
            };

            _send?.Invoke(id, message.Payload);
        }

        public void FindNode(NodeId id, NodeId target)
        {
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.FindNode,
                Payload = target.ToString()
            };

            _send?.Invoke(id, message.Payload);
        }

        public void FindValue(NodeId id, string key)
        {
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.FindValue,
                Payload = key
            };

            _send?.Invoke(id, message.Payload);
        }
    }
}

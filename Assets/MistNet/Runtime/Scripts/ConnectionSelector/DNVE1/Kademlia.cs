using System;

namespace MistNet
{
    public class Kademlia
    {
        private readonly Action<NodeInfo, KademliaMessage> _send;

        public Kademlia(Action<NodeInfo, KademliaMessage> send)
        {
            _send = send;
        }

        public void Ping(NodeInfo id)
        {
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.Ping,
            };

            _send?.Invoke(id, message);
        }

        public void Store(NodeInfo id, byte[] key, string value)
        {
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.Store,
                Payload = $"{Convert.ToBase64String(key)}:{value}"
            };

            _send?.Invoke(id, message);
        }

        public void FindNode(NodeInfo id, byte[] target)
        {
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.FindNode,
                Payload = Convert.ToBase64String(target)
            };

            _send?.Invoke(id, message);
        }

        public void FindValue(NodeInfo id, string key)
        {
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.FindValue,
                Payload = key
            };

            _send?.Invoke(id, message);
        }
    }
}

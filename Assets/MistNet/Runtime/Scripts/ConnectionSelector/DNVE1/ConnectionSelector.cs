using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MistNet
{
    public class ConnectionSelector : IConnectionSelector
    {
        private readonly Kademlia _kademlia;
        private readonly Dictionary<KademliaMessageType, Action<string, NodeId>> _onMessageReceived = new();

        public ConnectionSelector()
        {
            _kademlia = new Kademlia(Send);
        }

        protected override void OnMessage(string data, NodeId id)
        {
            var message = JsonConvert.DeserializeObject<KademliaMessage>(data);
            _onMessageReceived[message.Type]?.Invoke(message.Payload, id);
        }

        private void Send(NodeId id, string payload)
        {
            // _kademlia.Send(id, message);
        }

        private void Init()
        {
            var position = MistSyncManager.I.SelfSyncObject.transform.position;
            var chunk = new Area(position);
            // _kademlia.FindNode(chunk.GetId());
        }
    }
}

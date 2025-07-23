using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MistNet
{
    public class ConnectionSelector : IConnectionSelector
    {
        private readonly Kademlia _kademlia;
        private readonly Dictionary<KademliaMessageType, Action<string, NodeInfo>> _onMessageReceived = new();
        private readonly KademliaRoutingTable _routingTable;
        private readonly KademliaDataStore _dataStore;

        public ConnectionSelector()
        {
            _kademlia = new Kademlia(SendInternal);
            _routingTable = new KademliaRoutingTable(_kademlia);
            _dataStore = new KademliaDataStore();

            _onMessageReceived[KademliaMessageType.Ping] = OnPing;
            _onMessageReceived[KademliaMessageType.Store] = OnStore;
            _onMessageReceived[KademliaMessageType.FindNode] = OnFindNode;
            _onMessageReceived[KademliaMessageType.FindValue] = OnFindValue;
        }

        protected override void OnMessage(string data, NodeId id)
        {
            var message = JsonConvert.DeserializeObject<KademliaMessage>(data);
            _routingTable.AddNode(message.Sender);
            _onMessageReceived[message.Type]?.Invoke(message.Payload, message.Sender);
        }

        private void SendInternal(NodeInfo node, KademliaMessage message)
        {
            message.Sender = _routingTable.SelfNode;
            Send(JsonConvert.SerializeObject(message), node.Id);
        }

        private void Init()
        {
            var position = MistSyncManager.I.SelfSyncObject.transform.position;
            var chunk = new Area(position);
            // _kademlia.FindNode(chunk.GetId());
        }

        private void OnPing(string payload, NodeInfo sender)
        {
            var response = new KademliaMessage
            {
                Type = KademliaMessageType.Pong,
                Payload = ""
            };
            SendInternal(sender, response);
        }

        private void OnFindNode(string payload, NodeInfo sender)
        {
            var target = Convert.FromBase64String(payload);
            SendClosestNodes(sender, target);
        }

        private void OnStore(string payload, NodeInfo sender)
        {
            var parts = payload.Split(':');
            if (parts.Length == 2)
            {
                var key = Convert.FromBase64String(parts[0]);
                var value = parts[1];
                _dataStore.Store(key, value);
                SendValue(sender, value);
            }
        }

        private void OnFindValue(string payload, NodeInfo sender)
        {
            var targetKey = Convert.FromBase64String(payload);
            if (_dataStore.TryGetValue(targetKey, out var value))
            {
                SendValue(sender, value);
            }
            else
            {
                SendClosestNodes(sender, targetKey);
            }
        }

        private void SendValue(NodeInfo sender, string value)
        {
            var response = new KademliaMessage
            {
                Type = KademliaMessageType.ResponseValue,
                Payload = value
            };
            SendInternal(sender, response);
        }

        private void SendClosestNodes(NodeInfo sender, byte[] target)
        {
            var closestNodes = _routingTable.FindClosestNodes(target);

            foreach (var node in closestNodes)
            {
                var response = new KademliaMessage
                {
                    Type = KademliaMessageType.ResponseNode,
                    Payload = JsonConvert.SerializeObject(node)
                };
                SendInternal(sender, response);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MistNet
{
    public class KademliaController : IConnectionSelector
    {
        private readonly Kademlia _kademlia;
        private readonly Dictionary<KademliaMessageType, Action<KademliaMessage>> _onMessageReceived = new();
        private readonly KademliaRoutingTable _routingTable;
        private readonly KademliaDataStore _dataStore;

        public KademliaController()
        {
            _dataStore = new KademliaDataStore();
            _routingTable = new KademliaRoutingTable();
            _kademlia = new Kademlia(SendInternal, _dataStore, _routingTable);

            _onMessageReceived[KademliaMessageType.ResponseNode] = OnFindNodeResponse;
            _onMessageReceived[KademliaMessageType.ResponseValue] = OnFindValueResponse;
        }

        protected override void OnMessage(string data, NodeId id)
        {
            var message = JsonConvert.DeserializeObject<KademliaMessage>(data);
            _routingTable.AddNode(message.Sender);

            if (_onMessageReceived.TryGetValue(message.Type, out var handler))
            {
                handler(message);
            }
            else _kademlia.OnMessage(message);
        }

        private void SendInternal(NodeInfo node, KademliaMessage message)
        {
            message.Sender = _routingTable.SelfNode;
            Send(JsonConvert.SerializeObject(message), node.Id);
        }

        private void OnFindNodeResponse(KademliaMessage message)
        {
            var closestNodes = JsonConvert.DeserializeObject<ResponseFindNode>(message.Payload);
            var target = closestNodes.Target;
            if (closestNodes.Nodes.Count < 5)
            {
                // OK
            }
            else
            {
                // さらに絞り込む
                FindNode(closestNodes.Nodes, target);
            }
        }

        private void OnFindValueResponse(KademliaMessage message)
        {
            var response = JsonConvert.DeserializeObject<ResponseFindValue>(message.Payload);
            if (response.Value != null)
            {
                _dataStore.Store(response.Target, response.Value);
            }
            else
            {
                // さらに検索する
                FindValue(response.Nodes, response.Target);
            }
        }

        public void FindValue(List<NodeInfo> closestNodes, byte[] target)
        {
            foreach (var node in closestNodes)
            {
                _kademlia.FindValue(node, target);
            }
        }

        public void FindNode(List<NodeInfo> closestNodes, byte[] target)
        {
            foreach (var node in closestNodes)
            {
                _kademlia.FindNode(node, target);
            }
        }
    }
}

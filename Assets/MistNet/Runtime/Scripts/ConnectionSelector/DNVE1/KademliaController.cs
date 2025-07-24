using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MistNet
{
    public class KademliaController : IConnectionSelector, IDisposable
    {
        private readonly Kademlia _kademlia;
        private readonly Dictionary<KademliaMessageType, Action<KademliaMessage>> _onMessageReceived = new();
        private readonly KademliaRoutingTable _routingTable;
        private readonly KademliaDataStore _dataStore;
        private readonly AreaTracker _areaTracker;
        private readonly IRouting _routing;
        private readonly ConnectionBalancer _connectionBalancer;

        public KademliaController()
        {
            _dataStore = new KademliaDataStore();
            _routingTable = new KademliaRoutingTable();
            _kademlia = new Kademlia(SendInternal, _dataStore, _routingTable);
            _areaTracker = new AreaTracker(_kademlia, _dataStore, _routingTable, this);
            _routing = MistManager.I.routing;
            _connectionBalancer = new ConnectionBalancer(_dataStore, _areaTracker);

            _onMessageReceived[KademliaMessageType.ResponseNode] = OnFindNodeResponse;
            _onMessageReceived[KademliaMessageType.ResponseValue] = OnFindValueResponse;
        }

        protected override void OnMessage(string data, NodeId id)
        {
            var message = JsonConvert.DeserializeObject<KademliaMessage>(data);
            _routingTable.AddNode(message.Sender);
            _routing.Add(message.Sender.Id, id);

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
            foreach (var node in closestNodes.Nodes)
            {
                _routingTable.AddNode(node);
            }

            if (closestNodes.Nodes.Count < 5)
            {
                // OK 既にroutingTableに登録されている
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

        public void Dispose()
        {
            _areaTracker?.Dispose();
            _connectionBalancer?.Dispose();
        }
    }
}

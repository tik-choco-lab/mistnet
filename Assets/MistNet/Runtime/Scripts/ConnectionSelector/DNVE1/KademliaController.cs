using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MistNet
{
    public class KademliaController : IConnectionSelector, IDisposable
    {
        private const int Alpha = 3; // Number of parallel requests
        private Kademlia _kademlia;
        private readonly Dictionary<KademliaMessageType, Action<KademliaMessage>> _onMessageReceived = new();
        private KademliaRoutingTable _routingTable;
        private KademliaDataStore _dataStore;
        private AreaTracker _areaTracker;
        private IRouting _routing;
        private ConnectionBalancer _connectionBalancer;
        private VisibleNodesController _visibleNodesController;

        protected override void Start()
        {
            OptConfigLoader.ReadConfig();

            base.Start();
            _dataStore = new KademliaDataStore();
            _routingTable = new KademliaRoutingTable();
            _kademlia = new Kademlia(SendInternal, _dataStore, _routingTable);
            _areaTracker = new AreaTracker(_kademlia, _dataStore, _routingTable, this);
            _connectionBalancer = new ConnectionBalancer(SendInternal, _dataStore, _routingTable, _areaTracker);
            _visibleNodesController = new VisibleNodesController(_connectionBalancer);
            _routing = MistManager.I.routing;

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

            _kademlia.OnMessage(message);
            _connectionBalancer.OnMessage(message);
        }

        private void SendInternal(NodeInfo node, KademliaMessage message)
        {
            SendInternal(node.Id, message);
        }

        private void SendInternal(NodeId nodeId, KademliaMessage message)
        {
            message.Sender = _routingTable.SelfNode;
            Send(JsonConvert.SerializeObject(message), nodeId);
        }

        private void OnFindNodeResponse(KademliaMessage message)
        {
            var closestNodes = JsonConvert.DeserializeObject<ResponseFindNode>(message.Payload);
            var target = closestNodes.Target;
            foreach (var node in closestNodes.Nodes)
            {
                _routingTable.AddNode(node);
            }

            if (closestNodes.Nodes.Count < KBucket.K)
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
            var count = 0;
            foreach (var node in closestNodes)
            {
                _kademlia.FindValue(node, target);
                count++;
                if (count >= Alpha) break;
            }
        }

        public void FindNode(List<NodeInfo> closestNodes, byte[] target)
        {
            var count = 0;
            foreach (var node in closestNodes)
            {
                _kademlia.FindNode(node, target);
                count++;
                if (count >= Alpha) break;
            }
        }

        public void Dispose()
        {
            _areaTracker?.Dispose();
            _connectionBalancer?.Dispose();
            _visibleNodesController?.Dispose();
        }
    }
}

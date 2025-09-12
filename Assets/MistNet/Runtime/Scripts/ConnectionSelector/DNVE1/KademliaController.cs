using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MistNet
{
    public class KademliaController : SelectorBase, IDisposable
    {
        private const int Alpha = 3; // Number of parallel requests
        private Kademlia _kademlia;
        private readonly Dictionary<KademliaMessageType, Action<KademliaMessage>> _onMessageReceived = new();
        private KademliaRoutingTable _routingTable;
        private KademliaDataStore _dataStore;
        private AreaTracker _areaTracker;
        private RoutingBase _routingBase;
        private ConnectionBalancer _connectionBalancer;
        private VisibleNodesController _visibleNodesController;

        protected override void Start()
        {
            OptConfig.ReadConfig();

            base.Start();

            _routingBase = MistManager.I.Routing;
            _dataStore = new KademliaDataStore();
            _routingTable = new KademliaRoutingTable();
            _kademlia = new Kademlia(SendInternal, _dataStore, _routingTable);
            _areaTracker = new AreaTracker(_kademlia, _dataStore, _routingTable, this);
            _connectionBalancer = new ConnectionBalancer(SendInternal, _dataStore, _routingTable, _areaTracker);
            _visibleNodesController = new VisibleNodesController(_connectionBalancer);

            _onMessageReceived[KademliaMessageType.ResponseNode] = OnFindNodeResponse;
            _onMessageReceived[KademliaMessageType.ResponseValue] = OnFindValueResponse;
        }

        protected override void OnMessage(string data, NodeId id)
        {
            var message = JsonConvert.DeserializeObject<KademliaMessage>(data);
            _routingTable.AddNode(message.Sender);
            _routingBase.Add(message.Sender.Id, id);

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
            foreach (var node in closestNodes.Nodes)
            {
                _routingTable.AddNode(node);
            }

            if (closestNodes.Nodes.Count < KBucket.K)
            {
                // OK 既にroutingTableに登録されている
                MistLogger.Debug($"[Debug][KademliaController] Found {closestNodes.Nodes.Count} nodes");
            }
        }

        private void OnFindValueResponse(KademliaMessage message)
        {
            MistLogger.Debug($"[Debug][KademliaController] Received FindValue {message.Payload} from {message.Sender.Id}");

            var response = JsonConvert.DeserializeObject<ResponseFindValue>(message.Payload);
            if (string.IsNullOrEmpty(response.Value))
            {
                MistLogger.Error(
                    $"[Error][KademliaController] No value found for target {BitConverter.ToString(response.Key)}");
                return;
            }

            MistLogger.Debug($"[Debug][KademliaController] Found value for target {BitConverter.ToString(response.Key)}: {response.Value}");
            _dataStore.Store(response.Key, response.Value);
        }

        public void FindValue(HashSet<NodeInfo> closestNodes, byte[] target)
        {
            var count = 0;
            foreach (var node in closestNodes)
            {
                _kademlia.FindValue(node, target);
                count++;
                if (count >= Alpha) break;
            }
        }

        private void FindNode(HashSet<NodeInfo> closestNodes, byte[] target)
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

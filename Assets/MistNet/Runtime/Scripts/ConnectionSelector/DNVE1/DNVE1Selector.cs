using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MistNet
{
    public class DNVE1Selector : SelectorBase, IDisposable, IDNVE1MessageSender
    {
        private const int Alpha = 3; // Number of parallel requests
        private Kademlia _kademlia;
        private KademliaRoutingTable _routingTable;
        private KademliaDataStore _dataStore;
        private AreaTracker _areaTracker;
        private ConnectionBalancer _connectionBalancer;
        private VisibleNodesController _visibleNodesController;
        private static readonly Dictionary<KademliaMessageType, DNVE1MessageReceivedHandler> Receivers = new();

        protected override void Start()
        {
            OptConfig.ReadConfig();

            base.Start();

            _dataStore = new KademliaDataStore();
            _routingTable = new KademliaRoutingTable();
            _kademlia = new Kademlia(this, _dataStore, _routingTable);
            _areaTracker = new AreaTracker(_kademlia, _dataStore, _routingTable, this);
            _connectionBalancer = new ConnectionBalancer(this, _dataStore, _routingTable, _areaTracker);
            _visibleNodesController = new VisibleNodesController(_connectionBalancer);

            RegisterReceive(KademliaMessageType.ResponseNode, OnFindNodeResponse);
            RegisterReceive(KademliaMessageType.ResponseValue, OnFindValueResponse);
        }

        protected override void OnMessage(string data, NodeId id)
        {
            var message = JsonConvert.DeserializeObject<KademliaMessage>(data);
            _routingTable.AddNode(message.Sender);

            if (Receivers.TryGetValue(message.Type, out var handler))
            {
                handler(message);
            }
        }

        public void Send(NodeId targetId, KademliaMessage message)
        {
            SendInternal(targetId, message);
        }

        private void SendInternal(NodeId nodeId, KademliaMessage message)
        {
            message.Sender = _routingTable.SelfNode;
            Send(JsonConvert.SerializeObject(message), nodeId);
        }

        public void RegisterReceive(KademliaMessageType type, DNVE1MessageReceivedHandler receiver)
        {
            Receivers[type] = receiver;
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

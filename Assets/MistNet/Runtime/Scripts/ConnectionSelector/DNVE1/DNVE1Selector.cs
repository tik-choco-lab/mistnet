using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MistNet
{
    public class DNVE1Selector : SelectorBase, IDNVE1MessageSender
    {
        private const int Alpha = 3; // Number of parallel requests
        private Kademlia _kademlia;
        private KademliaRoutingTable _routingTable;
        private KademliaDataStore _dataStore;
        private AreaTracker _areaTracker;
        private ConnectionBalancer _connectionBalancer;
        private VisibleNodesController _visibleNodesController;
        private static readonly Dictionary<KademliaMessageType, DNVE1MessageReceivedHandler> Receivers = new();
        private static readonly Dictionary<KademliaMessageType, DNVE1MessageReceivedHandlerWithFromId> ReceiversWithId = new();
        private RoutingBase _routingBase;

        protected override void Start()
        {
            OptConfig.ReadConfig();

            base.Start();

            _dataStore = new KademliaDataStore();
            _routingTable = new KademliaRoutingTable();
            _routingBase = MistManager.I.Routing;
            _kademlia = new Kademlia(this, _dataStore, _routingTable);
            _areaTracker = new AreaTracker(_kademlia, _routingTable, this);
            _connectionBalancer = new ConnectionBalancer(this, _dataStore, _routingTable, _areaTracker);
            _visibleNodesController = new VisibleNodesController(_connectionBalancer);

            RegisterReceive(KademliaMessageType.ResponseNode, OnFindNodeResponse);
            RegisterReceive(KademliaMessageType.ResponseValue, OnFindValueResponse);

            MistManager.I.World.AddSendFailedCallback((Action<NodeId>) SendFailed);
        }

        private void SendFailed(NodeId id)
        {
            _routingTable.RemoveNode(id);
        }

        protected override void OnMessage(string data, NodeId id)
        {
            var message = JsonConvert.DeserializeObject<KademliaMessage>(data);
            _routingTable.AddNode(message.Sender);

            if (Receivers.TryGetValue(message.Type, out var handler))
            {
                handler(message);
            }
            else if (ReceiversWithId.TryGetValue(message.Type, out var handlerWithId))
            {
                handlerWithId(message, id);
            }
        }

        public void Send(NodeId targetId, KademliaMessage message)
        {
            message.Sender = _routingTable.SelfNode;
            if (targetId == _routingTable.SelfNode.Id)
            {
                MistLogger.Debug($"[Debug][Send] Loopback {JsonConvert.SerializeObject(message)}");
                OnMessage(JsonConvert.SerializeObject(message), targetId);
            }
            else Send(JsonConvert.SerializeObject(message), targetId);
        }

        public void RegisterReceive(KademliaMessageType type, DNVE1MessageReceivedHandler receiver)
        {
            Receivers[type] = receiver;
        }

        public void RegisterReceive(KademliaMessageType type, DNVE1MessageReceivedHandlerWithFromId receiver)
        {
            ReceiversWithId[type] = receiver;
        }

        private void OnFindNodeResponse(KademliaMessage message, NodeId fromId)
        {
            var closestNodes = JsonConvert.DeserializeObject<ResponseFindNode>(message.Payload);
            foreach (var node in closestNodes.Nodes)
            {
                _routingBase.AddRouting(node.Id, message.Sender.Id);
                _routingTable.AddNode(node);
            }
        }

        private void OnFindValueResponse(KademliaMessage message)
        {
            var response = JsonConvert.DeserializeObject<ResponseFindValue>(message.Payload);
            MistLogger.Debug($"[FindValue][RES] {message.Payload} from {message.Sender.Id}");

            if (string.IsNullOrEmpty(response.Value))
            {
                MistLogger.Error($"[FindValue] Value not found for target {BitConverter.ToString(response.Key)}");
                return;
            }

            MistLogger.Debug(
                $"[Debug][KademliaController] Found value for target {BitConverter.ToString(response.Key)}: {response.Value}");
            _dataStore.Store(response.Key, response.Value);
        }

        public void FindValue(HashSet<NodeInfo> closestNodes, byte[] target)
        {
            if (closestNodes == null) return;
            var count = 0;
            foreach (var node in closestNodes)
            {
                count++;
                _kademlia.FindValue(node, target);
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

        private void OnDestroy()
        {
            _areaTracker?.Dispose();
            _connectionBalancer?.Dispose();
            _visibleNodesController?.Dispose();
        }
    }
}

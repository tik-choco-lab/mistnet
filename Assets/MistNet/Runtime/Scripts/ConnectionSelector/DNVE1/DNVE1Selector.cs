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
        private DNVE1VisibleNodesController _dnve1VisibleNodesController;
        private static readonly Dictionary<KademliaMessageType, DNVE1MessageReceivedHandler> Receivers = new();
        private static readonly Dictionary<KademliaMessageType, DNVE1MessageReceivedHandlerWithFromId> ReceiversWithId = new();
        public DNVE1 Dnve1 = new ();

        protected override void Start()
        {
            base.Start();

            Dnve1.Sender = this;
            Dnve1.RoutingBase = RoutingBase;

            _dataStore = new KademliaDataStore();
            Dnve1.DataStore = _dataStore;

            _routingTable = new KademliaRoutingTable();
            Dnve1.RoutingTable = _routingTable;

            _kademlia = new Kademlia(Dnve1);
            Dnve1.Kademlia = _kademlia;

            _areaTracker = new AreaTracker(Dnve1);
            Dnve1.AreaTracker = _areaTracker;

            _connectionBalancer = new ConnectionBalancer(Dnve1);
            Dnve1.ConnectionBalancer = _connectionBalancer;

            _dnve1VisibleNodesController = new DNVE1VisibleNodesController(Dnve1);
            Dnve1.VisibleNodesController = _dnve1VisibleNodesController;

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
                RoutingBase.AddRouting(node.Id, fromId);
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
            _dnve1VisibleNodesController?.Dispose();
        }
    }
}

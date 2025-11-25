using System;
using System.Collections.Generic;
using System.Linq;
using MistNet.Utils;
using Newtonsoft.Json;

namespace MistNet
{
    public class DNVE1Selector : SelectorBase, IDNVE1MessageSender
    {
        private Kademlia _kademlia;
        private KademliaRoutingTable _routingTable;
        private KademliaDataStore _dataStore;
        private AreaTracker _areaTracker;
        private ConnectionBalancer _connectionBalancer;
        private DNVE1VisibleNodesController _dnve1VisibleNodesController;
        private static readonly Dictionary<KademliaMessageType, DNVE1MessageReceivedHandler> Receivers = new();
        private static readonly Dictionary<KademliaMessageType, DNVE1MessageReceivedHandlerWithFromId> ReceiversWithId = new();
        private readonly DNVE1 _dnve1 = new ();

        protected override void Start()
        {
            base.Start();

            _dnve1.Sender = this;
            _dnve1.RoutingBase = RoutingBase;
            _dnve1.PeerRepository = PeerRepository;
            _dnve1.Layer = Layer;

            _dataStore = new KademliaDataStore();
            _routingTable = new KademliaRoutingTable();
            _dnve1.DataStore = _dataStore;
            _dnve1.RoutingTable = _routingTable;

            _kademlia = new Kademlia(_dnve1);
            _dnve1.Kademlia = _kademlia;
            _routingTable.Init(_dnve1);

            _areaTracker = new AreaTracker(_dnve1);
            _dnve1.AreaTracker = _areaTracker;

            _connectionBalancer = new ConnectionBalancer(_dnve1);
            _dnve1.ConnectionBalancer = _connectionBalancer;

            _dnve1VisibleNodesController = new DNVE1VisibleNodesController(_dnve1);
            _dnve1.VisibleNodesController = _dnve1VisibleNodesController;

            RegisterReceive(KademliaMessageType.ResponseNode, OnFindNodeResponse);
            RegisterReceive(KademliaMessageType.ResponseValue, OnFindValueResponse);

            Layer.World.AddSendFailedCallback((Action<NodeId>) SendFailed);
        }

        private void SendFailed(NodeId id)
        {
            _routingTable.RemoveNode(id);
        }

        protected override void OnMessage(string data, NodeId id)
        {
            var message = JsonConvert.DeserializeObject<KademliaMessage>(data);
            _routingTable.AddNode(message.Sender);
            _dnve1.LastMessageTimes[id] = DateTime.UtcNow;

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
            if (RoutingBase.ConnectedNodes.Count == 0) return;
            message.Sender = _routingTable.SelfNode;
            if (targetId == _routingTable.SelfNode.Id)
            {
                MistLogger.Debug($"[Debug][Send] Loopback {JsonConvert.SerializeObject(message)}");
                OnMessage(JsonConvert.SerializeObject(message), targetId);
            }
            else
            {
                _dnve1.LastMessageTimes[targetId] = DateTime.UtcNow;
                Send(JsonConvert.SerializeObject(message), targetId);
            }
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
#if UNITY_EDITOR
                DebugShowDistance(closestNodes, node);
#endif
            }
            MistLogger.Debug($"[Debug][KademliaController] FindNode response from {fromId}: {string.Join(", ", closestNodes.Nodes.Select(nf => nf.Id))}");
        }

        /// <summary>
        /// xor距離をdebug表示してみる 10進数で
        /// </summary>
        /// <param name="closestNodes"></param>
        /// <param name="node"></param>
        private static void DebugShowDistance(ResponseFindNode closestNodes, NodeInfo node)
        {
            var bits = IdUtil.Xor(closestNodes.Key, IdUtil.ToBytes(node.Id));
            var index = IdUtil.LeadingBitIndex(bits);
            MistLogger.Debug($"[Debug][FindNode] Index: [{index}]");
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

        public void FindValue(IEnumerable<NodeInfo> closestNodes, byte[] target)
        {
            if (closestNodes == null) return;
            var count = 0;
            foreach (var node in closestNodes)
            {
                count++;
                _kademlia.FindValue(node, target);
#if UNITY_EDITOR
                DebugShowDistance(target, node);
#endif
                if (count >= OptConfig.Data.Alpha) break;
            }
        }

        private static void DebugShowDistance(byte[] target, NodeInfo nodeInfo)
        {
            var bits = IdUtil.Xor(target, IdUtil.ToBytes(nodeInfo.Id));
            var index = IdUtil.LeadingBitIndex(bits);
            MistLogger.Debug($"[Debug][AreaTracker]   Index={index} NodeId={nodeInfo.Id} ");
        }

        private void FindNode(HashSet<NodeInfo> closestNodes, byte[] target)
        {
            var count = 0;
            foreach (var node in closestNodes)
            {
                _kademlia.FindNode(node, target);
                count++;
                if (count >= OptConfig.Data.Alpha) break;
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

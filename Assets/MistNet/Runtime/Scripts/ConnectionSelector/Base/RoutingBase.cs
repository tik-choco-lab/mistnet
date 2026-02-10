using System;
using System.Collections.Generic;
using UnityEngine;

namespace MistNet
{
    public abstract class RoutingBase : MonoBehaviour
    {
        private const float ExpireSeconds = 2f;
        public IReadOnlyDictionary<NodeId, Node> Nodes => _nodes;
        protected readonly Dictionary<NodeId, Node> _nodes = new();
        public readonly HashSet<NodeId> ConnectedNodes = new();
        public readonly HashSet<NodeId> MessageNodes = new();
        private Dictionary<NodeId, DateTime> _expireAt = new();

        protected readonly Dictionary<NodeId, NodeId> _routingTable = new();
        protected IPeerRepository PeerRepository;
        protected ILayer Layer;

        public virtual void Init(IPeerRepository peerRepository, ILayer layer)
        {
            PeerRepository = peerRepository;
            Layer = layer;
        }

        public virtual void OnConnected(NodeId id)
        {
            MistLogger.Info($"[ConnectionSelector] OnConnected: {id}");
            ConnectedNodes.Add(id);
            AddNode(id);
        }

        public virtual void OnDisconnected(NodeId id)
        {
            MistLogger.Info($"[ConnectionSelector] OnDisconnected: {id}");
            ConnectedNodes.Remove(id);
            if (MessageNodes.Contains(id)) MessageNodes.Remove(id);
        }

        public virtual void AddMessageNode(NodeId id)
        {
            MistLogger.Info($"[ConnectionSelector] AddMessageNode: {id}");
            MessageNodes.Add(id);
            AddNode(id);
        }

        public virtual void RemoveMessageNode(NodeId id)
        {
            MistLogger.Info($"[ConnectionSelector] RemoveMessageNode: {id}");
            MessageNodes.Remove(id);
        }

        public virtual void AddRouting(NodeId sourceId, NodeId fromId)
        {
            if (sourceId == PeerRepository.SelfId) return;
            if (sourceId == fromId) return;

            MistLogger.Info($"[RoutingTable] Add {sourceId} from {fromId}");

            _routingTable[sourceId] = fromId;
            _expireAt[sourceId] = DateTime.UtcNow.AddSeconds(ExpireSeconds);
        }

        public virtual void Add(NodeId sourceId, NodeId fromId)
        {
            MistLogger.Info($"[RoutingTable] Add {sourceId} from {fromId}");
        }

        public virtual NodeId Get(NodeId targetId)
        {
            if (ConnectedNodes.Contains(targetId)) return targetId;

            MistLogger.Trace($"[RoutingTable] Get {targetId}");
            if (_routingTable.TryGetValue(targetId, out var value))
            {
                return value;
            }

            MistLogger.Warning($"[RoutingTable] Not found {targetId}");

            if (_nodes.TryGetValue(targetId, out var node) && node != null)
            {
                return node.Id;
            }

            MistLogger.Warning($"[RoutingTable] Not found node {targetId}");
            return null;
        }


        public virtual void Remove(NodeId id)
        {
        }

        // -------------------------

        private void AddNode(NodeId id)
        {
            if (id == PeerRepository.SelfId) return;
            if (_nodes.ContainsKey(id)) return;
            var node = new Node
            {
                Id = id,
            };
            _nodes[id] = node;
        }

        public void UpdateNode(NodeId id, Node node)
        {
            if (id == PeerRepository.SelfId) return;
            MistLogger.Info($"[ConnectionSelector] AddNode: {id}");

            if (ConnectedNodes.Contains(node.Id))
            {
                node.State = EvalNodeState.Connected;
                if (MessageNodes.Contains(node.Id))
                {
                    node.State = EvalNodeState.Visible;
                }
            }
            else node.State = EvalNodeState.Disconnected;

            _nodes[id] = node;
        }

        public void RemoveNode(NodeId id)
        {
            MistLogger.Info($"[ConnectionSelector] RemoveNode: {id}");
            _nodes.Remove(id);
        }

        public void ClearNodes()
        {
            MistLogger.Info("[ConnectionSelector] ClearNodes");
            _nodes.Clear();
        }

        public virtual Node GetNode(NodeId id)
        {
            return null;
        }
    }
}

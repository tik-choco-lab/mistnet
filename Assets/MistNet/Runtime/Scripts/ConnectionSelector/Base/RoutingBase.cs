using System.Collections.Generic;
using UnityEngine;

namespace MistNet
{
    public abstract class RoutingBase : MonoBehaviour
    {
        public IReadOnlyDictionary<NodeId, Node> Nodes => _nodes;
        protected readonly Dictionary<NodeId, Node> _nodes = new(); // ノードのリスト 接続しているかどうかに関わらず持つ
        public readonly HashSet<NodeId> ConnectedNodes = new(); // 今接続中のノードのリスト
        public readonly HashSet<NodeId> MessageNodes = new(); // メッセージのやり取りを行うノードのリスト

        protected readonly Dictionary<NodeId, NodeId> _routingTable = new();

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
            if (sourceId == MistManager.I.PeerRepository.SelfId) return;
            if (sourceId == fromId) return;

            MistLogger.Info($"[RoutingTable] Add {sourceId} from {fromId}");
            // if (_routingTable.TryAdd(sourceId, fromId))
            // {
            //     return;
            // }
            _routingTable[sourceId] = fromId;
        }

        public virtual void Add(NodeId sourceId, NodeId fromId)
        {
            Debug.LogError($"[RoutingTable] Add {sourceId} from {fromId}");
        }

        public virtual NodeId Get(NodeId targetId)
        {
            if (ConnectedNodes.Contains(targetId)) return targetId;

            MistLogger.Info($"[RoutingTable] Get {targetId}");
            if (_routingTable.TryGetValue(targetId, out var value))
            {
                return value;
            }

            MistLogger.Warning($"[RoutingTable] Not found {targetId}");

            // 適当に返す
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
            if (id == MistManager.I.PeerRepository.SelfId) return; // 自分自身のノードは追加しない
            if (_nodes.ContainsKey(id)) return;
            var node = new Node
            {
                Id = id,
            };
            _nodes[id] = node;
        }

        public void UpdateNode(NodeId id, Node node)
        {
            if (id == MistManager.I.PeerRepository.SelfId) return; // 自分自身のノードは更新しない
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

        /// <summary>
        /// TODO: 現状、DhtRoutingの方はBucketで取得しているがBasicRoutingはTableを持たないため、取得ができない状態である
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public virtual Node GetNode(NodeId id)
        {
            return null;
        }
    }
}

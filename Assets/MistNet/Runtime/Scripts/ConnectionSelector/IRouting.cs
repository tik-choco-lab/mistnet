using System.Collections.Generic;
using UnityEngine;

namespace MistNet
{
    public abstract class IRouting : MonoBehaviour
    {
        public IReadOnlyDictionary<NodeId, Node> Nodes => _nodes;
        protected readonly Dictionary<NodeId, Node> _nodes = new(); // ノードのリスト 接続しているかどうかに関わらず持つ
        public readonly HashSet<NodeId> ConnectedNodes = new(); // 今接続中のノードのリスト
        public readonly HashSet<NodeId> MessageNodes = new(); // メッセージのやり取りを行うノードのリスト

        protected readonly Dictionary<NodeId, NodeId> _routingTable = new();

        public virtual void OnConnected(NodeId id)
        {
            MistDebug.Log($"[ConnectionSelector] OnConnected: {id}");
            ConnectedNodes.Add(id);
            AddNode(id);
        }

        public virtual void OnDisconnected(NodeId id)
        {
            MistDebug.Log($"[ConnectionSelector] OnDisconnected: {id}");
            ConnectedNodes.Remove(id);
            if (MessageNodes.Contains(id)) MessageNodes.Remove(id);
        }

        public virtual void AddMessageNode(NodeId id)
        {
            MistDebug.Log($"[ConnectionSelector] AddMessageNode: {id}");
            MessageNodes.Add(id);
            AddNode(id);
        }

        public virtual void RemoveMessageNode(NodeId id)
        {
            MistDebug.Log($"[ConnectionSelector] RemoveMessageNode: {id}");
            MessageNodes.Remove(id);
        }

        public virtual void AddRouting(NodeId sourceId, NodeId fromId)
        {
            if (sourceId == MistManager.I.PeerRepository.SelfId) return;
            if (sourceId == fromId) return;

            MistDebug.Log($"[RoutingTable] Add {sourceId} from {fromId}");
            if (_routingTable.TryAdd(sourceId, fromId))
            {
                return;
            }

            _routingTable[sourceId] = fromId;
        }

        public virtual NodeId Get(NodeId targetId)
        {
            if (ConnectedNodes.Contains(targetId)) return targetId;

            MistDebug.Log($"[RoutingTable] Get {targetId}");
            if (_routingTable.TryGetValue(targetId, out var value))
            {
                return value;
            }

            MistDebug.LogWarning($"[RoutingTable] Not found {targetId}");

            // 適当に返す
            if (_nodes.TryGetValue(targetId, out var node) && node != null)
            {
                return node.Id;
            }

            MistDebug.LogWarning($"[RoutingTable] Not found node {targetId}");
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
            MistDebug.Log($"[ConnectionSelector] AddNode: {id}");
            _nodes[id] = node;
        }

        public void RemoveNode(NodeId id)
        {
            MistDebug.Log($"[ConnectionSelector] RemoveNode: {id}");
            _nodes.Remove(id);
        }

        public void ClearNodes()
        {
            MistDebug.Log("[ConnectionSelector] ClearNodes");
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

using System.Collections.Generic;
using UnityEngine;

namespace MistNet
{
    public abstract class IRouting : MonoBehaviour
    {
        public readonly Dictionary<NodeId, Node> Nodes = new(); // ノードのリスト 接続しているかどうかに関わらず持つ
        public readonly HashSet<NodeId> ConnectedNodes = new(); // 今接続中のノードのリスト
        public readonly HashSet<NodeId> AoiNodes = new(); // メッセージのやり取りを行うノードのリスト

        public virtual void OnConnected(NodeId id)
        {
            Debug.Log($"[ConnectionSelector] OnConnected: {id}");
            ConnectedNodes.Add(id);
        }

        public virtual void OnDisconnected(NodeId id)
        {
            Debug.Log($"[ConnectionSelector] OnDisconnected: {id}");
            ConnectedNodes.Remove(id);
            if (AoiNodes.Contains(id)) AoiNodes.Remove(id);
        }

        public virtual void AddAoiNode(NodeId id)
        {
            Debug.Log($"[ConnectionSelector] AddMessageNode: {id}");
            AoiNodes.Add(id);
        }

        public virtual void RemoveAoiNode(NodeId id)
        {
            Debug.Log($"[ConnectionSelector] RemoveMessageNode: {id}");
            AoiNodes.Remove(id);
        }

        public virtual void AddNode(NodeId id, Node node)
        {
            Debug.Log($"[ConnectionSelector] AddNode: {id}");
            Nodes[id] = node;
        }

        public virtual void RemoveNode(NodeId id)
        {
            Debug.Log($"[ConnectionSelector] RemoveNode: {id}");
            Nodes.Remove(id);
        }

        public virtual void Add(NodeId sourceId, NodeId fromId)
        {
            Debug.LogError($"[RoutingTable] Add {sourceId} from {fromId}");
        }

        public virtual NodeId Get(NodeId targetId)
        {
            return null;
        }

        public virtual void Remove(NodeId id)
        {
        }
    }
}

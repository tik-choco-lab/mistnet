using System.Collections.Generic;
using UnityEngine;

namespace MistNet
{
    public abstract class IRouting : MonoBehaviour
    {
        public readonly HashSet<NodeId> ConnectedNodes = new();

        public virtual void OnConnected(NodeId id)
        {
            Debug.Log($"[ConnectionSelector] OnConnected: {id}");
            ConnectedNodes.Add(id);
        }

        public virtual void OnDisconnected(NodeId id)
        {
            Debug.Log($"[ConnectionSelector] OnDisconnected: {id}");
            ConnectedNodes.Remove(id);
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

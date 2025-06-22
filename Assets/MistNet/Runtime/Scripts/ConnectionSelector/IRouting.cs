using System.Collections.Generic;
using System.Linq;
using MistNet.Utils;
using UnityEngine;

namespace MistNet
{
    public abstract class IRouting : MonoBehaviour
    {

        public readonly HashSet<NodeId> ConnectedNodes = new(); // 今接続中のノードのリスト
        public readonly HashSet<NodeId> MessageNodes = new(); // メッセージのやり取りを行うノードのリスト

        public virtual void OnConnected(NodeId id)
        {
            Debug.Log($"[ConnectionSelector] OnConnected: {id}");
            ConnectedNodes.Add(id);
        }

        public virtual void OnDisconnected(NodeId id)
        {
            Debug.Log($"[ConnectionSelector] OnDisconnected: {id}");
            ConnectedNodes.Remove(id);
            if (MessageNodes.Contains(id)) MessageNodes.Remove(id);
        }

        public virtual void AddMessageNode(NodeId id)
        {
            Debug.Log($"[ConnectionSelector] AddMessageNode: {id}");
            MessageNodes.Add(id);
        }

        public virtual void RemoveMessageNode(NodeId id)
        {
            Debug.Log($"[ConnectionSelector] RemoveMessageNode: {id}");
            MessageNodes.Remove(id);
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

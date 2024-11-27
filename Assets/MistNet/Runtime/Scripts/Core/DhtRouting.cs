using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MistNet
{
    public class DhtRouting : IRouting
    {
        // ↓以下の2つ，まとめられそう？
        public readonly HashSet<string> ConnectedNodes = new();
        private readonly Dictionary<string, string> _routingTable = new();

        public readonly List<HashSet<Node>> Buckets = new();
        public readonly Dictionary<string, int> NodeIdToBucketIndex = new();

        public override void Add(string sourceId, string fromId)
        {
            if (sourceId == MistManager.I.MistPeerData.SelfId) return;
            if (sourceId == fromId) return;

            MistDebug.Log($"[RoutingTable] Add {sourceId} from {fromId}");
            if (_routingTable.TryAdd(sourceId, fromId))
            {
                return;
            }

            _routingTable[sourceId] = fromId;
        }

        public override string Get(string targetId)
        {
            if (ConnectedNodes.Contains(targetId)) return targetId;

            MistDebug.Log($"[RoutingTable] Get {targetId}");
            if (_routingTable.TryGetValue(targetId, out var value))
            {
                return value;
            }

            MistDebug.LogWarning($"[RoutingTable] Not found {targetId}");

            // 適当に返す
            if (NodeIdToBucketIndex.TryGetValue(targetId, out var bucketIndex))
            {
                var bucket = Buckets[bucketIndex];
                if (bucket.Count != 0)
                {
                    var node = bucket.FirstOrDefault(n => ConnectedNodes.Contains(n.Id));
                    if (!string.IsNullOrEmpty(node.Id)) // デフォルト値もしくは条件不一致でない場合
                    {
                        return node.Id;
                    }
                }
            }

            Debug.LogError($"[RoutingTable] Not found bucket index {targetId}");
            return null;
        }
    }
}

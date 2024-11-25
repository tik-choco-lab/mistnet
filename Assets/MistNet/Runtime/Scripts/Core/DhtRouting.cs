using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MistNet
{
    public class DhtRouting : IRouting
    {
        public readonly HashSet<string> ConnectedNodes = new();
        public readonly List<HashSet<Node>> Buckets = new();
        public readonly Dictionary<string, int> NodeIdToBucketIndex = new();
        private readonly Dictionary<string, string> _routingTable = new();

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
                    var node = bucket.First(n => ConnectedNodes.Contains(n.Id));
                    if (!string.IsNullOrEmpty(node.Id)) return node.Id;
                }
            }

            Debug.LogError($"[RoutingTable] Not found bucket index {targetId}");
            return null;
        }
    }
}

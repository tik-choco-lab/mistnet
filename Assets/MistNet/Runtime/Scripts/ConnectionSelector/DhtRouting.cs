using System.Collections.Generic;
using System.Linq;

namespace MistNet
{
    /// <summary>
    /// 基本，接続切断関係なく持つべきかも
    /// </summary>
    public class DhtRouting : IRouting
    {
        private const int BucketSize = 8;
        private readonly Dictionary<NodeId, NodeId> _routingTable = new();

        public IReadOnlyList<IReadOnlyCollection<Node>> Buckets => _buckets;
        private readonly List<HashSet<Node>> _buckets = new();
        private readonly Dictionary<NodeId, int> _bucketIndexByNodeId = new();

        public override void Add(NodeId sourceId, NodeId fromId)
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

        public override NodeId Get(NodeId targetId)
        {
            if (ConnectedNodes.Contains(targetId)) return targetId;

            MistDebug.Log($"[RoutingTable] Get {targetId}");
            if (_routingTable.TryGetValue(targetId, out var value))
            {
                return value;
            }

            MistDebug.LogWarning($"[RoutingTable] Not found {targetId}");

            // 適当に返す
            if (_bucketIndexByNodeId.TryGetValue(targetId, out var bucketIndex))
            {
                var bucket = Buckets[bucketIndex];
                if (bucket.Count != 0)
                {
                    var node = bucket.FirstOrDefault(n => ConnectedNodes.Contains(n.Id));
                    if (node == null)
                    {
                        MistDebug.LogWarning($"[RoutingTable] node {bucketIndex} is null");
                        return null;
                    }

                    if (!string.IsNullOrEmpty(node.Id)) // デフォルト値もしくは条件不一致でない場合
                    {
                        return node.Id;
                    }
                }
            }

            MistDebug.LogWarning($"[RoutingTable] Not found bucket index {targetId}");
            return null;
        }

        public enum Result
        {
            Success,
            Fail,
        }

        public Result AddBucket(int index, Node node)
        {
            if (node.Id == MistManager.I.MistPeerData.SelfId) return Result.Success;
            InitBucket(index);
            _buckets[index] ??= new HashSet<Node>();
            if (_buckets[index].Count >= BucketSize) return Result.Fail;
            _buckets[index].Add(node);
            _bucketIndexByNodeId[node.Id] = index;
            return Result.Success;
        }

        private void InitBucket(int index)
        {
            if (index < _buckets.Count) return;

            for (var i = 0; i <= index; i++)
            {
                if (i < _buckets.Count) continue;
                _buckets.Add(new HashSet<Node>());
                MistDebug.Log($"[RoutingTable] InitBucket {i}");
            }
        }

        public void RemoveBucket(int index, Node node)
        {
            _buckets[index].Remove(node);
            _bucketIndexByNodeId.Remove(node.Id);
        }

        public int GetBucketIndex(NodeId nodeId)
        {
            if (!_bucketIndexByNodeId.ContainsKey(nodeId)) return -1;
            return _bucketIndexByNodeId[nodeId];
        }

        public void ReplaceBucket(Node node, int newIndex)
        {
            InitBucket(newIndex);
            var oldIndex = _bucketIndexByNodeId[node.Id];
            _buckets[oldIndex].Remove(node);
            _buckets[newIndex].Add(node);
            _bucketIndexByNodeId[node.Id] = newIndex;
        }

        public override void Remove(NodeId id)
        {
            if (!_routingTable.ContainsKey(id)) return;

            MistDebug.Log($"[RoutingTable] Remove {id}");
            _routingTable.Remove(id);
            // _buckets[_bucketIndexByNodeId[id]].RemoveWhere(n => n.Id == id);
            // _bucketIndexByNodeId.Remove(id);
        }

        public override void OnDisconnected(NodeId id)
        {
            base.OnDisconnected(id);
            MistDebug.Log($"[RoutingTable] Remove {id}");
            _routingTable.Remove(id);
        }
    }
}

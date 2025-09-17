using System;
using System.Collections.Generic;
using System.Linq;
using MistNet.Utils;

namespace MistNet
{
    public class KademliaRoutingTable
    {
        public NodeInfo SelfNode { get; private set; } = new NodeInfo();
        private readonly KBucket[] _buckets = new KBucket[IdUtil.BitLength];
        private byte[] _selfId;
        private Kademlia _kademlia;
        private PriorityQueue<NodeInfo, byte[]> _pq;
        private ByteArrayDistanceComparer _comparer;

        public void Init(Kademlia kademlia)
        {
            _kademlia = kademlia;
        }

        public void AddNode(NodeInfo nodeInfo)
        {
            if (nodeInfo.Id == PeerRepository.I.SelfId) return;

            nodeInfo.LastSeen = DateTime.UtcNow;
            var id = IdUtil.ToBytes(nodeInfo.Id.ToString());
            _selfId ??= IdUtil.ToBytes(PeerRepository.I.SelfId.ToString());
            var distance = IdUtil.Xor(_selfId, id);
            var index = IdUtil.LeadingBitIndex(distance);

            if (index == -1)
            {
                MistLogger.Error($"[KademliaRoutingTable] Invalid node ID: {nodeInfo.Id}. self: {PeerRepository.I.SelfId} Cannot determine bucket index.");
            }
            _buckets[index] ??= new KBucket(_kademlia);
            _buckets[index].AddNode(nodeInfo);
        }

        public HashSet<NodeInfo> FindClosestNodes(byte[] targetId)
        {
            _comparer ??= new ByteArrayDistanceComparer();
            _pq ??= new PriorityQueue<NodeInfo, byte[]>(_comparer);

            foreach (var bucket in _buckets)
            {
                if (bucket == null) continue;
                foreach (var node in bucket.Nodes)
                {
                    var dist = IdUtil.Xor(targetId, IdUtil.ToBytes(node.Id.ToString()));
                    _pq.Enqueue(node, dist);
                    if (_pq.Count > KBucket.K)
                        _pq.Dequeue(); // 常にK個以内に保つ
                }
            }

            if (_pq.Count == 0)
            {
                MistLogger.Warning("[KademliaRoutingTable] No nodes found in routing table.");
                return new HashSet<NodeInfo>();
            }

            var orderedList = _pq.UnorderedItems()
                .OrderBy(x => x.Priority, _comparer) // 近い順
                .Select(x => x.Element)
                .ToList(); // Listに変換

            var orderedSet = new HashSet<NodeInfo>(orderedList); // HashSetを作る
            return orderedSet;
        }
        // public HashSet<NodeInfo> FindClosestNodes(byte[] targetId)
        // {
        //     var allNodes = new List<NodeInfo>();
        //     foreach (var bucket in _buckets)
        //     {
        //         if (bucket != null)
        //         {
        //             allNodes.AddRange(bucket.Nodes);
        //         }
        //     }
        //
        //     if (allNodes.Count == 0)
        //     {
        //         MistLogger.Warning("[KademliaRoutingTable] No nodes found in routing table.");
        //         return new HashSet<NodeInfo>();
        //     }
        //
        //     return allNodes
        //         .Select(n => (Node: n, Distance: IdUtil.Xor(targetId, IdUtil.ToBytes(n.Id.ToString()))))
        //         .OrderBy(tuple => tuple.Distance, new ByteArrayDistanceComparer())
        //         .Take(KBucket.K)
        //         .Select(tuple => tuple.Node)
        //         .ToHashSet();
        // }
    }
}

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
                MistDebug.LogError($"[KademliaRoutingTable] Invalid node ID: {nodeInfo.Id}. self: {PeerRepository.I.SelfId}Cannot determine bucket index.");
            }
            _buckets[index] ??= new KBucket(_kademlia);
            _buckets[index].AddNode(nodeInfo);
        }

        public List<NodeInfo> FindClosestNodes(byte[] targetId)
        {
            var allNodes = new List<NodeInfo>();
            foreach (var bucket in _buckets)
            {
                if (bucket != null)
                {
                    allNodes.AddRange(bucket.Nodes);
                }
            }

            return allNodes
                .OrderBy(n => IdUtil.Xor(targetId, IdUtil.ToBytes(n.Id.ToString())))
                .Take(KBucket.K)
                .ToList();
        }
    }
}

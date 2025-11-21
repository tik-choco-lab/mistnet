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
        private IPeerRepository _peerRepository;

        public void Init(DNVE1 dnve1)
        {
            _kademlia = dnve1.Kademlia;
            _peerRepository = dnve1.PeerRepository;
        }

        public void AddNode(NodeInfo nodeInfo)
        {
            if (nodeInfo.Id == _peerRepository.SelfId) return;

            nodeInfo.LastSeen = DateTime.UtcNow;
            var id = IdUtil.ToBytes(nodeInfo.Id.ToString());
            _selfId ??= IdUtil.ToBytes(_peerRepository.SelfId.ToString());
            var distance = IdUtil.Xor(_selfId, id);
            var index = IdUtil.LeadingBitIndex(distance);

            if (index == -1)
            {
                MistLogger.Error(
                    $"[KademliaRoutingTable] Invalid node ID: {nodeInfo.Id}. self: {_peerRepository.SelfId} Cannot determine bucket index.");
            }

            _buckets[index] ??= new KBucket(_kademlia);
            _buckets[index].AddNode(nodeInfo);
        }

        public void RemoveNode(NodeId nodeId)
        {
            var id = IdUtil.ToBytes(nodeId.ToString());
            _selfId ??= IdUtil.ToBytes(_peerRepository.SelfId.ToString());
            var distance = IdUtil.Xor(_selfId, id);
            var index = IdUtil.LeadingBitIndex(distance);

            if (index == -1)
            {
                MistLogger.Error(
                    $"[KademliaRoutingTable] Invalid node ID: {nodeId}. self: {_peerRepository.SelfId} Cannot determine bucket index.");
                return;
            }

            var bucket = _buckets[index];
            if (bucket == null) return;

            bucket.RemoveNode(nodeId);
        }

        public NodeInfo GetNodeInfo(NodeId nodeId)
        {
            var id = IdUtil.ToBytes(nodeId.ToString());
            _selfId ??= IdUtil.ToBytes(_peerRepository.SelfId.ToString());
            var distance = IdUtil.Xor(_selfId, id);
            var index = IdUtil.LeadingBitIndex(distance);

            if (index == -1)
            {
                MistLogger.Error(
                    $"[KademliaRoutingTable] Invalid node ID: {nodeId}. self: {_peerRepository.SelfId} Cannot determine bucket index.");
                return null;
            }

            var bucket = _buckets[index];
            if (bucket == null) return null;

            return bucket.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId));
        }

        public HashSet<NodeInfo> FindClosestNodes(byte[] targetId)
        {
            var allNodes = GetAllNodes();

            if (allNodes.Count == 0)
            {
                MistLogger.Warning("[KademliaRoutingTable] No nodes found in routing table.");
                return new HashSet<NodeInfo>();
            }

            return allNodes
                .Select(n => (Node: n, Distance: IdUtil.Xor(targetId, IdUtil.ToBytes(n.Id.ToString()))))
                .OrderBy(tuple => tuple.Distance, new ByteArrayDistanceComparer())
                .Take(KBucket.K)
                .Select(tuple => tuple.Node)
                .ToHashSet();
        }

        private List<NodeInfo> GetAllNodes()
        {
            var allNodes = new List<NodeInfo>();
            foreach (var bucket in _buckets)
            {
                if (bucket == null) continue;

                allNodes.AddRange(bucket.Nodes);
            }

            return allNodes;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MistNet.Utils;

namespace MistNet
{
    public class AreaTracker : IDisposable
    {
        private readonly Kademlia _kademlia;
        private readonly KademliaRoutingTable _routingTable;
        private readonly DNVE1Selector _dnve1Selector;
        private readonly CancellationTokenSource _cts;

        public static Area MyArea => new(MistSyncManager.I.SelfSyncObject.transform.position);
        public IEnumerable<Area> SurroundingChunks => _surroundingChunks;
        private readonly HashSet<Area> _surroundingChunks = new();
        private Area _prevSelfChunk;
        private Area _selfChunk;

        public AreaTracker(Kademlia kademlia, KademliaRoutingTable routingTable,
            DNVE1Selector dnve1Selector)
        {
            _kademlia = kademlia;
            _routingTable = routingTable;
            _dnve1Selector = dnve1Selector;
            _cts = new CancellationTokenSource();
            LoopFindMyAreaInfo(_cts.Token).Forget();
        }

        private async UniTask LoopFindMyAreaInfo(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.AreaTrackerIntervalSeconds),
                    cancellationToken: token);
                var selfNodePosition = MistSyncManager.I.SelfSyncObject.transform.position;
                _selfChunk ??= new Area();
                _selfChunk.Set(selfNodePosition);

                GetSurroundingChunks(OptConfig.Data.ChunkLoadSize, _selfChunk);

                FindMyAreaNodes(_surroundingChunks);

                AddNodeToArea(_selfChunk, _routingTable.SelfNode);
                if (!_selfChunk.Equals(_prevSelfChunk))
                {
                    if (_prevSelfChunk != null) RemoveNodeFromArea(_prevSelfChunk, _routingTable.SelfNode);
                    _prevSelfChunk ??= new Area();
                    _prevSelfChunk.Set(_selfChunk.GetChunk());
                }
            }
        }

        /// <summary>
        /// NOTE: ConnectionBalancerで自身の位置を周りに送信している
        /// </summary>
        /// <param name="surroundingChunks"></param>
        private void FindMyAreaNodes(HashSet<Area> surroundingChunks)
        {
            foreach (var area in surroundingChunks)
            {
                var target = IdUtil.ToBytes(area.ToString());
                var closestNodes = _routingTable.FindClosestNodes(target);
                _dnve1Selector.FindValue(closestNodes, target);
            }
        }

        private void AddNodeToArea(Area chunk, NodeInfo node)
        {
            var target = IdUtil.ToBytes(chunk.ToString());

            var closestNodes = _routingTable.FindClosestNodes(target);
            foreach (var closestNode in closestNodes)
            {
                _kademlia.Store(closestNode, target, $"add{Kademlia.SplitChar}{node.Id}");
            }
        }

        private void RemoveNodeFromArea(Area chunk, NodeInfo node)
        {
            var target = IdUtil.ToBytes(chunk.ToString());

            var closestNodes = _routingTable.FindClosestNodes(target);
            foreach (var closestNode in closestNodes)
            {
                _kademlia.Store(closestNode, target, $"remove{Kademlia.SplitChar}{node.Id}");
            }
        }

        private void GetSurroundingChunks(int sizeIndex, Area area)
        {
            _surroundingChunks.Clear();
            for (int x = -sizeIndex; x <= sizeIndex; x++)
            {
                for (int z = -sizeIndex; z <= sizeIndex; z++)
                {
                    var newArea = new Area(area.X + x, 0, area.Z + z);
                    _surroundingChunks.Add(newArea);
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}

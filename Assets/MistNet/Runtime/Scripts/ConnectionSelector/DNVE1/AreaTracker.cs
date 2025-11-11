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
        public readonly HashSet<NodeId> ExchangeNodes = new();

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
            }
        }

        /// <summary>
        /// NOTE: ConnectionBalancerで自身の位置を周りに送信している
        /// </summary>
        /// <param name="surroundingChunks"></param>
        private void FindMyAreaNodes(HashSet<Area> surroundingChunks)
        {
            ExchangeNodes.Clear();
            foreach (var area in surroundingChunks)
            {
                var target = IdUtil.ToBytes(area.ToString());
                var closestNodes = _routingTable.FindClosestNodes(target);
                _dnve1Selector.FindValue(closestNodes, target);

                foreach (var nodeInfo in closestNodes)
                {
                    // 接続していない場合はskip
                    if (!MistManager.I.Transport.IsConnectingOrConnected(nodeInfo.Id)) continue;
                    ExchangeNodes.Add(nodeInfo.Id);
                }
            }
        }

        private void AddNodeToArea(Area chunk, NodeInfo node)
        {
            var target = IdUtil.ToBytes(chunk.ToString());

            var closeNodes = _routingTable.FindClosestNodes(target);
            foreach (var closeNode in closeNodes)
            {
                _kademlia.Store(closeNode, target, node.Id.ToString());
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

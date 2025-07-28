using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MistNet.Utils;
using Newtonsoft.Json;

namespace MistNet
{
    public class AreaTracker : IDisposable
    {
        private readonly Kademlia _kademlia;
        private readonly KademliaDataStore _dataStore;
        private readonly KademliaRoutingTable _routingTable;
        private readonly KademliaController _kademliaController;
        private readonly CancellationTokenSource _cts;

        public Area MyArea => new(MistSyncManager.I.SelfSyncObject.transform.position);
        public IReadOnlyCollection<Area> SurroundingChunks => _surroundingChunks;
        private readonly HashSet<Area> _surroundingChunks = new();
        private readonly HashSet<Area> _unloadedChunks = new();

        public AreaTracker(Kademlia kademlia, KademliaDataStore dataStore, KademliaRoutingTable routingTable,
            KademliaController kademliaController)
        {
            _kademlia = kademlia;
            _dataStore = dataStore;
            _routingTable = routingTable;
            _kademliaController = kademliaController;
            _cts = new CancellationTokenSource();
            LoopFindMyAreaInfo(_cts.Token).Forget();
        }

        private async UniTask LoopFindMyAreaInfo(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfigLoader.Data.AreaTrackerIntervalSeconds), cancellationToken: token);
                var selfNodePosition = MistSyncManager.I.SelfSyncObject.transform.position;
                var chunk = new Area(selfNodePosition);

                var previousChunk = new HashSet<Area>(_surroundingChunks);
                GetSurroundingChunks(OptConfigLoader.Data.ChunkLoadSize, chunk);
                _unloadedChunks.Clear();

                foreach (var area in _surroundingChunks)
                {
                    if (previousChunk.Contains(area)) continue;
                    _unloadedChunks.Add(area);
                }

                // 前と同じ場合は何もしない
                // if (previousChunk.SetEquals(_surroundingChunks)) continue; // 途中で空間のNodesに更新がかかるので、何度も読み込む

                FindMyAreaInfo(_surroundingChunks);

                foreach (var areaChunk in _surroundingChunks)
                {
                    AddNodeToArea(areaChunk, _routingTable.SelfNode);
                }

                foreach (var areaChunk in _unloadedChunks)
                {
                    RemoveNodeFromArea(areaChunk, _routingTable.SelfNode);
                }
            }
        }

        private void FindMyAreaInfo(HashSet<Area> surroundingChunks)
        {
            foreach (var area in surroundingChunks)
            {
                var target = IdUtil.ToBytes(area.ToString());
                if (_dataStore.TryGetValue(target, out var _)) continue;
                var closestNodes = _routingTable.FindClosestNodes(target);
                _kademliaController.FindValue(closestNodes, target);
            }
        }

        private void AddNodeToArea(Area chunk, NodeInfo node)
        {
            var target = IdUtil.ToBytes(chunk.ToString());
            var areaInfo = GetAreaInfo(chunk, target);

            if (areaInfo.Nodes.Contains(node)) return; // Node already exists

            areaInfo.Nodes.Add(node);
            _dataStore.Store(target, areaInfo.ToString());

            var areaInfoStr = JsonConvert.SerializeObject(areaInfo);
            var closestNodes = _routingTable.FindClosestNodes(target);
            foreach (var closestNode in closestNodes)
            {
                _kademlia.Store(closestNode, target, areaInfoStr);
            }
        }

        private void RemoveNodeFromArea(Area chunk, NodeInfo node)
        {
            var target = IdUtil.ToBytes(chunk.ToString());
            var areaInfo = GetAreaInfo(chunk, target);

            if (!areaInfo.Nodes.Contains(node)) return; // Node does not exist

            areaInfo.Nodes.Remove(node);
            _dataStore.Store(target, areaInfo.ToString());

            var areaInfoStr = JsonConvert.SerializeObject(areaInfo);
            var closestNodes = _routingTable.FindClosestNodes(target);
            foreach (var closestNode in closestNodes)
            {
                _kademlia.Store(closestNode, target, areaInfoStr);
            }
        }

        private AreaInfo GetAreaInfo(Area chunk, byte[] target)
        {
            AreaInfo areaInfo;
            if (_dataStore.TryGetValue(target, out var value))
            {
                areaInfo = JsonConvert.DeserializeObject<AreaInfo>(value);
            }
            else
            {
                areaInfo = new AreaInfo
                {
                    Chunk = chunk,
                    Nodes = new List<NodeInfo>()
                };
            }

            return areaInfo;
        }

        private void GetSurroundingChunks(int sizeIndex, Area area)
        {
            _surroundingChunks.Clear();
            for (int x = -sizeIndex; x <= sizeIndex; x++)
            {
                // for (int y = -sizeIndex; y <= sizeIndex; y++)
                {
                    for (int z = -sizeIndex; z <= sizeIndex; z++)
                    {
                        // var newArea = new Area(area.X + x, area.Y + y, area.Z + z);
                        var newArea = new Area(area.X + x, 0, area.Z + z);
                        _surroundingChunks.Add(newArea);
                    }
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

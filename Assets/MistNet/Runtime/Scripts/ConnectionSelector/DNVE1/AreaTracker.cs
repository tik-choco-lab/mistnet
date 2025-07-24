using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MistNet.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class AreaTracker : IDisposable
    {
        private readonly Kademlia _kademlia;
        private readonly KademliaDataStore _dataStore;
        private readonly KademliaRoutingTable _routingTable;
        private readonly KademliaController _kademliaController;
        private readonly CancellationTokenSource _cts;

        public Area MyArea => new Area(MistSyncManager.I.SelfSyncObject.transform.position);
        public IReadOnlyCollection<Area> SurroundingChunks => _surroundingChunks;
        private HashSet<Area> _surroundingChunks;
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
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfigLoader.Data.AreaTrackerIntervalSeconds),
                    cancellationToken: token);
                var selfNodePosition = MistSyncManager.I.SelfSyncObject.transform.position;
                var chunk = new Area(selfNodePosition);

                var previousChunk = new HashSet<Area>(_surroundingChunks);
                _surroundingChunks = GetSurroundingChunks(OptConfigLoader.Data.ChunkLoadSize, chunk);
                _unloadedChunks.Clear();

                foreach (var area in _surroundingChunks)
                {
                    if (previousChunk.Contains(area)) continue;
                    _unloadedChunks.Add(area);
                }

                foreach (var surroundingChunk in _surroundingChunks)
                {
                    AddNodeToArea(IdUtil.ToBytes(surroundingChunk.ToString()), _routingTable.SelfNode);
                }

                foreach (var unloadedChunk in _unloadedChunks)
                {
                    RemoveNodeFromArea(IdUtil.ToBytes(unloadedChunk.ToString()), _routingTable.SelfNode);
                }

                FindMyAreaInfo(_surroundingChunks);
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

        private void StoreMyLocation(Vector3 position)
        {
            var chunk = new Area(position);

            var target = IdUtil.ToBytes(chunk.ToString());
            var closestNodes = _routingTable.FindClosestNodes(target);
            if (closestNodes.Count < KBucket.K)
            {
                UpdateArea(target, chunk, closestNodes);
            }
            else
            {
                _kademliaController.FindNode(closestNodes, target);
            }
        }

        private void AddNodeToArea(byte[] target, NodeInfo node)
        {
            if (!_dataStore.TryGetValue(target, out var value))
            {
                return; // Area not found
            }

            var areaInfo = JsonConvert.DeserializeObject<AreaInfo>(value);
            if (areaInfo.Nodes.Contains(node)) return; // Node already exists

            areaInfo.Nodes.Add(node);
            _dataStore.Store(target, areaInfo.ToString());

            var closestNodes = _routingTable.FindClosestNodes(target);
            foreach (var closestNode in closestNodes)
            {
                _kademlia.Store(closestNode, target, areaInfo.ToString());
            }
        }

        private void RemoveNodeFromArea(byte[] target, NodeInfo node)
        {
            if (!_dataStore.TryGetValue(target, out var value))
            {
                return; // Area not found
            }

            var areaInfo = JsonConvert.DeserializeObject<AreaInfo>(value);
            if (!areaInfo.Nodes.Contains(node)) return; // Node does not exist

            areaInfo.Nodes.Remove(node);
            _dataStore.Store(target, areaInfo.ToString());

            var closestNodes = _routingTable.FindClosestNodes(target);
            foreach (var closestNode in closestNodes)
            {
                _kademlia.Store(closestNode, target, areaInfo.ToString());
            }
        }

        private void UpdateArea(byte[] target, Area chunk, List<NodeInfo> closestNodes)
        {
            AreaInfo areaInfo;
            if (!_dataStore.TryGetValue(target, out var value))
            {
                areaInfo = new AreaInfo
                {
                    Chunk = chunk,
                };
            }
            else
            {
                areaInfo = JsonConvert.DeserializeObject<AreaInfo>(value);
            }

            areaInfo.Nodes.Add(_routingTable.SelfNode);
            _dataStore.Store(target, areaInfo.ToString());

            foreach (var node in closestNodes)
            {
                _kademlia.Store(node, target, areaInfo.ToString());
            }
        }

        private HashSet<Area> GetSurroundingChunks(int sizeIndex, Area area)
        {
            var surroundingChunks = new HashSet<Area>();
            for (int x = -sizeIndex; x <= sizeIndex; x++)
            {
                for (int y = -sizeIndex; y <= sizeIndex; y++)
                {
                    for (int z = -sizeIndex; z <= sizeIndex; z++)
                    {
                        var newArea = new Area(area.X + x, area.Y + y, area.Z + z);
                        surroundingChunks.Add(newArea);
                    }
                }
            }

            return surroundingChunks;
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}

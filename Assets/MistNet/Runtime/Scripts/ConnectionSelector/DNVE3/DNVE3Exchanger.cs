using System;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;
using System.Threading;
using Cysharp.Threading.Tasks;
using MistNet.Utils;
using Newtonsoft.Json;
using UnityEngine;
using MistNet;

namespace MistNet.DNVE3
{
    public class DNVE3Exchanger : IDisposable
    {
        private readonly IMessageSender _sender;
        private readonly RoutingBase _routingBase;
        private readonly INodeListStore _dataStore;
        private readonly CancellationTokenSource _cts = new();
        private readonly DNVE3DataStore _dnveDataStore;

        public void Dispose()
        {

            _cts.Cancel();
            _cts.Dispose();
        }

        public DNVE3Exchanger(IMessageSender sender, INodeListStore dataStore, DNVE3DataStore dnve3DataStore, RoutingBase routingBase)
        {
            _sender = sender;
            _dataStore = dataStore;
            _dnveDataStore = dnve3DataStore;
            _routingBase = routingBase;
            _sender.RegisterReceive(DNVEMessageType.Heartbeat, OnHeartbeatReceived);
            SendLoop(_cts.Token).Forget();
        }

        private void OnHeartbeatReceived(DNVEMessage message)
        {
            SpatialDensityData data;
            try 
            {
                var byteData = MemoryPackSerializer.Deserialize<SpatialDensityDataByte>(message.Payload);
                data = SpatialDensityUtils.FromCompact(byteData, OptConfig.Data.SpatialDistanceLayers);
            }
            catch
            {
                data = MemoryPackSerializer.Deserialize<SpatialDensityData>(message.Payload);
            }

            // _dnveDataStore.NodeMaps[message.Sender] = data;
            var expireTime = DateTime.UtcNow.AddSeconds(OptConfig.Data.ExpireSeconds);
            // _dnveDataStore.ExpireNodeTimes[message.Sender] = expireTime;
            _dnveDataStore.AddOrUpdateNeighbor(message.Sender, data, expireTime);

            var node = new Node
            {
                Id = message.Sender,
                Position = data.Position
            };
            _dataStore.AddOrUpdate(node);
        }

        private List<Node> GetNodes()
        {
            var nodes = new List<Node>();
            var nodeIds = _routingBase.ConnectedNodes;

            if (nodeIds.Count == 0) return nodes;

            foreach (var nodeId in nodeIds)
            {
                if (!_dataStore.TryGet(nodeId, out var node)) continue;
                nodes.Add(node);
            }

            return nodes;
        }

        private async UniTask SendLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (token.IsCancellationRequested) break;
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.HeartbeatIntervalSeconds),
                    cancellationToken: token);
                DeleteOldData();
                var selfHistData = GetSpatialHistogramData(GetNodes().ToArray());
                _dnveDataStore.SelfDensity = new SpatialDensityData
                {
                    DensityMap = (float[,])selfHistData.DensityMap.Clone(),
                    Position = selfHistData.Position,
                };

                foreach (var (_, data) in _dnveDataStore.Neighbors)
                {
                    var otherPos = data.Data.Position;
                    var hist = data.Data.DensityMap;
                    selfHistData.DensityMap = SpatialDensityUtils.MergeSpatialDensity(selfHistData.DensityMap, selfHistData.Position.ToVector3(), hist, otherPos.ToVector3(), OptConfig.Data.SpatialDistanceLayers);
                }
                _dnveDataStore.MergedDensityMap = selfHistData.DensityMap;

                var compactData = SpatialDensityUtils.ToCompact(selfHistData);
                var payload = MemoryPackSerializer.Serialize(compactData);

                foreach (var nodeId in _routingBase.ConnectedNodes.ToArray())
                {
                    var message = new DNVEMessage
                    {
                        Type = DNVEMessageType.Heartbeat,
                        Payload = payload,
                        Receiver = nodeId,
                    };
                    _sender.Send(message);
                    await UniTask.Yield();
                }
            }
        }

        private void DeleteOldData()
        {
            var now = DateTime.UtcNow;
            var toRemove = new List<NodeId>();
            foreach (var kvp in _dnveDataStore.Neighbors)
            {
                var nodeId = kvp.Key;
                var lastUpdateTime = kvp.Value.LastMessageTime;
                if ((now - lastUpdateTime).TotalSeconds > OptConfig.Data.ExpireSeconds)
                {
                    toRemove.Add(nodeId);
                }
            }
            foreach (var nodeId in toRemove)
            {
                // _dnveDataStore.NodeMaps.Remove(nodeId);
                // _dnveDataStore.ExpireNodeTimes.Remove(nodeId);
                // _dnveDataStore.LastMessageTimes.Remove(nodeId);
                _dnveDataStore.RemoveNeighbor(nodeId);
                _dataStore.Remove(nodeId);
            }
        }

        private SpatialDensityData GetSpatialHistogramData(Node[] nodes)
        {
            var selfPos = MistSyncManager.I.SelfSyncObject.transform.position;
            var posArray = new Vector3[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                posArray[i] = nodes[i].Position.ToVector3();
            }

            var hists = SpatialDensityUtils.CreateSpatialDensity(selfPos, posArray, OptConfig.Data.SpatialDistanceLayers);

            return new SpatialDensityData
            {
                DensityMap = hists,
                Position = new Position(selfPos),
            };
        }
    }
}

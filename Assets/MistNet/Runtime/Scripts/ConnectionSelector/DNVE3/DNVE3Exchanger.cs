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

        private readonly List<NodeId> _toRemove = new();
        private readonly List<Node> _tempNodeList = new();
        
        private float[,] _selfDensityBuffer;
        private float[,] _mergedDensityBuffer;
        private byte[] _compactPayloadBuffer;
        private Vector3[] _posBuffer;
        private DNVEMessage _heartbeatMessage;
        private SpatialDensityData _selfDensityData;
        private NodeId[] _connectedNodesBuffer = Array.Empty<NodeId>();

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
            
            InitializeBuffers();
            
            _sender.RegisterReceive(DNVEMessageType.Heartbeat, OnHeartbeatReceived);
            SendLoop(_cts.Token).Forget();
        }

        private void InitializeBuffers()
        {
            var dirCount = SpatialDensityUtils.Directions.Length;
            var layerCount = OptConfig.Data.SpatialDistanceLayers;
            
            _selfDensityBuffer = new float[dirCount, layerCount];
            _mergedDensityBuffer = new float[dirCount, layerCount];
            _compactPayloadBuffer = new byte[dirCount * layerCount];
            _posBuffer = new Vector3[OptConfig.Data.MaxConnectionCount];
            
            _selfDensityData = new SpatialDensityData
            {
                DensityMap = _selfDensityBuffer,
                Position = new Position(Vector3.zero)
            };

            _heartbeatMessage = new DNVEMessage
            {
                Type = DNVEMessageType.Heartbeat
            };
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

            _dnveDataStore.AddOrUpdateNeighbor(message.Sender, data);

            if (!_dataStore.TryGet(message.Sender, out var node))
            {
                node = new Node { Id = message.Sender };
            }
            node.Position = data.Position;
            _dataStore.AddOrUpdate(node);
        }

        private void GetNodes(List<Node> nodes)
        {
            nodes.Clear();
            var nodeIds = _routingBase.ConnectedNodes;

            if (nodeIds.Count == 0) return;

            foreach (var nodeId in nodeIds)
            {
                if (!_dataStore.TryGet(nodeId, out var node)) continue;
                nodes.Add(node);
            }
        }

        private async UniTask SendLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (token.IsCancellationRequested) break;
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.HeartbeatIntervalSeconds),
                    cancellationToken: token);
                DeleteOldData();

                GetNodes(_tempNodeList);
                UpdateSpatialHistogramData(_tempNodeList);
                
                _dnveDataStore.SelfDensity = _selfDensityData; 

                Array.Copy(_selfDensityBuffer, _mergedDensityBuffer, _selfDensityBuffer.Length);

                foreach (var (_, info) in _dnveDataStore.Neighbors)
                {
                    var otherPos = info.Data.Position;
                    var otherDensityMap = info.Data.DensityMap;
                    SpatialDensityUtils.MergeSpatialDensity(_mergedDensityBuffer, _selfDensityData.Position.ToVector3(), otherDensityMap, otherPos.ToVector3(), _mergedDensityBuffer, OptConfig.Data.SpatialDistanceLayers);
                }
                _dnveDataStore.MergedDensityMap = _mergedDensityBuffer;

                var compactData = SpatialDensityUtils.ToCompact(_selfDensityData, _compactPayloadBuffer);
                var payload = MemoryPackSerializer.Serialize(compactData);
                _heartbeatMessage.Payload = payload;

                var connectedCount = _routingBase.ConnectedNodes.Count;
                if (_connectedNodesBuffer.Length < connectedCount)
                {
                    _connectedNodesBuffer = new NodeId[connectedCount];
                }
                _routingBase.ConnectedNodes.CopyTo(_connectedNodesBuffer);

                for (int i = 0; i < connectedCount; i++)
                {
                    var nodeId = _connectedNodesBuffer[i];
                    _heartbeatMessage.Receiver = nodeId;
                    _sender.Send(_heartbeatMessage);
                    await UniTask.Yield();
                }
            }
        }

        private void DeleteOldData()
        {
            var now = DateTime.UtcNow;
            _toRemove.Clear();

            foreach (var kvp in _dnveDataStore.LastUpdateTimes)
            {
                var nodeId = kvp.Key;
                var lastUpdateTime = kvp.Value;
                if ((now - lastUpdateTime).TotalSeconds > OptConfig.Data.ExpireSeconds)
                {
                    _toRemove.Add(nodeId);
                }
            }
            foreach (var nodeId in _toRemove)
            {
                _dnveDataStore.RemoveNeighbor(nodeId);
                _dataStore.Remove(nodeId);
            }
        }

        private void UpdateSpatialHistogramData(List<Node> nodes)
        {
            var selfPos = MistSyncManager.I.SelfSyncObject.transform.position;
            
            if (_posBuffer.Length < nodes.Count)
            {
                _posBuffer = new Vector3[nodes.Count];
            }
            
            for (int i = 0; i < nodes.Count; i++)
            {
                _posBuffer[i] = nodes[i].Position.ToVector3();
            }

            var maxRange = OptConfig.Data.AoiRange * 2.0f;
            SpatialDensityUtils.CreateSpatialDensity(selfPos, _posBuffer, nodes.Count, _selfDensityBuffer, maxRange, OptConfig.Data.SpatialDistanceLayers);
            _selfDensityData.Position = new Position(selfPos);
        }
    }
}

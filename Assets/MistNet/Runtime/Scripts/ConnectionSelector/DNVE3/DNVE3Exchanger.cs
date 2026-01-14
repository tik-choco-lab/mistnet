using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MistNet.Utils;
using Newtonsoft.Json;
using UnityEngine;

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
            var data = JsonConvert.DeserializeObject<SpatialHistogramData>(message.Payload);
            _dnveDataStore.NodeMaps[message.Sender] = data;
            var expireTime = DateTime.UtcNow.AddSeconds(OptConfig.Data.ExpireSeconds);
            _dnveDataStore.ExpireNodeTimes[message.Sender] = expireTime;

            var node = new Node { Id = message.Sender, Position = data.Position };
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
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.HeartbeatIntervalSeconds), cancellationToken: token);
                DeleteOldData();
                var selfHistData = GetSpatialHistogramData(GetNodes().ToArray());
                
                _dnveDataStore.SelfData = new SpatialHistogramData
                {
                    Hists = (float[,])selfHistData.Hists.Clone(),
                    Position = selfHistData.Position,
                };

                _dnveDataStore.LocalDensityMap = (float[,])selfHistData.Hists.Clone();

                Array.Clear(_dnveDataStore.MergedHistogram, 0, _dnveDataStore.MergedHistogram.Length);
                Array.Clear(_dnveDataStore.ConfidenceMap, 0, _dnveDataStore.ConfidenceMap.Length);

                var dirs = SphericalHistogramUtils.Directions.Length;
                var bins = SphericalHistogramUtils.DistBins;
                
                for (int i = 0; i < dirs; i++)
                {
                    for (int j = 0; j < bins; j++)
                    {
                        _dnveDataStore.MergedHistogram[i, j] = _dnveDataStore.LocalDensityMap[i, j];
                        _dnveDataStore.ConfidenceMap[i, j] = 1.0f;
                    }
                }

                var now = DateTime.UtcNow;
                var selfPos = selfHistData.Position.ToVector3();
                float decayRate = OptConfig.Data.ExpireSeconds > 0 ? 2.0f / OptConfig.Data.ExpireSeconds : 1.0f;

                foreach (var (nodeId, data) in _dnveDataStore.NodeMaps)
                {
                    if (!_dnveDataStore.LastMessageTimes.TryGetValue(nodeId, out var lastTime)) continue;

                    var elapsed = (float)(now - lastTime).TotalSeconds;
                    var weight = Mathf.Exp(-elapsed * decayRate);
                    var projected = SphericalHistogramUtils.ProjectSphericalHistogram(data.Hists, data.Position.ToVector3(), selfPos);
                    
                    for (int i = 0; i < dirs; i++)
                    {
                        for (int j = 0; j < bins; j++)
                        {
                            _dnveDataStore.MergedHistogram[i, j] += projected[i, j] * weight;
                            _dnveDataStore.ConfidenceMap[i, j] += weight;
                        }
                    }
                }

                _dnveDataStore.SelfData.Hists = (float[,])_dnveDataStore.MergedHistogram.Clone();
                var json = JsonConvert.SerializeObject(_dnveDataStore.SelfData);

                foreach (var nodeId in _routingBase.ConnectedNodes.ToArray())
                {
                    var message = new DNVEMessage { Type = DNVEMessageType.Heartbeat, Payload = json, Receiver = nodeId };
                    _sender.Send(message);
                    await UniTask.Yield();
                }
            }
        }

        private void DeleteOldData()
        {
            var now = DateTime.UtcNow;
            var toRemove = new List<NodeId>();
            foreach (var kvp in _dnveDataStore.ExpireNodeTimes)
            {
                if ((now - kvp.Value).TotalSeconds > OptConfig.Data.ExpireSeconds) toRemove.Add(kvp.Key);
            }
            foreach (var nodeId in toRemove)
            {
                _dnveDataStore.NodeMaps.Remove(nodeId);
                _dnveDataStore.ExpireNodeTimes.Remove(nodeId);
                _dnveDataStore.LastMessageTimes.Remove(nodeId);
                _dataStore.Remove(nodeId);
            }
        }

        private SpatialHistogramData GetSpatialHistogramData(Node[] nodes)
        {
            var selfPos = MistSyncManager.I.SelfSyncObject.transform.position;
            var posArray = nodes.Select(n => n.Position.ToVector3()).ToArray();
            var hists = SphericalHistogramUtils.CreateSphericalHistogram(selfPos, posArray);

            return new SpatialHistogramData { Hists = hists, Position = new Position(selfPos) };
        }
    }
}

using System;
using System.Collections.Generic;
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

        public DNVE3Exchanger(IMessageSender sender, INodeListStore dataStore, DNVE3DataStore dnve3DataStore)
        {
            _sender = sender;
            _dataStore = dataStore;
            _dnveDataStore = dnve3DataStore;
            _routingBase = MistManager.I.Routing;
            _sender.RegisterReceive(DNVEMessageType.Heartbeat, OnHeartbeatReceived);
            SendLoop(_cts.Token).Forget();
        }

        private void OnHeartbeatReceived(DNVEMessage message)
        {
            var data = JsonConvert.DeserializeObject<SpatialHistogramData>(message.Payload);
            _dnveDataStore.NodeMaps[message.Sender] = data;
            var expireTime = DateTime.UtcNow.AddSeconds(OptConfig.Data.ExpireSeconds);
            _dnveDataStore.ExpireNodeTimes[message.Sender] = expireTime;
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
                // copy渡し
                _dnveDataStore.SelfData = new SpatialHistogramData
                {
                    Hists = (float[,])selfHistData.Hists.Clone(),
                    Position = selfHistData.Position,
                };

                // merge
                foreach (var (_, data) in _dnveDataStore.NodeMaps)
                {
                    var otherPos = data.Position;
                    var hist = data.Hists;
                    selfHistData.Hists = SphericalHistogramUtils.MergeHistograms(selfHistData.Hists, selfHistData.Position.ToVector3(), hist, otherPos.ToVector3());
                }
                _dnveDataStore.MergedHistogram = selfHistData.Hists;

                // send
                var json = JsonConvert.SerializeObject(selfHistData);

                foreach (var nodeId in _routingBase.ConnectedNodes)
                {
                    var message = new DNVEMessage
                    {
                        Type = DNVEMessageType.Heartbeat,
                        Payload = json,
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
            foreach (var kvp in _dnveDataStore.ExpireNodeTimes)
            {
                var nodeId = kvp.Key;
                var lastUpdateTime = kvp.Value;
                if ((now - lastUpdateTime).TotalSeconds > OptConfig.Data.ExpireSeconds)
                {
                    toRemove.Add(nodeId);
                }
            }
            foreach (var nodeId in toRemove)
            {
                _dnveDataStore.NodeMaps.Remove(nodeId);
                _dnveDataStore.ExpireNodeTimes.Remove(nodeId);
            }
        }

        private SpatialHistogramData GetSpatialHistogramData(Node[] nodes)
        {
            var selfPos = MistSyncManager.I.SelfSyncObject.transform.position;
            var posArray = new Vector3[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                posArray[i] = nodes[i].Position.ToVector3();
            }

            var hists = SphericalHistogramUtils.CreateSphericalHistogram(selfPos, posArray);

            return new SpatialHistogramData
            {
                Hists = hists,
                Position = new Position(selfPos),
            };
        }
    }
}

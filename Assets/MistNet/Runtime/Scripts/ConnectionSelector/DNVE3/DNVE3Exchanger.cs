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
        private readonly Dictionary<NodeId, DirectionalFeatureData> _nodeMaps = new(); // TODO: dataStoreに入れるべきかも
        private readonly Dictionary<NodeId, DateTime> _expireNodeTimes = new();

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        public DNVE3Exchanger(IMessageSender sender, INodeListStore dataStore)
        {
            _sender = sender;
            _dataStore = dataStore;
            _routingBase = MistManager.I.Routing;
            _sender.RegisterReceive(DNVEMessageType.Heartbeat, OnHeartbeatReceived);
            SendLoop(_cts.Token).Forget();
        }

        private void OnHeartbeatReceived(DNVEMessage message)
        {
            var data = JsonConvert.DeserializeObject<DirectionalFeatureData>(message.Payload);
            _nodeMaps[message.Sender] = data;
            var expireTime = DateTime.UtcNow.AddSeconds(OptConfig.Data.ExpireSeconds);
            _expireNodeTimes[message.Sender] = expireTime;
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
                var selfHistData = GetDirectionalFeatureData(GetNodes().ToArray());
                var selfPos = MistSyncManager.I.SelfSyncObject.transform.position;

                // merge
                foreach (var (_, data) in _nodeMaps)
                {
                    var otherPos = data.Position;
                    var hist = data.Hists;
                    selfHistData.Hists = SphericalHistogramUtils.MergeHistograms(selfHistData.Hists, selfPos, hist, otherPos);
                }

                var json = JsonConvert.SerializeObject(selfHistData);
                var message = new DNVEMessage
                {
                    Type = DNVEMessageType.Heartbeat,
                    Payload = json
                };
                _sender.Send(message);
            }
        }

        private void DeleteOldData()
        {
            var now = DateTime.UtcNow;
            var toRemove = new List<NodeId>();
            foreach (var kvp in _expireNodeTimes)
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
                _nodeMaps.Remove(nodeId);
                _expireNodeTimes.Remove(nodeId);
            }
        }

        private DirectionalFeatureData GetDirectionalFeatureData(Node[] nodes)
        {
            var selfPos = MistSyncManager.I.SelfSyncObject.transform.position;
            var posArray = new Vector3[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                posArray[i] = nodes[i].Position.ToVector3();
            }

            var hists = SphericalHistogramUtils.CreateSphericalHistogram(selfPos, posArray);

            return new DirectionalFeatureData
            {
                Hists = hists
            };
        }
    }
}

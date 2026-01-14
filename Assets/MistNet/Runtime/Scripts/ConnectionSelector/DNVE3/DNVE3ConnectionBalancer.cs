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
    public class DNVE3ConnectionBalancer : IDisposable
    {
        private const float JitterRatio = 0.2f;
        private readonly DNVE3DataStore _dnveDataStore;
        private readonly CancellationTokenSource _cts = new();
        private readonly IMessageSender _sender;
        private readonly NodeListStore _dataStore;
        private readonly RoutingBase _routing;
        private readonly ILayer _layer;
        private readonly IPeerRepository _peerRepository;
        
        private readonly Dictionary<NodeId, DateTime> _disconnectCooldowns = new();
        private const double CooldownSeconds = 5.0;

        private readonly Dictionary<NodeId, Node> _nearbyNodesCache = new();
        private const double CacheExpireSeconds = 30.0;

        private readonly Dictionary<NodeId, DateTime> _recentlyVisible = new();
        private const double VisibleGraceSeconds = 30.0;

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        
        public DNVE3ConnectionBalancer(IMessageSender sender, NodeListStore dataStore, DNVE3DataStore dnveDataStore, ILayer layer, RoutingBase routingBase, IPeerRepository peerRepository)
        {
            _layer = layer;
            _sender = sender;
            _dnveDataStore = dnveDataStore;
            _dataStore = dataStore;
            _routing = routingBase;
            _peerRepository = peerRepository;
            LoopBalanceConnections(_cts.Token).Forget();
            _sender.RegisterReceive(DNVEMessageType.RequestNodeList, OnRequestNodeListReceived);
            _sender.RegisterReceive(DNVEMessageType.NodeList, OnNodeListReceived);
        }

        private async UniTask LoopBalanceConnections(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var interval = OptConfig.Data.ConnectionBalancerIntervalSeconds;
                var jitter = UnityEngine.Random.Range(0f, interval * JitterRatio);
                await UniTask.Delay(TimeSpan.FromSeconds(interval + jitter), cancellationToken: token);

                if (_dnveDataStore.SelfData == null || _dnveDataStore.MergedHistogram == null) continue;
                
                var importantNodes = FindImportantNode();
                var count = Math.Min(OptConfig.Data.ExchangeCount, importantNodes.Count);
                for (var i = 0; i < count; i++) SendRequestNodeList(importantNodes[i].nodeId);

                await UniTask.Delay(TimeSpan.FromSeconds(interval + jitter), cancellationToken: token);
                SelectConnection();
            }
        }

        private void SelectConnection()
        {
            var selfPos = MistSyncManager.I.SelfSyncObject.transform.position;
            var now = DateTime.UtcNow;

            var expiredCooldowns = _disconnectCooldowns.Where(kv => (now - kv.Value).TotalSeconds > CooldownSeconds).Select(kv => kv.Key).ToList();
            foreach (var id in expiredCooldowns) _disconnectCooldowns.Remove(id);

            var expiredCache = _nearbyNodesCache.Where(kv => (now - _dnveDataStore.LastMessageTimes.GetValueOrDefault(kv.Key, DateTime.MinValue)).TotalSeconds > CacheExpireSeconds && !_dataStore.TryGet(kv.Key, out _)).Select(kv => kv.Key).ToList();
            foreach (var id in expiredCache) _nearbyNodesCache.Remove(id);

            foreach (var id in _routing.MessageNodes) _recentlyVisible[id] = now;
            var expiredVisible = _recentlyVisible
                .Where(kv => !_routing.MessageNodes.Contains(kv.Key) && (now - kv.Value).TotalSeconds > VisibleGraceSeconds)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var id in expiredVisible) _recentlyVisible.Remove(id);

            var allNodesDict = _dataStore.GetAllNodes().ToDictionary(n => n.Id);
            foreach (var kv in _nearbyNodesCache)
            {
                if (!allNodesDict.ContainsKey(kv.Key)) allNodesDict[kv.Key] = kv.Value;
            }
            var allNodes = allNodesDict.Values.ToList();

            var connectedIds = _routing.ConnectedNodes.ToHashSet();
            var mergedHist = _dnveDataStore.MergedHistogram;
            var targetCount = OptConfig.Data.MaxConnectionCount;

            var candidates = new List<Node>();
            const float threshold = 0.05f;

            foreach (var node in allNodes)
            {
                if (node.Id == _peerRepository.SelfId) continue;
                if (_disconnectCooldowns.ContainsKey(node.Id)) continue;

                var vec = node.Position.ToVector3() - selfPos;
                var distSq = vec.sqrMagnitude;
                if (distSq == 0) continue;
                
                if (distSq <= OptConfig.Data.AoiRange * OptConfig.Data.AoiRange)
                {
                    _nearbyNodesCache[node.Id] = node;
                }
                
                if (connectedIds.Contains(node.Id) || allNodes.Count <= targetCount + 1 || distSq <= OptConfig.Data.AoiRange * OptConfig.Data.AoiRange)
                {
                    candidates.Add(node);
                    continue;
                }

                int dirIdx = GetDirectionIndex(vec.normalized);
                float densitySum = 0f;
                if (dirIdx >= 0)
                {
                    for (int j = 0; j < SphericalHistogramUtils.DistBins; j++) densitySum += mergedHist[dirIdx, j];
                }

                if (densitySum >= threshold) candidates.Add(node);
            }

            var scoredCandidates = candidates.Select(n => new 
            { 
                Node = n, 
                Score = CalculateCompositeScore(n, selfPos, mergedHist, connectedIds) 
            }).OrderByDescending(x => x.Score).ToList();

            var selectedIds = scoredCandidates.Take(targetCount).Select(x => x.Node.Id).ToHashSet();

            int excess = Math.Max(0, _routing.ConnectedNodes.Count - targetCount);
            int disconnectLimit = excess;
            if (_routing.ConnectedNodes.Count >= targetCount)
            {
                disconnectLimit += OptConfig.Data.ForceDisconnectCount;
            }
            
            int disconnectedCount = 0;

            foreach (var id in _routing.ConnectedNodes.Where(id => !selectedIds.Contains(id)).ToList())
            {
                if (disconnectedCount >= disconnectLimit || id == _peerRepository.SelfId) continue;
                if (!_layer.Transport.IsConnectingOrConnected(id)) continue;
                if (_routing.MessageNodes.Contains(id)) continue;
                if (_recentlyVisible.TryGetValue(id, out var lastVisible) && (now - lastVisible).TotalSeconds <= VisibleGraceSeconds) continue;
                
                _layer.Transport.Disconnect(id);
                _disconnectCooldowns[id] = now;
                disconnectedCount++;
            }

            foreach (var id in selectedIds)
            {
                if (id == _peerRepository.SelfId || _layer.Transport.IsConnectingOrConnected(id)) continue;
                _layer.Transport.Connect(id);
            }
        }

        private float CalculateCompositeScore(Node node, Vector3 selfPos, float[,] mergedHist, HashSet<NodeId> connectedIds)
        {
            var vec = node.Position.ToVector3() - selfPos;
            var dist = vec.magnitude;
            var score = 1000f / (1f + dist);
            
            int dirIdx = GetDirectionIndex(vec.normalized);
            if (dirIdx >= 0)
            {
                float density = 0f;
                for (int j = 0; j < SphericalHistogramUtils.DistBins; j++) density += mergedHist[dirIdx, j];
                score += density * 10f; 
            }

            if (connectedIds.Contains(node.Id) && _dnveDataStore.LastMessageTimes.TryGetValue(node.Id, out var lastTime))
            {
                score -= (float)(DateTime.UtcNow - lastTime).TotalSeconds * 0.1f; 
            }
            return score;
        }

        private int GetDirectionIndex(Vector3 dir)
        {
            int bestIdx = -1;
            float maxDot = -1f;
            for (int i = 0; i < SphericalHistogramUtils.Directions.Length; i++)
            {
                var dot = Vector3.Dot(SphericalHistogramUtils.Directions[i], dir);
                if (dot > maxDot) { maxDot = dot; bestIdx = i; }
            }
            return bestIdx;
        }

        private void SendRequestNodeList(NodeId nodeId)
        {
            var message = new DNVEMessage { Type = DNVEMessageType.RequestNodeList, Payload = string.Empty, Receiver = nodeId };
            _sender.Send(message);
        }

        private void OnRequestNodeListReceived(DNVEMessage receiveMessage)
        {
            var connectedAllNodes = _dataStore.GetAllNodes().Where(n => _routing.ConnectedNodes.Contains(n.Id)).ToList();
            var message = new DNVEMessage { Type = DNVEMessageType.NodeList, Payload = JsonConvert.SerializeObject(connectedAllNodes), Receiver = receiveMessage.Sender };
            _sender.Send(message);
        }

        private void OnNodeListReceived(DNVEMessage receiveMessage)
        {
            var nodes = JsonConvert.DeserializeObject<List<Node>>(receiveMessage.Payload);
            var expireTime = DateTime.UtcNow.AddSeconds(OptConfig.Data.ExpireSeconds);
            foreach (var node in nodes)
            {
                _routing.AddRouting(node.Id, receiveMessage.Sender);
                _dataStore.AddOrUpdate(node);
                _dnveDataStore.ExpireNodeTimes[node.Id] = expireTime;
            }
        }

        private List<(NodeId nodeId, float score)> FindImportantNode()
        {
            var selfHist = _dnveDataStore.SelfData.Hists;
            var selfCenter = _dnveDataStore.SelfData.Position.ToVector3();
            var importantNodes = new List<(NodeId nodeId, float score)>();

            foreach (var (nodeId, nodeData) in _dnveDataStore.NodeMaps)
            {
                var projected = SphericalHistogramUtils.ProjectSphericalHistogram(nodeData.Hists, nodeData.Position.ToVector3(), selfCenter);
                var score = 0f;
                for (var j = 0; j < SphericalHistogramUtils.DistBins; j++)
                {
                    var weight = 1f / (1f + j);
                    for (var i = 0; i < SphericalHistogramUtils.Directions.Length; i++)
                    {
                        var diff = projected[i, j] - selfHist[i, j];
                        if (diff > 0f) score += diff * weight;
                    }
                }
                if (score > 0f) importantNodes.Add((nodeId, score));
            }
            importantNodes.Sort((a, b) => b.score.CompareTo(a.score));
            return importantNodes;
        }
    }
}

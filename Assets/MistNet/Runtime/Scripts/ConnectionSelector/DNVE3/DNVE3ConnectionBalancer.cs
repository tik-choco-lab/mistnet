using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MistNet.Utils;
using Newtonsoft.Json;
using UnityEngine;
using MemoryPack;
using MistNet;

namespace MistNet.DNVE3
{
    public class DNVE3ConnectionBalancer : IDisposable
    {
        private const float JitterRatio = 0.2f;
        private const float DirectionThreshold = 0.7f;
        private const float ScoreSelectedNode = 10000f;
        private const float ScoreAoiNodeUnknownPos = 100f;
        private const float PenaltyNoMessageHistory = 1000f;
        private const int ReservedConnectionCount = 1;
        private const float BaseWeight = 1f;

        private readonly DNVE3DataStore _dnveDataStore;
        private readonly CancellationTokenSource _cts = new();
        private readonly IMessageSender _sender;
        private readonly NodeListStore _dataStore;
        private readonly RoutingBase _routing;
        private readonly ILayer _layer;
        private readonly IPeerRepository _peerRepository;

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
                await UniTask.Delay(TimeSpan.FromSeconds(interval + jitter),
                    cancellationToken: token);

                if (_dnveDataStore.SelfDensity == null) continue;
                if (_dnveDataStore.MergedDensityMap == null) continue;
                var importantNodes= FindImportantNode();

                var count = Math.Min(OptConfig.Data.ExchangeCount, importantNodes.Count);
                for (var i = 0; i < count; i++)
                {
                    SendRequestNodeList(importantNodes[i].nodeId);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(interval + jitter),
                    cancellationToken: token);

                SelectConnection();
            }
        }

        private void SelectConnection()
        {
            var allNodes = _dataStore.GetAllNodes().ToList();
            var selfPos = MistSyncManager.I.SelfSyncObject.transform.position;

            var directions = SpatialDensityUtils.Directions;
            var selectedNodes = new List<Node>();

            foreach (var dir in directions)
            {
                Node closest = null;
                var minDist = float.MaxValue;

                foreach (var node in allNodes)
                {
                    var vec = node.Position.ToVector3() - selfPos;
                    var dot = Vector3.Dot(vec.normalized, dir);
                    if (dot < DirectionThreshold) continue;

                    var dist = vec.magnitude;
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closest = node;
                    }
                }

                if (closest != null && !selectedNodes.Contains(closest))
                    selectedNodes.Add(closest);
            }

            var selectedNodeIds = selectedNodes.Select(n => n.Id).ToHashSet();

            var targetConnectionCount = OptConfig.Data.MaxConnectionCount - ReservedConnectionCount;
            if (_routing.ConnectedNodes.Count > targetConnectionCount)
            {
                var numToDisconnect = _routing.ConnectedNodes.Count - targetConnectionCount;
                numToDisconnect += OptConfig.Data.ForceDisconnectCount;

                var nodesWithScore = _routing.ConnectedNodes
                    .Select(id => new { Id = id, Score = CalculateNodeScore(id, selectedNodeIds, selfPos) })
                    .OrderBy(x => x.Score)
                    .ToList();

                var disconnectedCount = 0;
                for (var i = 0; i < nodesWithScore.Count && disconnectedCount < numToDisconnect; i++)
                {
                    var nodeId = nodesWithScore[i].Id;
                    if (nodeId == _peerRepository.SelfId) continue;
                    if (!_layer.Transport.IsConnectingOrConnected(nodeId)) continue;
                    
                    _layer.Transport.Disconnect(nodeId);
                    disconnectedCount++;
                }
            }

            foreach (var node in selectedNodes)
            {
                var nodeId = node.Id;
                if (nodeId == _peerRepository.SelfId) continue;
                if (_layer.Transport.IsConnectingOrConnected(nodeId)) continue;

                if (_routing.ConnectedNodes.Count >= OptConfig.Data.MaxConnectionCount)
                {
                    var worstNode = _routing.ConnectedNodes
                        .Select(id => new { Id = id, Score = CalculateNodeScore(id, selectedNodeIds, selfPos) })
                        .OrderBy(x => x.Score)
                        .FirstOrDefault();

                    if (worstNode != null)
                    {
                        _layer.Transport.Disconnect(worstNode.Id);
                    }
                }
                
                _layer.Transport.Connect(nodeId);
            }
        }

        private float CalculateNodeScore(NodeId id, HashSet<NodeId> selectedNodeIds, Vector3 selfPos)
        {
            var score = 0f;

            if (selectedNodeIds.Contains(id)) score += ScoreSelectedNode;

            if (_routing.MessageNodes.Contains(id))
            {
                if (_dataStore.TryGet(id, out var node))
                {
                    var dist = Vector3.Distance(selfPos, node.Position.ToVector3());
                    score += Mathf.Max(0, OptConfig.Data.AoiRange - dist);
                }
                else
                {
                    score += ScoreAoiNodeUnknownPos;
                }
            }

            if (_dnveDataStore.Neighbors.TryGetValue(id, out var info))
            {
                var elapsed = (float)(DateTime.UtcNow - info.LastMessageTime).TotalSeconds;
                score -= elapsed;
            }
            else
            {
                score -= PenaltyNoMessageHistory;
            }

            return score;
        }

        private void SendRequestNodeList(NodeId nodeId)
        {
            var message = new DNVEMessage
            {
                Type = DNVEMessageType.RequestNodeList,
                Payload = Array.Empty<byte>(),
                Receiver = nodeId,
            };
            _sender.Send(message);
        }

        private void OnRequestNodeListReceived(DNVEMessage receiveMessage)
        {
            var allNodes = _dataStore.GetAllNodes().ToHashSet();
            var connectedAllNodes = allNodes
                .Where(n => _routing.ConnectedNodes.Contains(n.Id))
                .ToList();
            var payload = MemoryPackSerializer.Serialize(connectedAllNodes);
            var message = new DNVEMessage
            {
                Type = DNVEMessageType.NodeList,
                Payload = payload,
                Receiver = receiveMessage.Sender,
            };
            _sender.Send(message);
        }

        private void OnNodeListReceived(DNVEMessage receiveMessage)
        {
            var nodes = MemoryPackSerializer.Deserialize<List<Node>>(receiveMessage.Payload);
            foreach (var node in nodes)
            {
                _routing.AddRouting(node.Id, receiveMessage.Sender);
                _dataStore.AddOrUpdate(node);
            }
        }

        private List<(NodeId nodeId, float score)> FindImportantNode()
        {
            var otherNodes = _dnveDataStore.Neighbors;
            var selfDensityMap = _dnveDataStore.SelfDensity.DensityMap;
            var selfCenter = _dnveDataStore.SelfDensity.Position;

            var importantNodes = new List<(NodeId nodeId, float score)>();

            foreach (var (nodeId, nodeData) in otherNodes)
            {
                var otherDensityMap = nodeData.Data.DensityMap;
                var otherCenter = nodeData.Data.Position;

                var projected = SpatialDensityUtils.ProjectSpatialDensity(
                    otherDensityMap, otherCenter.ToVector3(), selfCenter.ToVector3(), OptConfig.Data.SpatialDistanceLayers
                );

                var score = 0f;
                var layerCount = OptConfig.Data.SpatialDistanceLayers;

                for (var j = 0; j < layerCount; j++)
                {
                    var weight = BaseWeight / (BaseWeight + j);

                    for (var i = 0; i < SpatialDensityUtils.Directions.Length; i++)
                    {
                        var diff = projected[i, j] - selfDensityMap[i, j];
                        if (diff > 0f)
                            score += diff * weight;
                    }
                }

                if (score > 0f)
                    importantNodes.Add((nodeId, score));
            }

            importantNodes.Sort((a, b) => b.score.CompareTo(a.score));

            foreach (var (nodeId, score) in importantNodes)
                 MistLogger.Debug($"[Important Node] {nodeId} - Score: {score:F3}");

            if (importantNodes.Count > 0)
                MistLogger.Debug($"The node with the strongest short-range influence: {importantNodes[0].nodeId}");

            return importantNodes;
        }
    }
}

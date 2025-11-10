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
        private readonly DNVE3DataStore _dnveDataStore;
        private readonly CancellationTokenSource _cts = new();
        private readonly IMessageSender _sender;
        private readonly NodeListStore _dataStore;
        private readonly RoutingBase _routing;

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        
        public DNVE3ConnectionBalancer(IMessageSender sender, NodeListStore dataStore, DNVE3DataStore dnveDataStore)
        {
            _sender = sender;
            _dnveDataStore = dnveDataStore;
            _dataStore = dataStore;
            _routing = MistManager.I.Routing;
            LoopBalanceConnections(_cts.Token).Forget();
            _sender.RegisterReceive(DNVEMessageType.RequestNodeList, OnRequestNodeListReceived);
            _sender.RegisterReceive(DNVEMessageType.NodeList, OnNodeListReceived);
        }

        private async UniTask LoopBalanceConnections(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.ConnectionBalancerIntervalSeconds),
                    cancellationToken: token);

                if (_dnveDataStore.SelfData == null) continue;
                if (_dnveDataStore.MergedHistogram == null) continue;
                var importantNodes= FindImportantNode();

                var count = Math.Min(OptConfig.Data.ExchangeCount, importantNodes.Count);
                for (var i = 0; i < count; i++)
                {
                    SendRequestNodeList(importantNodes[i].nodeId);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.ConnectionBalancerIntervalSeconds),
                    cancellationToken: token);

                SelectConnection();
            }
        }

        private void SelectConnection()
        {
            var allNodes = _dataStore.GetAllNodes().ToList();
            var selfPos = MistSyncManager.I.SelfSyncObject.transform.position;

            var directions = SphericalHistogramUtils.Directions;
            var selectedNodes = new List<Node>();

            foreach (var dir in directions)
            {
                Node closest = null;
                var minDist = float.MaxValue;

                foreach (var node in allNodes)
                {
                    var vec = node.Position.ToVector3() - selfPos;
                    var dot = Vector3.Dot(vec.normalized, dir);
                    if (dot < 0.7f) continue; // 同じ方向じゃない場合スキップ（閾値は調整可）

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

            // selectedNodesに含まれないノードを切断し、selectedNodesに含まれるノードに接続を試みる
            foreach (var node in selectedNodes)
            {
                var nodeId = node.Id;
                if (nodeId == PeerRepository.I.SelfId) continue;
                if (PeerRepository.I.IsConnectingOrConnected(nodeId)) continue;
                MistManager.I.Transport.Connect(nodeId);
            }

            // AOI対象ノードは切断しない
            var nodesToDisconnect = _routing.ConnectedNodes
                .Where(id => selectedNodes.All(n => n.Id != id) && !_routing.MessageNodes.Contains(id))
                .ToList();

            foreach (var nodeId in nodesToDisconnect)
            {
                if (nodeId == PeerRepository.I.SelfId) continue;

                if (!PeerRepository.I.IsConnectingOrConnected(nodeId)) continue;
                MistManager.I.Transport.Disconnect(nodeId);
            }
        }

        private void SendRequestNodeList(NodeId nodeId)
        {
            // 相手にNodeListを要求する
            var message = new DNVEMessage
            {
                Type = DNVEMessageType.RequestNodeList,
                Payload = string.Empty,
                Receiver = nodeId,
            };
            _sender.Send(message);
        }

        private void OnRequestNodeListReceived(DNVEMessage receiveMessage)
        {
            // NodeList要求を受信したら、自分のNodeListを送信する
            // TODO: 自動で_dataStoreの古いノードを削除したい
            var allNodes = _dataStore.GetAllNodes().ToHashSet();
            var connectedAllNodes = allNodes
                .Where(n => _routing.ConnectedNodes.Contains(n.Id))
                .ToList();
            var jsonPayload = JsonConvert.SerializeObject(connectedAllNodes);
            var message = new DNVEMessage
            {
                Type = DNVEMessageType.NodeList,
                Payload = jsonPayload,
                Receiver = receiveMessage.Sender,
            };
            _sender.Send(message);
        }

        private void OnNodeListReceived(DNVEMessage receiveMessage)
        {
            var nodes = JsonConvert.DeserializeObject<List<Node>>(receiveMessage.Payload);
            foreach (var node in nodes)
            {
                _routing.AddRouting(node.Id, receiveMessage.Sender);
                _dataStore.AddOrUpdate(node);
            }
        }

        private List<(NodeId nodeId, float score)> FindImportantNode()
        {
            var otherNodeHists = _dnveDataStore.NodeMaps;
            var selfHist = _dnveDataStore.SelfData.Hists;
            var selfCenter = _dnveDataStore.SelfData.Position;

            var importantNodes = new List<(NodeId nodeId, float score)>();

            foreach (var (nodeId, nodeData) in otherNodeHists)
            {
                var otherHist = nodeData.Hists;
                var otherCenter = nodeData.Position;

                // 自分の中心に射影
                var projected = SphericalHistogramUtils.ProjectSphericalHistogram(
                    otherHist, otherCenter.ToVector3(), selfCenter.ToVector3()
                );

                var score = 0f;
                var distBins = SphericalHistogramUtils.DistBins;

                // 近距離ほど重くする
                for (var j = 0; j < distBins; j++)
                {
                    var weight = 1f / (1f + j); // distBin=0が最も重い

                    for (var i = 0; i < SphericalHistogramUtils.Directions.Length; i++)
                    {
                        // 他ノードの強度を評価
                        var diff = projected[i, j] - selfHist[i, j];
                        if (diff > 0f)
                            score += diff * weight;
                    }
                }

                if (score > 0f)
                    importantNodes.Add((nodeId, score));
            }

            // スコア順にソート
            importantNodes.Sort((a, b) => b.score.CompareTo(a.score));

            foreach (var (nodeId, score) in importantNodes)
                MistLogger.Debug($"[重要ノード] {nodeId} - 近距離強度スコア: {score:F3}");

            if (importantNodes.Count > 0)
                MistLogger.Debug($"最も近距離影響の強いノード: {importantNodes[0].nodeId}");

            return importantNodes;
        }
    }
}

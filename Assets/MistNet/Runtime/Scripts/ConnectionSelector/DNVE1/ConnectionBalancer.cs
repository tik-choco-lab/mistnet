using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MistNet.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class ConnectionBalancer : IDisposable
    {
        private readonly RoutingBase _routingBase;
        private readonly KademliaDataStore _dataStore;
        private readonly AreaTracker _areaTracker;
        private readonly CancellationTokenSource _cts = new();
        private readonly IDNVE1MessageSender _sender;
        public IReadOnlyDictionary<NodeId, Vector3> NodeLocations => _nodeLocations;
        private readonly Dictionary<NodeId, Vector3> _nodeLocations = new();
        private KademliaMessage _message;
        private readonly KademliaRoutingTable _routingTable;
        private readonly ILayer _layer;
        private readonly IPeerRepository _peerRepository;
        private readonly DNVE1 _dnve1;

        public ConnectionBalancer(DNVE1 dnve1)
        {
            _sender = dnve1.Sender;
            _dataStore = dnve1.DataStore;
            _areaTracker = dnve1.AreaTracker;
            _routingBase = dnve1.RoutingBase;
            _routingTable = dnve1.RoutingTable;
            _layer = dnve1.Layer;
            _peerRepository = dnve1.PeerRepository;
            _dnve1 = dnve1;
            LoopBalanceConnections(_cts.Token).Forget();
            _sender.RegisterReceive(KademliaMessageType.Location, OnLocation);
        }

        private void OnLocation(KademliaMessage message)
        {
            MistLogger.Trace($"[ConnectionBalancer] OnLocation: {message.Sender.Id} {message.Payload}");
            _nodeLocations[message.Sender.Id] = JsonUtility.FromJson<Vector3>(message.Payload);
        }

        private async UniTask LoopBalanceConnections(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.ConnectionBalancerIntervalSeconds),
                    cancellationToken: token);

                SelectConnection();
                SelectDisconnection();
                SendLocation();
            }
        }

        private void SendLocation()
        {
            var connectedNodes = _routingBase.ConnectedNodes;
            if (connectedNodes.Count == 0) return;

            _message ??= new KademliaMessage
            {
                Type = KademliaMessageType.Location,
            };
            var position = MistSyncManager.I.SelfSyncObject.transform.position;
            _message.Payload = JsonUtility.ToJson(position);

            foreach (var nodeId in connectedNodes)
            {
                _sender?.Send(nodeId, _message);
            }
        }

        private void SelectConnection()
        {
            if (_routingBase.ConnectedNodes.Count >= OptConfig.Data.MaxConnectionCount) return;
            var requestCount = OptConfig.Data.MaxConnectionCount - _routingBase.ConnectedNodes.Count;

            if (requestCount <= 0) return;
            var i = 0;

            // dataStoreから接続候補を探す
            // 自身のいるChunkを優先的に
            var surroundingChunks = _areaTracker.SurroundingChunks;
            var selfChunk = AreaTracker.MyArea;
            var chunkId = IdUtil.ToBytes(selfChunk.ToString());
            if (_dataStore.TryGetValue(chunkId, out var data))
            {
                var areaInfo = JsonConvert.DeserializeObject<AreaInfo>(data);
                PruneExpiredNodes(chunkId, areaInfo);

                foreach (var nodeId in areaInfo.Nodes)
                {
                    if (_layer.Transport.IsConnectingOrConnected(nodeId)) continue;
                    _layer.Transport.Connect(nodeId);

                    i++;
                    if (i >= requestCount) return;
                    break;
                }
            }

            // 周囲のChunkからも接続候補を探す
            foreach (var area in surroundingChunks)
            {
                if (area.Equals(selfChunk)) continue; // 自分のChunkはスキップ
                var areaId = IdUtil.ToBytes(area.ToString());
                var closestNodes = _routingTable.FindClosestNodes(areaId);
                foreach (var node in closestNodes)
                {
                    if (_layer.Transport.IsConnectingOrConnected(node.Id)) continue;
                    _layer.Transport.Connect(node.Id);
                    i++;
                    if (i >= requestCount) return;
                    break;
                }

                if (!_dataStore.TryGetValue(areaId, out data)) continue;

                var areaInfo = JsonConvert.DeserializeObject<AreaInfo>(data);
                PruneExpiredNodes(areaId, areaInfo);
                foreach (var nodeId in areaInfo.Nodes)
                {
                    if (_layer.Transport.IsConnectingOrConnected(nodeId)) continue;
                    _layer.Transport.Connect(nodeId);

                    i++;
                    if (i >= requestCount) return;
                    break;
                }

                if (i >= requestCount) return;
            }
        }

        /// <summary>
        /// 期限切れノードをまとめて削除し、変更があれば保存する
        /// </summary>
        private void PruneExpiredNodes(byte[] chunkId, AreaInfo areaInfo)
        {
            var expiredNodes = areaInfo.ExpireAt
                .Where(kvp => kvp.Value < DateTime.UtcNow)
                .Select(kvp => kvp.Key)
                .ToList();

            if (expiredNodes.Count == 0) return;

            foreach (var nodeId in expiredNodes)
            {
                areaInfo.Nodes.Remove(nodeId);
                areaInfo.ExpireAt.Remove(nodeId);
                MistLogger.Debug($"[ConnectionBalancer] Removed expired node {nodeId} from AreaInfo");
            }

            // まとめて保存
            var json = JsonConvert.SerializeObject(areaInfo);
            _dataStore.Store(chunkId, json);
        }

        private void SelectDisconnection()
        {
            // 表示しているNodeと情報交換Node以外の中から切断していく
            if (_routingBase.ConnectedNodes.Count <= OptConfig.Data.MaxConnectionCount) return;

            var requestCount = _routingBase.ConnectedNodes.Count - OptConfig.Data.MaxConnectionCount;
            requestCount += 5; // 少し多めに切断しておく 探索のための枠を確保するため
            var connectedNodes = _routingBase.ConnectedNodes;

            // 最後に通信した時間が最も遅い順にソート
            var sortedNodes = connectedNodes
                .OrderBy(n => _dnve1.LastMessageTimes.TryGetValue(n, out var time) ? time : DateTime.MinValue)
                .Where(n => !_routingBase.MessageNodes.Contains(n))
                .ToList();

            var disconnectedCount = 0;
            for (var i = 0; i < sortedNodes.Count && disconnectedCount < requestCount; i++)
            {
                var nodeId = sortedNodes[i];
                _layer.Transport.Disconnect(nodeId);
                disconnectedCount++;
            }

            // var connectedNodes = _routingBase.ConnectedNodes;
            //
            // // 保護すべきノード（情報交換ノード + 表示ノード）
            // var exchangeNodes = _areaTracker.ExchangeNodes;
            // var visibleNodes = _routingBase.MessageNodes;
            // var safeConnectedNodes = connectedNodes
            //     .Where(n => exchangeNodes.Contains(n) || visibleNodes.Contains(n))
            //     .ToList();
            //
            // // 切断候補ノード（safe以外）
            // var candidateNodes = connectedNodes.Except(safeConnectedNodes).ToList();
            //
            // if (candidateNodes.Count < requestCount)
            // {
            //     // 切断候補が足りない場合はexchangeNodesから ExchangeNodeCount分を残して切断する
            //     // connectedNodes かつ exchangeNodes
            //     var connectedExchangeNodes = connectedNodes.Intersect(exchangeNodes).ToList();
            //     var targetCount = connectedExchangeNodes.Count - OptConfig.Data.ExchangeNodeCount;
            //     if (targetCount <= 0) return; // 切断するノードがない場合は終了
            //     var count = 0;
            //     // candidateに追加していく
            //     foreach (var nodeId in connectedExchangeNodes)
            //     {
            //         if (count >= targetCount) break;
            //         if (visibleNodes.Contains(nodeId)) continue;
            //
            //         candidateNodes.Add(nodeId);
            //         count++;
            //     }
            // }
            //
            // foreach (var nodeId in candidateNodes)
            // {
            //     _layer.Transport.Disconnect(nodeId);
            // }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}

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

        public ConnectionBalancer(DNVE1 dnve1)
        {
            _sender = dnve1.Sender;
            _dataStore = dnve1.DataStore;
            _areaTracker = dnve1.AreaTracker;
            _routingBase = dnve1.RoutingBase;
            _routingTable = dnve1.RoutingTable;
            _layer = dnve1.Layer;
            _peerRepository = dnve1.PeerRepository;
            LoopBalanceConnections(_cts.Token).Forget();
            _sender.RegisterReceive(KademliaMessageType.Location, OnLocation);
        }

        private void OnLocation(KademliaMessage message)
        {
            MistLogger.Info($"[ConnectionBalancer] OnLocation: {message.Sender.Id} {message.Payload}");
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
                foreach (var nodeId in areaInfo.Nodes.ToList())
                {
                    if (RemoveExpiredNode(areaInfo, nodeId)) continue;
                    if (_layer.Transport.IsConnectingOrConnected(nodeId)) continue;
                    if(!IdUtil.CompareId(_peerRepository.SelfId, nodeId)) continue;
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
                foreach (var nodeId in areaInfo.Nodes.ToList())
                {
                    if (RemoveExpiredNode(areaInfo, nodeId)) continue;

                    if (_layer.Transport.IsConnectingOrConnected(nodeId)) continue;
                    // if (MistManager.I.CompareId(nodeId))
                    {
                        _layer.Transport.Connect(nodeId);
                    }

                    i++;
                    if (i >= requestCount) return;
                    break;
                }

                if (i >= requestCount) return;
            }
        }

        private static bool RemoveExpiredNode(AreaInfo areaInfo, NodeId nodeId)
        {
            var time = areaInfo.ExpireAt[nodeId];
            if (time >= DateTime.UtcNow) return false;

            // 有効期限切れ
            areaInfo.Nodes.Remove(nodeId);
            areaInfo.ExpireAt.Remove(nodeId);

            MistLogger.Debug($"[ConnectionBalancer] Removed expired node {nodeId} from AreaInfo");
            return true;
        }

        private void SelectDisconnection()
        {
            // 表示しているNodeと情報交換Node以外の中から切断していく
            if (_routingBase.ConnectedNodes.Count <= OptConfig.Data.MaxConnectionCount) return;

            var requestCount = _routingBase.ConnectedNodes.Count - OptConfig.Data.MaxConnectionCount;

            var connectedNodes = _routingBase.ConnectedNodes;

            // 保護すべきノード（情報交換ノード + 表示ノード）
            var exchangeNodes = _areaTracker.ExchangeNodes;
            var visibleNodes = _routingBase.MessageNodes;
            var safeConnectedNodes = connectedNodes
                .Where(n => exchangeNodes.Contains(n) || visibleNodes.Contains(n))
                .ToList();

            // 切断候補ノード（safe以外）
            var candidateNodes = connectedNodes.Except(safeConnectedNodes).ToList();

            if (candidateNodes.Count < requestCount)
            {
                // 切断候補が足りない場合はexchangeNodesから ExchangeNodeCount分を残して切断する
                // connectedNodes かつ exchangeNodes
                var connectedExchangeNodes = connectedNodes.Intersect(exchangeNodes).ToList();
                var targetCount = connectedExchangeNodes.Count - OptConfig.Data.ExchangeNodeCount;
                if (targetCount <= 0) return; // 切断するノードがない場合は終了
                var count = 0;
                // candidateに追加していく
                foreach (var nodeId in connectedExchangeNodes)
                {
                    if (count >= targetCount) break;
                    candidateNodes.Add(nodeId);
                    count++;
                }
            }

            foreach (var nodeId in candidateNodes)
            {
                _layer.Transport.Disconnect(nodeId);
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}

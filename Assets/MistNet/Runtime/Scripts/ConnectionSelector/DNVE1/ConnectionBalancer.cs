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
        private readonly DNVE1 _dnve1;

        public ConnectionBalancer(DNVE1 dnve1)
        {
            _sender = dnve1.Sender;
            _dataStore = dnve1.DataStore;
            _areaTracker = dnve1.AreaTracker;
            _routingBase = dnve1.RoutingBase;
            _routingTable = dnve1.RoutingTable;
            _layer = dnve1.Layer;
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
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.ConnectionBalancerIntervalSeconds), cancellationToken: token);
                SelectConnection();
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.ConnectionBalancerIntervalSeconds), cancellationToken: token);
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

        // 接続候補リスト
        private readonly HashSet<NodeId> _candidateNodes = new();

        private void SelectConnection()
        {
            if (_routingBase.ConnectedNodes.Count >= OptConfig.Data.MaxConnectionCount - OptConfig.Data.SafeMargin) return;
            var requestCount = OptConfig.Data.Alpha;
            if (requestCount <= 0) return;
            // var i = 0;

            _candidateNodes.Clear();

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
                    _candidateNodes.Add(nodeId);
                }
            }

            // 周囲のChunkからも接続候補を探す
            foreach (var area in surroundingChunks)
            {
                if (area.Equals(selfChunk)) continue; // 自分のChunkはスキップ
                var areaId = IdUtil.ToBytes(area.ToString());

                // AOI内のノード接続
                if (_dataStore.TryGetValue(areaId, out data))
                {
                    var areaInfo = JsonConvert.DeserializeObject<AreaInfo>(data);
                    PruneExpiredNodes(areaId, areaInfo);
                    foreach (var nodeId in areaInfo.Nodes)
                    {
                        if (_layer.Transport.IsConnectingOrConnected(nodeId)) continue;
                        _candidateNodes.Add(nodeId);
                    }
                }

                // 情報交換リストを取得するためのノード接続
                var closestNodes = _routingTable.FindClosestNodes(areaId);
                foreach (var node in closestNodes)
                {
                    if (_layer.Transport.IsConnectingOrConnected(node.Id)) continue;
                    _candidateNodes.Add(node.Id);
                }
            }

            // 実際に接続を試みる
            // ランダムに並び変え
            var randomizedNodes = _candidateNodes.OrderBy(_ => UnityEngine.Random.value).ToList();
            for (var i = 0; i < requestCount; i++)
            {
                if (_candidateNodes.Count == 0) break;
                var nodeId = randomizedNodes[i];
                _candidateNodes.Remove(nodeId);
                _layer.Transport.Connect(nodeId);
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
            if (_routingBase.ConnectedNodes.Count < OptConfig.Data.MaxConnectionCount) return;

            var requestCount = _routingBase.ConnectedNodes.Count - OptConfig.Data.MaxConnectionCount;
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
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}

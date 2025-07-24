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
        private readonly IRouting _routing;
        private readonly KademliaDataStore _dataStore;
        private readonly AreaTracker _areaTracker;
        private readonly CancellationTokenSource _cts = new();
        private readonly Action<NodeId, KademliaMessage> _send;
        public IReadOnlyDictionary<NodeId, Vector3> NodeLocations => _nodeLocations;
        private readonly Dictionary<NodeId, Vector3> _nodeLocations = new();
        private KademliaMessage _message;
        private readonly KademliaRoutingTable _routingTable;

        public ConnectionBalancer(Action<NodeId, KademliaMessage> send, KademliaDataStore dataStore,
            KademliaRoutingTable routingTable, AreaTracker areaTracker)
        {
            _send = send;
            _dataStore = dataStore;
            _areaTracker = areaTracker;
            _routing = MistManager.I.routing;
            _routingTable = routingTable;
            LoopBalanceConnections(_cts.Token).Forget();
        }

        public void OnMessage(KademliaMessage message)
        {
            if (message.Type != KademliaMessageType.Location) return;
            _nodeLocations[message.Sender.Id] = JsonConvert.DeserializeObject<Vector3>(message.Payload);
        }

        private async UniTask LoopBalanceConnections(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfigLoader.Data.ConnectionBalancerIntervalSeconds),
                    cancellationToken: token);

                SelectConnection();
                SelectDisconnection();
                SendLocation();
            }
        }

        private void SendLocation()
        {
            var connectedNodes = _routing.ConnectedNodes;
            if (connectedNodes.Count == 0) return;
            _message ??= new KademliaMessage
            {
                Type = KademliaMessageType.Location,
                Sender = _routingTable.SelfNode
            };
            var position = MistSyncManager.I.SelfSyncObject.transform.position;
            _message.Payload = JsonConvert.SerializeObject(position);

            foreach (var nodeId in connectedNodes)
            {
                _send?.Invoke(nodeId, _message);
            }
        }

        private void SelectConnection()
        {
            if (_routing.ConnectedNodes.Count >= OptConfigLoader.Data.MaxConnectionCount) return;
            var requestCount = OptConfigLoader.Data.MaxConnectionCount - _routing.ConnectedNodes.Count;

            if (requestCount <= 0) return;
            var i = 0;

            // dataStoreから接続候補を探す
            // 自身のいるChunkを優先的に
            var surroundingChunks = _areaTracker.SurroundingChunks;
            var selfChunk = _areaTracker.MyArea;
            var chunkId = IdUtil.ToBytes(selfChunk.ToString());
            if (_dataStore.TryGetValue(chunkId, out var data))
            {
                var areaInfo = JsonConvert.DeserializeObject<AreaInfo>(data);
                foreach (var node in areaInfo.Nodes)
                {
                    MistManager.I.Connect(node.Id);
                    i++;
                    if (i >= requestCount) return;
                }
            }

            // 周囲のChunkからも接続候補を探す
            foreach (var area in surroundingChunks)
            {
                if (area.ToString() == selfChunk.ToString()) continue; // 自分のChunkはスキップ
                var areaId = IdUtil.ToBytes(area.ToString());
                if (!_dataStore.TryGetValue(areaId, out data)) continue;

                var areaInfo = JsonConvert.DeserializeObject<AreaInfo>(data);
                foreach (var node in areaInfo.Nodes)
                {
                    if (PeerRepository.I.IsConnectingOrConnected(node.Id)) continue;
                    if (MistManager.I.CompareId(node.Id))
                    {
                        MistManager.I.Connect(node.Id);
                    }

                    i++;
                    if (i >= requestCount) return;
                }

                if (i >= requestCount) return;
            }
        }

        private void SelectDisconnection()
        {
            // 接続数が最大値を超えているかつ、エリア外のノードがある場合に切断を行う
            if (_routing.ConnectedNodes.Count <= OptConfigLoader.Data.MaxConnectionCount) return;

            var requestCount = _routing.ConnectedNodes.Count - OptConfigLoader.Data.MaxConnectionCount;
            var i = 0;

            var connectedNodes = _routing.ConnectedNodes;
            foreach (var nodeId in connectedNodes)
            {
                if (!_nodeLocations.TryGetValue(nodeId, out var position)) continue;
                var area = new Area(position);

                if (_areaTracker.SurroundingChunks.Contains(area)) continue;
                // エリア外のノードを切断
                if (!PeerRepository.I.IsConnectingOrConnected(nodeId)) continue;
                if (MistManager.I.CompareId(nodeId))
                {
                    MistManager.I.Disconnect(nodeId);
                }

                i++;
                if (i >= requestCount) return;
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}

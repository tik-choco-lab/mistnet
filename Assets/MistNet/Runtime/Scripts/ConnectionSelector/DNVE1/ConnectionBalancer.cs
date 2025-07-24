using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MistNet.Utils;
using Newtonsoft.Json;

namespace MistNet
{
    public class ConnectionBalancer : IDisposable
    {
        private readonly IRouting _routing;
        private readonly KademliaDataStore _dataStore;
        private readonly AreaTracker _areaTracker;
        private readonly CancellationTokenSource _cts = new();

        public ConnectionBalancer(KademliaDataStore dataStore, AreaTracker areaTracker)
        {
            _dataStore = dataStore;
            _areaTracker = areaTracker;
            _routing = MistManager.I.routing;
            LoopBalanceConnections(_cts.Token).Forget();
        }

        private async UniTask LoopBalanceConnections(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfigLoader.Data.ConnectionBalancerIntervalSeconds),
                    cancellationToken: token);

                SelectConnection();
                SelectDisconnection();
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
                var nodeObj = MistSyncManager.I.GetSyncObject(nodeId);
                if (nodeObj == null) continue; // ノードが存在しない場合はスキップ

                var position = nodeObj.transform.position;
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

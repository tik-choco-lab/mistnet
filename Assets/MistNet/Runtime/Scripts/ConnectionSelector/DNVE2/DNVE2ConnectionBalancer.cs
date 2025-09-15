using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MistNet.DNVE2
{
    public class DNVE2ConnectionBalancer : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly IDNVE2NodeListStore _dataStore;
        private NodeId _selfId;
        private readonly RoutingBase _routing;

        public DNVE2ConnectionBalancer(IDNVE2MessageSender messageSender, IDNVE2NodeListStore dataStore)
        {
            _dataStore  = dataStore;
            _routing = MistManager.I.Routing;
            LoopBalanceConnections(_cts.Token).Forget();
        }

        private async UniTask LoopBalanceConnections(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.ConnectionBalancerIntervalSeconds),
                    cancellationToken: token);

                var allNodes = _dataStore.GetAllNodes().ToHashSet();
                _selfId ??= PeerRepository.I.SelfId;
                if (_selfId == null) return;
                if (!_dataStore.TryGet(_selfId, out var selfNode)) return;

                var closestNodes = DNVE2Util.GetNodeList(allNodes, selfNode, OptConfig.Data.MaxConnectionCount).ToHashSet();
                SelectConnection(closestNodes);
                SelectDisconnection(closestNodes);
            }
        }

        private void SelectDisconnection(IEnumerable<Node> closestNodes)
        {
            if (_routing.ConnectedNodes.Count <= OptConfig.Data.MaxConnectionCount) return;
            var nodesToDisconnect = _routing.ConnectedNodes
                .Where(id => closestNodes.All(n => n.Id != id))
                .Take(_routing.ConnectedNodes.Count - OptConfig.Data.MaxConnectionCount)
                .ToList();

            foreach (var nodeId in nodesToDisconnect)
            {
                if (nodeId == _selfId) continue;
                if (PeerRepository.I.IsConnectingOrConnected(nodeId)) continue;
                // if (MistManager.I.CompareId(nodeId)) continue;
                MistManager.I.Disconnect(nodeId);
            }
        }

        private void SelectConnection(IEnumerable<Node> closestNodes)
        {
            foreach (var node in closestNodes)
            {
                if (node.Id == _selfId) continue;
                if (PeerRepository.I.IsConnectingOrConnected(node.Id)) continue;
                // if (MistManager.I.CompareId(node.Id)) continue;
                MistManager.I.Connect(node.Id);
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts?.Dispose();
        }
    }
}

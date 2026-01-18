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
        private readonly INodeListStore _dataStore;
        private NodeId _selfId;
        private readonly RoutingBase _routing;
        private readonly IPeerRepository _peerRepository;
        private readonly ILayer _layer;

        public DNVE2ConnectionBalancer(INodeListStore dataStore, RoutingBase routing, IPeerRepository peerRepository, ILayer layer)
        {
            _dataStore  = dataStore;
            _routing = routing;
            _peerRepository = peerRepository;
            _layer = layer;
            LoopBalanceConnections(_cts.Token).Forget();
        }

        private async UniTask LoopBalanceConnections(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.ConnectionBalancerIntervalSeconds),
                    cancellationToken: token);

                var allNodes = _dataStore.GetAllNodes().ToHashSet();
                _selfId ??= _peerRepository.SelfId;
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
                if (_layer.Transport.IsConnectingOrConnected(nodeId)) continue;
                _layer.Transport.Disconnect(nodeId);
            }
        }

        private void SelectConnection(IEnumerable<Node> closestNodes)
        {
            foreach (var node in closestNodes)
            {
                if (node.Id == _selfId) continue;
                if (_layer.Transport.IsConnectingOrConnected(node.Id)) continue;
                _layer.Transport.Connect(node.Id);
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts?.Dispose();
        }
    }
}

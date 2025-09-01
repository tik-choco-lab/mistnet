using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MistNet
{
    public class VisibleNodesController : IDisposable
    {
        private readonly ConnectionBalancer _connectionBalancer;
        private readonly IRouting _routing;
        private readonly CancellationTokenSource _cts = new();
        // 表示中のNode List

        public VisibleNodesController(ConnectionBalancer connectionBalancer)
        {
            _connectionBalancer = connectionBalancer;
            _routing = MistManager.I.routing;
            LoopVisibleNodes(_cts.Token).Forget();
        }

        private async UniTask LoopVisibleNodes(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfigLoader.Data.VisibleNodesIntervalSeconds), cancellationToken: token);
                UpdateVisibleNodes();
            }
        }

        private void UpdateVisibleNodes()
        {
            var nodes = _connectionBalancer.NodeLocations;
            if (nodes.Count == 0) return;

            // 表示すべきNode一覧
            var visibleTargetNodes = nodes
                .OrderBy(kvp => Vector3.Distance(MistSyncManager.I.SelfSyncObject.transform.position, kvp.Value))
                .Take(OptConfigLoader.Data.VisibleCount)
                .Select(kvp => kvp.Key)
                .ToHashSet();

            var addVisibleNodes = visibleTargetNodes.Except(_routing.MessageNodes);
            var removeVisibleNodes = _routing.MessageNodes.Except(visibleTargetNodes);

            RequestObject(addVisibleNodes);
            RemoveObject(removeVisibleNodes);
        }

        private void RequestObject(IEnumerable<NodeId> nodeIds)
        {
            foreach (var nodeId in nodeIds)
            {
                if (!_routing.ConnectedNodes.Contains(nodeId)) continue; // 既に表示中のNodeはスキップ
                MistSyncManager.I.RequestObjectInstantiateInfo(nodeId);
                _routing.AddMessageNode(nodeId);
            }
        }

        private void RemoveObject(IEnumerable<NodeId> nodeIds)
        {
            var nodeIdsList = nodeIds.ToList();
            foreach (var nodeId in nodeIdsList)
            {
                MistSyncManager.I.RemoveObject(nodeId);
                _routing.RemoveMessageNode(nodeId);
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}

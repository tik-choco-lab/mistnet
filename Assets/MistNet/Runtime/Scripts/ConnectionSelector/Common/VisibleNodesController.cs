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
        private readonly INodeListStore _dataStore;
        private readonly RoutingBase _routingBase;
        private readonly CancellationTokenSource _cts = new();

        public VisibleNodesController(INodeListStore dataStore)
        {
            _dataStore = dataStore;
            _routingBase = MistManager.I.Routing;
            LoopVisibleNodes(_cts.Token).Forget();
        }

        private async UniTask LoopVisibleNodes(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.VisibleNodesIntervalSeconds),
                    cancellationToken: token);
                UpdateVisibleNodes();
            }
        }

        private void UpdateVisibleNodes()
        {
            var nodeIds = _routingBase.ConnectedNodes;

            if (nodeIds.Count == 0) return;

            var nodes = new List<Node>();
            foreach (var nodeId in nodeIds)
            {
                if (!_dataStore.TryGet(nodeId, out var node)) continue;
                nodes.Add(node);
            }

            // 表示すべきNode一覧
            var visibleTargetNodes = nodes
                .OrderBy(kvp =>
                    Vector3.Distance(MistSyncManager.I.SelfSyncObject.transform.position, kvp.Position.ToVector3()))
                .Take(OptConfig.Data.VisibleCount)
                .Select(kvp => kvp.Id)
                .ToHashSet();

            var addVisibleNodes = visibleTargetNodes.Except(_routingBase.MessageNodes);
            var removeVisibleNodes = _routingBase.MessageNodes.Except(visibleTargetNodes);

            RequestObject(addVisibleNodes);
            RemoveObject(removeVisibleNodes);
        }

        private void RequestObject(IEnumerable<NodeId> nodeIds)
        {
            foreach (var nodeId in nodeIds)
            {
                if (!_routingBase.ConnectedNodes.Contains(nodeId)) continue; // 既に表示中のNodeはスキップ
                MistSyncManager.I.RequestObjectInstantiateInfo(nodeId);
                _routingBase.AddMessageNode(nodeId);
            }
        }

        private void RemoveObject(IEnumerable<NodeId> nodeIds)
        {
            var nodeIdsList = nodeIds.ToList();
            foreach (var nodeId in nodeIdsList)
            {
                MistSyncManager.I.RemoveObject(nodeId);
                _routingBase.RemoveMessageNode(nodeId);
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MistNet
{
    public class DNVE1VisibleNodesController : IDisposable
    {
        private readonly ConnectionBalancer _connectionBalancer;
        private readonly RoutingBase _routingBase;
        private readonly CancellationTokenSource _cts = new();
        // 表示中のNode List

        public DNVE1VisibleNodesController(DNVE1 dnve1)
        {
            _connectionBalancer = dnve1.ConnectionBalancer;
            _routingBase = dnve1.RoutingBase;
            LoopVisibleNodes(_cts.Token).Forget();
        }

        private async UniTask LoopVisibleNodes(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.VisibleNodesIntervalSeconds), cancellationToken: token);
                UpdateVisibleNodes();
            }
        }

        private void UpdateVisibleNodes()
        {
            var nodes = _connectionBalancer.NodeLocations;
            if (nodes.Count == 0) return;

            // 表示すべきNode一覧
            var selfPos = MistSyncManager.I.SelfSyncObject.transform.position;

            var visibleTargetNodes = nodes
                .Select(kvp => new { kvp.Key, Distance = Vector3.Distance(selfPos, kvp.Value) })
                .Where(x => x.Distance <= OptConfig.Data.AoiRange)
                .OrderBy(x => x.Distance)
                .Take(OptConfig.Data.VisibleCount)
                .Select(x => x.Key)
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

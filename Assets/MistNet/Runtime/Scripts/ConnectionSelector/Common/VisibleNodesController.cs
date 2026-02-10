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

        private struct NodeDistance
        {
            public NodeId Id;
            public float Distance;
        }

        private readonly List<Node> _allNodesBuffer = new();
        private readonly List<NodeDistance> _nodeDistancesBuffer = new();
        private readonly HashSet<NodeId> _visibleTargetNodes = new();
        private readonly List<NodeId> _tempNodeIds = new();

        public VisibleNodesController(INodeListStore dataStore, RoutingBase routingBase)
        {
            _dataStore = dataStore;
            _routingBase = routingBase;
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
            _allNodesBuffer.Clear();
            foreach (var node in _dataStore.GetAllNodes())
            {
                _allNodesBuffer.Add(node);
            }

            if (_allNodesBuffer.Count == 0) return;

            var selfPos = MistSyncManager.I.SelfSyncObject.transform.position;

            _nodeDistancesBuffer.Clear();
            var aoiRange = OptConfig.Data.AoiRange;

            foreach (var node in _allNodesBuffer)
            {
                var dist = Vector3.Distance(selfPos, node.Position.ToVector3());
                if (dist <= aoiRange)
                {
                    _nodeDistancesBuffer.Add(new NodeDistance { Id = node.Id, Distance = dist });
                }
            }

            _nodeDistancesBuffer.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            _visibleTargetNodes.Clear();
            var count = Math.Min(_nodeDistancesBuffer.Count, OptConfig.Data.VisibleCount);
            for (int i = 0; i < count; i++)
            {
                _visibleTargetNodes.Add(_nodeDistancesBuffer[i].Id);
            }

            _tempNodeIds.Clear();
            foreach (var id in _visibleTargetNodes)
            {
                if (!_routingBase.MessageNodes.Contains(id))
                {
                    _tempNodeIds.Add(id);
                }
            }
            RequestObject(_tempNodeIds);

            _tempNodeIds.Clear();
            foreach (var id in _routingBase.MessageNodes)
            {
                if (!_visibleTargetNodes.Contains(id))
                {
                    _tempNodeIds.Add(id);
                }
            }
            RemoveObject(_tempNodeIds);
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
            foreach (var nodeId in nodeIds)
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

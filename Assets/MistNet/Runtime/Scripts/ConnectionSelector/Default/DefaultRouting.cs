using System.Collections.Generic;

namespace MistNet
{
    public class DefaultRouting : RoutingBase
    {
        private readonly Dictionary<NodeId, Dictionary<NodeId, int>> _routingTableList = new();

        public override void AddRouting(NodeId sourceId, NodeId fromId)
        {
            if (sourceId == PeerRepository.SelfId) return;
            if (sourceId == fromId) return;

            MistLogger.Trace($"[RoutingTable] Add {sourceId} from {fromId}");
            // _routingTable[sourceId] = fromId;
            InitIfNeeded(sourceId, fromId);

            _routingTableList[sourceId][fromId] += 1;

            // 他のものを-1する
            DecrementOthers(sourceId, fromId);
        }

        private void InitIfNeeded(NodeId sourceId, NodeId fromId)
        {
            if (!_routingTableList.ContainsKey(sourceId))
            {
                _routingTableList[sourceId] = new Dictionary<NodeId, int>();
            }

            if (!_routingTableList[sourceId].ContainsKey(fromId))
            {
                _routingTableList[sourceId][fromId] = 0;
            }
        }

        private void DecrementOthers(NodeId sourceId, NodeId fromId)
        {
            foreach (var nodeId in new List<NodeId>(_routingTableList[sourceId].Keys))
            {
                if (nodeId == fromId) continue;
                _routingTableList[sourceId][nodeId] -= 1;
                if (_routingTableList[sourceId][nodeId] <= 0)
                {
                    _routingTableList[sourceId].Remove(nodeId);
                }
            }
        }

        public override NodeId Get(NodeId targetId)
        {
            if (!_routingTableList.TryGetValue(targetId, out var fromDict))
            {
                MistLogger.Warning($"[RoutingTable] Not found {targetId}");
                return null;
            }

            NodeId bestNodeId = null;
            var bestCount = int.MinValue;
            foreach (var (nodeId, count) in fromDict)
            {
                if (count <= bestCount) continue;
                bestCount = count;
                bestNodeId = nodeId;
            }

            if (bestNodeId == null)
            {
                MistLogger.Warning($"[RoutingTable] Not found best route for {targetId}");
                return null;
            }

            MistLogger.Trace($"[RoutingTable] Get {targetId} -> {bestNodeId}");
            return bestNodeId;
        }
    }
}

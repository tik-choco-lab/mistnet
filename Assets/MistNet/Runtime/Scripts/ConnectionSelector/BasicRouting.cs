using System.Collections.Generic;
using System.Linq;
using MistNet.Utils;

namespace MistNet
{
    public class BasicRouting : IRouting
    {
        private readonly Dictionary<NodeId, NodeId> _routingTable = new();

        public override void AddRouting(NodeId sourceId, NodeId fromId)
        {
            if (sourceId == MistManager.I.PeerRepository.SelfId) return;
            if (sourceId == fromId) return;

            MistLogger.Log($"[RoutingTable] Add {sourceId} from {fromId}");
            if (_routingTable.TryAdd(sourceId, fromId))
            {
                return;
            }

            _routingTable[sourceId] = fromId;
        }

        public override NodeId Get(NodeId targetId)
        {
            MistLogger.Log($"[RoutingTable] Get {targetId}");
            if (_routingTable.TryGetValue(targetId, out var value))
            {
                return value;
            }

            MistLogger.LogWarning($"[RoutingTable] Not found {targetId}");

            // 適当に返す
            if (ConnectedNodes.Count != 0)
                return ConnectedNodes.First();

            MistLogger.LogWarning("[RoutingTable] Not found connected peer");
            return null;
        }
    }
}

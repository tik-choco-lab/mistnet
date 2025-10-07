using System.Linq;

namespace MistNet
{
    public class DefaultRouting : RoutingBase
    {
        public override void AddRouting(NodeId sourceId, NodeId fromId)
        {
            if (sourceId == MistManager.I.PeerRepository.SelfId) return;
            if (sourceId == fromId) return;

            if (_routingTable.TryAdd(sourceId, fromId))
            {
                return;
            }
            MistLogger.Debug($"[RoutingTable] Add {sourceId} from {fromId}");

            _routingTable[sourceId] = fromId;
        }

        public override NodeId Get(NodeId targetId)
        {
            MistLogger.Trace($"[RoutingTable] Get {targetId}");
            if (_routingTable.TryGetValue(targetId, out var value))
            {
                return value;
            }

            MistLogger.Warning($"[RoutingTable] Not found {targetId}");

            // 適当に返す
            if (ConnectedNodes.Count != 0)
                return ConnectedNodes.First();

            MistLogger.Warning("[RoutingTable] Not found connected peer");
            return null;
        }
    }
}

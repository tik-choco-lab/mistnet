using System.Collections.Generic;
using System.Linq;
using MistNet.Utils;

namespace MistNet
{
    public class BasicRouting : IRouting
    {
        private readonly Dictionary<NodeId, NodeId> _routingTable = new();

        public override void Add(NodeId sourceId, NodeId fromId)
        {
            if (sourceId == MistManager.I.PeerRepository.SelfId) return;
            if (sourceId == fromId) return;

            MistDebug.Log($"[RoutingTable] Add {sourceId} from {fromId}");
            if (_routingTable.TryAdd(sourceId, fromId))
            {
                return;
            }

            _routingTable[sourceId] = fromId;
        }

        public override NodeId Get(NodeId targetId)
        {
            MistDebug.Log($"[RoutingTable] Get {targetId}");
            if (_routingTable.TryGetValue(targetId, out var value))
            {
                return value;
            }

            MistDebug.LogWarning($"[RoutingTable] Not found {targetId}");

            // 適当に返す
            if (ConnectedNodes.Count != 0)
                return ConnectedNodes.First();

            MistDebug.LogWarning("[RoutingTable] Not found connected peer");
            return null;
        }
    }
}

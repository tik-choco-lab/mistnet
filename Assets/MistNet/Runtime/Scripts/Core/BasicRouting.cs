using System.Collections.Generic;

namespace MistNet
{
    public class BasicRouting: IRouting
    {
        private readonly Dictionary<string, string> _routingTable = new();
        public override void Add(string sourceId, string fromId)
        {
            if (sourceId == MistManager.I.MistPeerData.SelfId) return;
            if (sourceId == fromId) return;

            MistDebug.Log($"[RoutingTable] Add {sourceId} from {fromId}");
            if (_routingTable.TryAdd(sourceId, fromId))
            {
                return;
            }

            _routingTable[sourceId] = fromId;
        }

        public override string Get(string targetId)
        {
            MistDebug.Log($"[RoutingTable] Get {targetId}");
            if (_routingTable.TryGetValue(targetId, out var value))
            {
                return value;
            }

            MistDebug.LogWarning($"[RoutingTable] Not found {targetId}");

            // 適当に返す
            if (MistManager.I.MistPeerData.GetConnectedPeer.Count != 0)
                return MistManager.I.MistPeerData.GetConnectedPeer[0].Id;

            MistDebug.LogWarning("[RoutingTable] Not found connected peer");
            return null;
        }
    }
}

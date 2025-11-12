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

            // NOTE: 下記のように適当に返すと、メッセージがループする可能性がある 扱いに注意 やるならhopCountを必ずつける
            // if (ConnectedNodes.Count != 0)
            // {
            //     // randomで返す
            //     var index = Random.Range(0, ConnectedNodes.Count);
            //     return ConnectedNodes.ElementAt(index);
            // }

            MistLogger.Warning("[RoutingTable] Not found connected peer");
            return null;
        }
    }
}

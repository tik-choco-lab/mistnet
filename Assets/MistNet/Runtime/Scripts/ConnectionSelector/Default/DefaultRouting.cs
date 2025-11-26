using System.Collections.Generic;

namespace MistNet
{
    public class DefaultRouting : RoutingBase
    {
        protected readonly Dictionary<NodeId, Dictionary<NodeId,int>> _routingTableList = new();

        public override void AddRouting(NodeId sourceId, NodeId fromId)
        {
            if (sourceId == PeerRepository.SelfId) return;
            if (sourceId == fromId) return;

            MistLogger.Trace($"[RoutingTable] Add {sourceId} from {fromId}");
            // _routingTable[sourceId] = fromId;
            if (!_routingTableList.ContainsKey(sourceId))
            {
                _routingTableList[sourceId] = new Dictionary<NodeId, int>();
            }
            if (!_routingTableList[sourceId].ContainsKey(fromId))
            {
                _routingTableList[sourceId][fromId] = 0;
            }
            _routingTableList[sourceId][fromId] += 1;
            // 他のものを-1する
            foreach (var key in new List<NodeId>(_routingTableList[sourceId].Keys))
            {
                if (key == fromId) continue;
                _routingTableList[sourceId][key] -= 1;
                if (_routingTableList[sourceId][key] <= 0)
                {
                    _routingTableList[sourceId].Remove(key);
                }
            }
        }

        public override NodeId Get(NodeId targetId)
        {
            if (_routingTableList.TryGetValue(targetId, out var fromDict))
            {
                NodeId bestNodeId = null;
                var bestCount = int.MinValue;
                foreach (var kvp in fromDict)
                {
                    if (kvp.Value <= bestCount) continue;
                    bestCount = kvp.Value;
                    bestNodeId = kvp.Key;
                }
                if (bestNodeId != null)
                {
                    MistLogger.Trace($"[RoutingTable] Get {targetId} -> {bestNodeId}");
                    return bestNodeId;
                }
            }


            // MistLogger.Trace($"[RoutingTable] Get {targetId}");
            // if (_routingTable.TryGetValue(targetId, out var value))
            // {
            //     return value;
            // }

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

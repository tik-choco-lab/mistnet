using System.Collections.Generic;
using MistNet.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class AreaTracker
    {
        private readonly Kademlia _kademlia;
        private readonly KademliaDataStore _dataStore;
        private readonly KademliaRoutingTable _routingTable;
        private readonly KademliaController _kademliaController;

        public AreaTracker(Kademlia kademlia, KademliaDataStore dataStore, KademliaRoutingTable routingTable, KademliaController kademliaController)
        {
            _kademlia = kademlia;
            _dataStore = dataStore;
            _routingTable = routingTable;
            _kademliaController = kademliaController;
        }

        private void FindMyAreaInfo(Vector3 position)
        {
            var chunk = new Area(position);
            var target = IdUtil.ToBytes(chunk.ToString());
            if (_dataStore.TryGetValue(target, out var _)) return;
            var closestNodes = _routingTable.FindClosestNodes(target);
            _kademliaController.FindValue(closestNodes, target);
        }

        private void StoreMyLocation(Vector3 position)
        {
            var chunk = new Area(position);
            var target = IdUtil.ToBytes(chunk.ToString());
            var closestNodes = _routingTable.FindClosestNodes(target);
            if (closestNodes.Count < KBucket.K)
            {
                UpdateArea(target, chunk, closestNodes);
            }
            else
            {
                _kademliaController.FindNode(closestNodes, target);
            }
        }

        private void UpdateArea(byte[] target, Area chunk, List<NodeInfo> closestNodes)
        {
            AreaInfo areaInfo;
            if (!_dataStore.TryGetValue(target, out var value))
            {
                areaInfo = new AreaInfo
                {
                    Chunk = chunk,
                };
            }
            else
            {
                areaInfo = JsonConvert.DeserializeObject<AreaInfo>(value);
            }

            areaInfo.Nodes.Add(_routingTable.SelfNode);
            _dataStore.Store(target, areaInfo.ToString());

            foreach (var node in closestNodes)
            {
                _kademlia.Store(node, target, areaInfo.ToString());
            }
        }

    }
}

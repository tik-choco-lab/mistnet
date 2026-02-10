using System;
using System.Collections.Generic;

namespace MistNet.DNVE3
{
    public class DNVE3DataStore
    {
        public SpatialDensityData SelfDensity;
        public float[,] MergedDensityMap;

        public readonly Dictionary<NodeId, NeighborDensityInfo> Neighbors = new();
        public readonly Dictionary<NodeId, DateTime> LastUpdateTimes = new();
        public class NeighborDensityInfo
        {
            public SpatialDensityData Data;
            public DateTime LastMessageTime;
        }

        public void AddOrUpdateNeighbor(NodeId id, SpatialDensityData data)
        {
            if (!Neighbors.TryGetValue(id, out var info))
            {
                info = new NeighborDensityInfo();
                Neighbors[id] = info;
            }

            var now = DateTime.UtcNow;
            info.Data = data;
            info.LastMessageTime = now;
            LastUpdateTimes[id] = now;
        }

        public void AddOrUpdateNodeUpdateTime(NodeId id)
        {
            LastUpdateTimes[id] = DateTime.UtcNow;
        }

        public void RemoveNeighbor(NodeId id)
        {
            Neighbors.Remove(id);
            LastUpdateTimes.Remove(id);
        }

        public void UpdateLastMessageTime(NodeId id)
        {
            var now = DateTime.UtcNow;
            LastUpdateTimes[id] = now;
            if (Neighbors.TryGetValue(id, out var info))
            {
                info.LastMessageTime = now;
            }
        }
    }
}

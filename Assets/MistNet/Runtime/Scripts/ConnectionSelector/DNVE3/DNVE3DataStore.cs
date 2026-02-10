using System;
using System.Collections.Generic;

namespace MistNet.DNVE3
{
    public class DNVE3DataStore
    {
        public SpatialDensityData SelfDensity;
        public float[,] MergedDensityMap;

        public readonly Dictionary<NodeId, NeighborDensityInfo> Neighbors = new();
        public class NeighborDensityInfo
        {
            public SpatialDensityData Data;     // 密度データ
            public DateTime LastMessageTime;    // 最終受信時刻
        }

        public void AddOrUpdateNeighbor(NodeId id, SpatialDensityData data)
        {
            if (!Neighbors.TryGetValue(id, out var info))
            {
                info = new NeighborDensityInfo();
                Neighbors[id] = info;
            }

            info.Data = data;
            info.LastMessageTime = DateTime.UtcNow;
        }

        public void RemoveNeighbor(NodeId id)
        {
            Neighbors.Remove(id);
        }

        public void UpdateLastMessageTime(NodeId id)
        {
            if (Neighbors.TryGetValue(id, out var info))
            {
                info.LastMessageTime = DateTime.UtcNow;
            }
        }
    }
}


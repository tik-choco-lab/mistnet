using System;
using System.Collections.Generic;

namespace MistNet.DNVE3
{
    public class DNVE3DataStore
    {
        // public readonly Dictionary<NodeId, SpatialDensityData> NodeMaps = new(); // TODO: dataStoreに入れるべきかも
        // public readonly Dictionary<NodeId, DateTime> ExpireNodeTimes = new();
        // public readonly Dictionary<NodeId, DateTime> LastMessageTimes = new();
        public SpatialDensityData SelfDensity;
        public float[,] MergedDensityMap;

        public readonly Dictionary<NodeId, NeighborDensityInfo> Neighbors = new();
        public class NeighborDensityInfo
        {
            public SpatialDensityData Data;     // 密度データ
            public DateTime ExpireTime;         // 有効期限
            public DateTime LastMessageTime;    // 最終受信時刻
        }

        public void AddOrUpdateNeighbor(NodeId id, SpatialDensityData data, DateTime expireTime)
        {
            if (!Neighbors.TryGetValue(id, out var info))
            {
                info = new NeighborDensityInfo();
                Neighbors[id] = info;
            }

            info.Data = data;
            info.ExpireTime = expireTime;
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

        public void UpdateExpireTime(NodeId id, DateTime expireTime)
        {
            if (Neighbors.TryGetValue(id, out var info))
            {
                info.ExpireTime = expireTime;
            }
        }
    }
}


using System;
using System.Collections.Generic;

namespace MistNet.DNVE3
{
    public class DNVE3DataStore
    {
        public readonly Dictionary<NodeId, SpatialHistogramData> NodeMaps = new(); // TODO: dataStoreに入れるべきかも
        public readonly Dictionary<NodeId, DateTime> ExpireNodeTimes = new();
        public readonly Dictionary<NodeId, DateTime> LastMessageTimes = new();
        public SpatialHistogramData SelfData;
        public float[,] MergedHistogram;
    }
}

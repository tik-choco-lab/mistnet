using System;
using System.Collections.Generic;
using MistNet.Utils;

namespace MistNet.DNVE3
{
    public class DNVE3DataStore
    {
        public readonly Dictionary<NodeId, SpatialHistogramData> NodeMaps = new();
        public readonly Dictionary<NodeId, DateTime> ExpireNodeTimes = new();
        public readonly Dictionary<NodeId, DateTime> LastMessageTimes = new();
        public SpatialHistogramData SelfData;
        public float[,] MergedHistogram;
        public float[,] LocalDensityMap;
        public float[,] ConfidenceMap;

        public DNVE3DataStore()
        {
            var directions = SphericalHistogramUtils.Directions.Length;
            var distBins = SphericalHistogramUtils.DistBins;
            MergedHistogram = new float[directions, distBins];
            LocalDensityMap = new float[directions, distBins];
            ConfidenceMap = new float[directions, distBins];
        }
    }
}

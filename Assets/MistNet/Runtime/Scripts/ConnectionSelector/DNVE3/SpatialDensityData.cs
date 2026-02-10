using MemoryPack;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet.DNVE3
{
    [MemoryPackable]
    public partial class SpatialDensityData
    {
        [MemoryPackOrder(0)] [JsonProperty("densityMap")] public float[,] DensityMap;
        [MemoryPackOrder(1)] [JsonProperty("position")] public Position Position;
    }

    [MemoryPackable]
    public partial class SpatialDensityDataByte
    {
        [MemoryPackOrder(0)] public Vector3 Position { get; set; }
        [MemoryPackOrder(1)] public float MaxValue { get; set; }
        [MemoryPackOrder(2)] public byte[] ByteDensities { get; set; }
    }
}

using MemoryPack;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet.DNVE3
{
    [MemoryPackable]
    public partial class SpatialHistogramData
    {
        [JsonProperty("hists")] public float[,] Hists;
        [JsonProperty("position")] public Position Position;
    }

    [MemoryPackable]
    public partial class SpatialHistogramDataByte
    {
        public Vector3 Position { get; set; }
        public byte[] ByteHists { get; set; }
    }
}

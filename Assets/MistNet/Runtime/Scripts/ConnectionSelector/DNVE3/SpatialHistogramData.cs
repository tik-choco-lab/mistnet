using Newtonsoft.Json;
using UnityEngine;

namespace MistNet.DNVE3
{
    public class SpatialHistogramData
    {
        [JsonProperty("hists")] public float[,] Hists;
        [JsonProperty("position")] public Vector3 Position;
    }
}

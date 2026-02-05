using Newtonsoft.Json;

namespace MistNet.DNVE3
{
    public class SpatialHistogramData
    {
        [JsonProperty("hists")] public float[,] Hists;
        [JsonProperty("position")] public Position Position;
    }
}

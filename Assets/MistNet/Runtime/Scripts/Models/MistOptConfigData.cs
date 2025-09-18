using Newtonsoft.Json;

namespace MistNet
{
    public class MistOptConfigData
    {
        [JsonProperty("visibleCount")] public int VisibleCount = 5;
        [JsonProperty("maxConnectionCount")] public int MaxConnectionCount = 20;

        // DNVE1
        [JsonProperty("safeMargin")] public int SafeMargin = 3;
        [JsonProperty("chunkLoadSize")] public int ChunkLoadSize = 2;
        [JsonProperty("areaTrackerIntervalSeconds")] public float AreaTrackerIntervalSeconds = 5f;
        [JsonProperty("connectionBalancerIntervalSeconds")] public float ConnectionBalancerIntervalSeconds = 2f;
        [JsonProperty("visibleNodesIntervalSeconds")] public float VisibleNodesIntervalSeconds = 2f;

        // DNVE2
        [JsonProperty("nodeListExchangeIntervalSeconds")] public float NodeListExchangeIntervalSeconds = 3f;
        [JsonProperty("nodeListExchangeMaxCount")] public int NodeListExchangeMaxCount = 10;
    }
}

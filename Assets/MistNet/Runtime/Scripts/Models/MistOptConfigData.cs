using Newtonsoft.Json;

namespace MistNet
{
    public class MistOptConfigData
    {
        [JsonProperty("visibleCount")] public int VisibleCount = 5;
        [JsonProperty("maxConnectionCount")] public int MaxConnectionCount = 20;

        // DNVE1 ---------------
        [JsonProperty("exchangeNodeCount")] public int ExchangeNodeCount = 12; // 情報交換用のノード数
        [JsonProperty("safeMargin")] public int SafeMargin = 3;
        [JsonProperty("chunkLoadSize")] public int ChunkLoadSize = 1;
        [JsonProperty("areaTrackerIntervalSeconds")] public float AreaTrackerIntervalSeconds = 5f;
        [JsonProperty("connectionBalancerIntervalSeconds")] public float ConnectionBalancerIntervalSeconds = 2f;
        [JsonProperty("visibleNodesIntervalSeconds")] public float VisibleNodesIntervalSeconds = 2f;
        [JsonProperty("expireSeconds")] public float ExpireSeconds = 10f; // dataStoreの中身で自動削除するまでの秒数
        [JsonProperty("kademliaK")] public int KademliaK = 20; // KademliaのK値

        // DNVE2 ---------------
        [JsonProperty("nodeListExchangeIntervalSeconds")] public float NodeListExchangeIntervalSeconds = 3f;
        [JsonProperty("nodeListExchangeMaxCount")] public int NodeListExchangeMaxCount = 10;
    }
}

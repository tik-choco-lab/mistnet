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
        [JsonProperty("aoiRange")] public int AoiRange = 256; // m
        [JsonProperty("chunkSize")] public int ChunkSize = 256; // m
        [JsonProperty("areaTrackerIntervalSeconds")] public float AreaTrackerIntervalSeconds = 5f;
        [JsonProperty("connectionBalancerIntervalSeconds")] public float ConnectionBalancerIntervalSeconds = 2f; // DNVE3も使用
        [JsonProperty("visibleNodesIntervalSeconds")] public float VisibleNodesIntervalSeconds = 2f;
        [JsonProperty("expireSeconds")] public float ExpireSeconds = 10f; // dataStoreの中身で自動削除するまでの秒数 // DNVE3も使用
        [JsonProperty("kademliaK")] public int KademliaK = 20; // KademliaのK値
        [JsonProperty("hopCount")] public int HopCount = 5; // メッセージの最大ホップ数

        // DNVE2 ---------------
        [JsonProperty("nodeListExchangeIntervalSeconds")] public float NodeListExchangeIntervalSeconds = 3f;
        [JsonProperty("nodeListExchangeMaxCount")] public int NodeListExchangeMaxCount = 10;

        // DNVE3 ---------------
        [JsonProperty("heartbeatIntervalSeconds")] public float HeartbeatIntervalSeconds = 4f;
        [JsonProperty("exchangeCount")] public int ExchangeCount = 3; // 重要順ノード交換対象ノード数
    }
}

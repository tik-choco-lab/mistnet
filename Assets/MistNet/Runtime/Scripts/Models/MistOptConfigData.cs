using Newtonsoft.Json;

namespace MistNet
{
    public class MistOptConfigData
    {
        [JsonProperty("visibleCount")] public int VisibleCount = 5;
        [JsonProperty("maxConnectionCount")] public int MaxConnectionCount = 34;

        // DNVE1 ---------------
        [JsonProperty("exchangeNodeCount")] public int ExchangeNodeCount = 12; // 情報交換用のノード数
        [JsonProperty("safeMargin")] public int SafeMargin = 3;
        [JsonProperty("chunkLoadSize")] public int ChunkLoadSize = 1;
        [JsonProperty("chunkSize")] public int ChunkSize = 128; // m
        [JsonProperty("aoiRange")] public float AoiRange = 64f; // m
        [JsonProperty("areaTrackerIntervalSeconds")] public float AreaTrackerIntervalSeconds = 2f;
        [JsonProperty("connectionBalancerIntervalSeconds")] public float ConnectionBalancerIntervalSeconds = 2f; // DNVE3も使用
        [JsonProperty("visibleNodesIntervalSeconds")] public float VisibleNodesIntervalSeconds = 2f;
        [JsonProperty("expireSeconds")] public float ExpireSeconds = 4f; // dataStoreの中身で自動削除するまでの秒数 // DNVE3も使用
        [JsonProperty("kademliaK")] public int KademliaK = 20; // KademliaのK値
        [JsonProperty("hopCount")] public int HopCount = 3; // メッセージの最大ホップ数
        [JsonProperty("alpha")] public int Alpha = 3; // 並列要求数

        // DNVE2 ---------------
        [JsonProperty("nodeListExchangeIntervalSeconds")] public float NodeListExchangeIntervalSeconds = 3f;
        [JsonProperty("nodeListExchangeMaxCount")] public int NodeListExchangeMaxCount = 10;

        // DNVE3 ---------------
        [JsonProperty("heartbeatIntervalSeconds")] public float HeartbeatIntervalSeconds = 4f;
        [JsonProperty("exchangeCount")] public int ExchangeCount = 3; // 重要順ノード交換対象ノード数
    }
}

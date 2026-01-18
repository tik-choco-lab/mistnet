using Newtonsoft.Json;

namespace MistNet
{
    public class MistOptConfigData
    {
        [JsonProperty("visibleCount")] public int VisibleCount = 5;
        [JsonProperty("maxConnectionCount")] public int MaxConnectionCount = 30;
        [JsonProperty("connectionBalancerIntervalSeconds")] public float ConnectionBalancerIntervalSeconds = 1f; // DNVE1,3で使用
        [JsonProperty("visibleNodesIntervalSeconds")] public float VisibleNodesIntervalSeconds = 1f; // DNVE1,3で使用
        [JsonProperty("expireSeconds")] public float ExpireSeconds = 4f; // dataStoreの中身で自動削除するまでの秒数 // DNVE1,3で使用
        [JsonProperty("aoiRange")] public float AoiRange = 64f; // m // DNVE1,3で使用

        // DNVE1 ---------------
        [JsonProperty("chunkLoadSize")] public int ChunkLoadSize = 1;
        [JsonProperty("chunkSize")] public int ChunkSize = 128; // m
        [JsonProperty("areaTrackerIntervalSeconds")] public float AreaTrackerIntervalSeconds = 1f;
        [JsonProperty("kademliaK")] public int KademliaK = 20; // KademliaのK値
        [JsonProperty("hopCount")] public int HopCount = 3; // メッセージの最大ホップ数
        [JsonProperty("alpha")] public int Alpha = 3; // 並列要求数
        [JsonProperty("forceDisconnectCount")] public int ForceDisconnectCount = 3;

        // DNVE2 ---------------
        [JsonProperty("nodeListExchangeIntervalSeconds")] public float NodeListExchangeIntervalSeconds = 3f;
        [JsonProperty("nodeListExchangeMaxCount")] public int NodeListExchangeMaxCount = 10;

        // DNVE3 ---------------
        [JsonProperty("heartbeatIntervalSeconds")] public float HeartbeatIntervalSeconds = 1f;
        [JsonProperty("exchangeCount")] public int ExchangeCount = 3; // 重要順ノード交換対象ノード数
    }
}

using Newtonsoft.Json;

namespace MistNet
{
    public class MistOptConfig
    {
        [JsonProperty("visibleCount")] public int VisibleCount = 5;
        [JsonProperty("bucketBase")] public int BucketBase = 4;
        [JsonProperty("bucketMax")] public int BucketMax = 5;
        [JsonProperty("bucketIndexMax")] public int BucketIndexMax = 4;
        [JsonProperty("chunkLoadSize")] public int ChunkLoadSize = 2;
        [JsonProperty("connectionsPerBucket")] public int ConnectionsPerBucket = 2;
        [JsonProperty("sendInfoIntervalSecondMultiplier")] public int SendInfoIntervalSecondMultiplier = 3;
        [JsonProperty("pingTimeoutSeconds")] public int PingTimeoutSeconds = 5;
        [JsonProperty("requestObjectIntervalSeconds")] public int RequestObjectIntervalSeconds = 5;
        [JsonProperty("areaTrackerIntervalSeconds")] public float AreaTrackerIntervalSeconds = 5f;
        [JsonProperty("connectionBalancerIntervalSeconds")] public float ConnectionBalancerIntervalSeconds = 2f;
        [JsonProperty("visibleNodesIntervalSeconds")] public float VisibleNodesIntervalSeconds = 2f;
        [JsonProperty("maxConnectionCount")] public int MaxConnectionCount = 20;
    }
}

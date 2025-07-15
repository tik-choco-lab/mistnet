using Newtonsoft.Json;

namespace MistNet
{
    public class MistOptConfig
    {
        [JsonProperty("visibleCount")] public int VisibleCount = 5;
        [JsonProperty("bucketBase")] public int BucketBase = 4;
        [JsonProperty("bucketMax")] public int BucketMax = 5;
        [JsonProperty("sendInfoIntervalSecondMultiplier")] public int SendInfoIntervalSecondMultiplier = 3;
        [JsonProperty("pingTimeoutSeconds")] public int PingTimeoutSeconds = 5;
        [JsonProperty("requestObjectIntervalSeconds")] public int RequestObjectIntervalSeconds = 5;
    }
}

using Newtonsoft.Json;

namespace MistNet
{
    public class MistOptConfig
    {
        [JsonProperty("maxCount")] public int MaxCount = 10;
        [JsonProperty("visibleCount")] public int VisibleCount = 10;
        [JsonProperty("bucketBase")] public int BucketBase = 4;
        [JsonProperty("sendInfoIntervalSeconds")] public int SendInfoIntervalSeconds = 5;
        [JsonProperty("pingTimeoutSeconds")] public int PingTimeoutSeconds = 5;
        [JsonProperty("requestObjectIntervalSeconds")] public int RequestObjectIntervalSeconds = 5;
    }
}

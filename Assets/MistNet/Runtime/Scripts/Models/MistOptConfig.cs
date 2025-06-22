using Newtonsoft.Json;

namespace MistNet
{
    public class MistOptConfig
    {
        [JsonProperty("maxCount")] public int MaxCount = 10;
        [JsonProperty("visibleCount")] public int VisibleCount = 5;
        [JsonProperty("bucketBase")] public int BucketBase = 4;
        [JsonProperty("sendInfoIntervalSecondMultiplier")] public int SendInfoIntervalSecondMultiplier = 3;
        [JsonProperty("pingTimeoutSeconds")] public int PingTimeoutSeconds = 5;
        [JsonProperty("requestObjectIntervalSeconds")] public int RequestObjectIntervalSeconds = 5;
    }
}

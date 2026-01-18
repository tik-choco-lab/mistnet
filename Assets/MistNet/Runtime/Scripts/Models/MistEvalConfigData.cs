using Newtonsoft.Json;

namespace MistNet
{
    public class MistEvalConfigData
    {
        [JsonProperty("serverUrl")] public string ServerUrl = "ws://localhost:8081";
        [JsonProperty("eventLogUrl")] public string EventLogUrl = "http://localhost:9090";
        [JsonProperty("enableEventLog")] public bool EnableEventLog = true;
        [JsonProperty("maxAreaSize")] public int MaxAreaSize = 1000;
        [JsonProperty("maxMoveSpeed")] public float MaxMoveSpeed = 6.0f; // 1秒間の移動速度 m/s
        [JsonProperty("sendStateIntervalSeconds")] public float SendStateIntervalSeconds = 3.0f;
        [JsonProperty("nodeResetIntervalSeconds")] public float NodeResetIntervalSeconds = 0.75f;
    }
}

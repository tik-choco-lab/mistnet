using Newtonsoft.Json;

namespace MistNet
{
    public class MistEvalConfigData
    {
        [JsonProperty("serverUrl")] public string ServerUrl = "ws://localhost:8081";
        [JsonProperty("maxAreaSize")] public int MaxAreaSize = 1000;
        [JsonProperty("maxMoveSpeed")] public float MaxMoveSpeed = 1.0f;
    }
}

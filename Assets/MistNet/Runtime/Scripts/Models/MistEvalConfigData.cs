using Unity.Plastic.Newtonsoft.Json;

namespace MistNet
{
    public class MistEvalConfigData
    {
        [JsonProperty("serverUrl")] public string ServerUrl = "ws://localhost:8081";
    }
}

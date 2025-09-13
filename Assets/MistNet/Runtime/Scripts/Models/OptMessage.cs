using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MistNet
{
    public class OptMessage
    {
        [JsonProperty("type")] public OptMessageType Type;
        [JsonProperty("payload")] public object Payload;
    }

    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public enum OptMessageType
    {
        NodeState,
        RequestNodeList,
    }
}

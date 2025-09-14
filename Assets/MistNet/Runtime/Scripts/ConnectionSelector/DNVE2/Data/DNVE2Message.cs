using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MistNet.DNVE2
{
    public class DNVE2Message
    {
        [JsonProperty("sender")] public NodeInfo Sender;
        [JsonProperty("type")] public DNVE2MessageType Type;
        [JsonProperty("payload")] public string Payload;
    }

    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public enum DNVE2MessageType
    {
        NodeList,
    }
}

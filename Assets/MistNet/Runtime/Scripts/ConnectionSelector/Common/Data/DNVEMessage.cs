using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MistNet
{
    public class DNVEMessage
    {
        [JsonProperty("sender")] public NodeId Sender;
        [JsonProperty("receiver")] public NodeId Receiver;
        [JsonProperty("type")] public DNVEMessageType Type;
        [JsonProperty("payload")] public string Payload;
    }

    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public enum DNVEMessageType
    {
        // DNVE2
        NodeList,
        // DNVE3
        Heartbeat,
        NodeRequest,
    }
}

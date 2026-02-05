using MemoryPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MistNet
{
    [MemoryPackable]
    public partial class DNVEMessage
    {
        [MemoryPackOrder(0)] [JsonProperty("sender")] public NodeId Sender;
        [MemoryPackOrder(1)] [JsonProperty("receiver")] public NodeId Receiver;
        [MemoryPackOrder(2)] [JsonProperty("type")] public DNVEMessageType Type;
        [MemoryPackOrder(3)] [JsonProperty("payload")] public byte[] Payload;
    }

    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public enum DNVEMessageType
    {
        // DNVE2
        NodeList,
        // DNVE3
        Heartbeat,
        RequestNodeList,
    }
}

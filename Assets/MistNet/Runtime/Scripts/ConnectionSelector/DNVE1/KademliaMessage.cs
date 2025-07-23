using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MistNet
{
    public class KademliaMessage
    {
        [JsonProperty("sender")] public NodeInfo Sender;
        [JsonProperty("type")] public KademliaMessageType Type;
        [JsonProperty("payload")] public string Payload;
    }

    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public enum KademliaMessageType
    {
        Ping,
        Pong,
        Store,
        FindNode,
        FindValue,
        ResponseNode,
        ResponseValue,
    }

    public class ResponseFindNode
    {
        [JsonProperty("nodes")] public List<NodeInfo> Nodes { get; set; }
        [JsonProperty("target")] public byte[] Target { get; set; }
    }

    public class ResponseFindValue
    {
        [JsonProperty("nodes")] public List<NodeInfo> Nodes { get; set; }
        [JsonProperty("value")] public string Value { get; set; }
        [JsonProperty("target")] public byte[] Target { get; set; }
    }
}

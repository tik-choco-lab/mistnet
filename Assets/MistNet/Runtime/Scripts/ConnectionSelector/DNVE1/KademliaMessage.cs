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
        // 拡張
        Location,
        Gossip,
    }

    public class ResponseFindNode
    {
        [JsonProperty("key")] public byte[] Key { get; set; }
        [JsonProperty("nodes")] public HashSet<NodeInfo> Nodes { get; set; }
    }

    public class ResponseFindValue
    {
        [JsonProperty("key")] public byte[] Key { get; set; }
        [JsonProperty("value")] public string Value { get; set; }
    }
}

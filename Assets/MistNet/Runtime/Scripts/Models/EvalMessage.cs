using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace MistNet
{
    public class EvalMessage
    {
        [JsonProperty("type")] public EvalMessageType Type;
        [JsonProperty("payload")] public object Payload;
    }

    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public enum EvalMessageType
    {
        NodeSettings,
        NodeState,
        NodeLocation,
        NodeRequest,
        NodeReset,
        NetworkPartitionCheck,
    }

    public class EvalNodeSettings
    {
        [JsonProperty("nodeId")] public NodeId NodeId;
        [JsonProperty("config")] public MistOptConfigData ConfigData;
    }

    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public enum EvalNodeState
    {
        Visible,
        Connected,
        Disconnected,
    }

    public class EvalPosition
    {
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;
    }

    public class NodeState
    {
        [JsonProperty("node")] public Node Node;
        [JsonProperty("nodes")] public Node[] Nodes;
    }

    public class NodeRequest
    {
        [JsonProperty("nodeId")] public NodeId NodeId;
        [JsonProperty("targetNodeId")] public NodeId TargetNodeId;
        [JsonProperty("action")] public RequestActionType Action;
    }

    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public enum RequestActionType
    {
        Connect,
        Disconnect,
        SendNodeInfo,
        Join,
    }
}

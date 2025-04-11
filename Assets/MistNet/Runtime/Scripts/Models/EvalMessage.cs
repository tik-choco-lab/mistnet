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
        AllNodeStates,
        NodeLocation,
        NodeRequest,
    }

    public class EvalNodeSettings
    {
        [JsonProperty("nodeId")] public NodeId NodeId;
        [JsonProperty("config")] public MistOptConfig Config;
    }

    public class EvalNode
    {
        [JsonProperty("nodeId")] public NodeId NodeId;
        [JsonProperty("state")] public EvalNodeState State;
        [JsonProperty("position")] public EvalPosition Position;
    }

    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public enum EvalNodeState
    {
        Connected,
        Disconnected,
    }

    public class EvalPosition
    {
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;
    }
}

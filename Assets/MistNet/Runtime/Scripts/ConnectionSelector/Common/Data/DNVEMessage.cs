using Newtonsoft.Json;

namespace MistNet
{
    public class DNVEMessage
    {
        [JsonProperty("sender")] public NodeId Sender;
        [JsonProperty("receiver")] public NodeId Receiver;
        [JsonProperty("type")] public DNVEMessageType Type;
        [JsonProperty("payload")] public string Payload;
    }

    public enum DNVEMessageType
    {
        NodeList,
        Heartbeat,
    }
}

using Newtonsoft.Json.Converters;
using Newtonsoft.Json;

namespace MistNet
{
    public class SignalingData
    {
        [JsonProperty("senderId")]
        public NodeId SenderId;

        [JsonProperty("receiverId")]
        public NodeId ReceiverId;

        [JsonProperty("roomId")]
        public string RoomId;

        [JsonProperty("data")]
        public string Data;

        [JsonProperty("type")]
        [JsonConverter(typeof(StringEnumConverter))]
        public SignalingType Type;
    }

    public enum SignalingType
    {
        Offer,
        Answer,
        Candidate,
        Candidates,
        Request,
    }
}

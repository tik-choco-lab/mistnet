using Newtonsoft.Json.Converters;
using Newtonsoft.Json;

namespace MistNet
{
    public class SignalingData
    {
        [JsonProperty("senderId")]
        public string SenderId;

        [JsonProperty("receiverId")]
        public string ReceiverId;

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
    }
}

using System;
using Newtonsoft.Json;

namespace MistNet
{
    public class NodeInfo
    {
        [JsonProperty("id")] public NodeId Id { get; set; } = PeerRepository.I.SelfId;
        [JsonIgnore] public DateTime LastSeen = DateTime.UtcNow;
    }
}

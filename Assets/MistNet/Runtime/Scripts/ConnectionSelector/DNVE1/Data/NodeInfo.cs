using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MistNet
{
    public class NodeInfo
    {
        [JsonProperty("id")] public NodeId Id { get; set; } = PeerRepository.I.SelfId;
        [JsonIgnore] public DateTime LastSeen = DateTime.UtcNow;

        public override bool Equals(object obj)
        {
            return Equals(obj as NodeInfo);
        }

        public bool Equals(NodeInfo other)
        {
            return other != null && Id.Equals(other.Id);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(NodeInfo left, NodeInfo right)
        {
            return EqualityComparer<NodeInfo>.Default.Equals(left, right);
        }

        public static bool operator !=(NodeInfo left, NodeInfo right)
        {
            return !(left == right);
        }
    }
}

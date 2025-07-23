using System;

namespace MistNet
{
    public class NodeInfo
    {
        public NodeId Id { get; set; }
        public DateTime LastSeen;

        public NodeInfo()
        {
            Id = PeerRepository.I.SelfId;
            LastSeen = DateTime.UtcNow;
        }
    }
}

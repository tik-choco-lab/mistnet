using System;
using System.Collections.Generic;

namespace MistNet
{
    public interface IPeerRepository : IDisposable
    {
        void Init();
        NodeId SelfId { get; }
        IReadOnlyDictionary<NodeId, MistPeerDataElement> PeerDict { get; }
        PeerEntity GetPeer(NodeId id);
        PeerEntity CreatePeer(NodeId id);
        // bool IsConnectingOrConnected(NodeId id);
        void RemovePeer(NodeId id);
    }
}

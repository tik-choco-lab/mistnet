using System;

namespace MistNet
{
    public interface IPeerRepository : IDisposable
    {
        void Init();
        NodeId SelfId { get; }
        PeerEntity GetPeer(NodeId id);
        PeerEntity CreatePeer(NodeId id);
    }
}

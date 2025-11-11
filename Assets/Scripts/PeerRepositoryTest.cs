using System.Collections.Generic;

namespace MistNet.Minimal
{
    public class PeerRepositoryTest : IPeerRepository
    {
        public NodeId SelfId { get; }
        public IReadOnlyDictionary<NodeId, MistPeerDataElement> PeerDict { get; }
        private PeerEntity _peerEntity;

        public void Init()
        {
        }

        public PeerEntity GetPeer(NodeId id)
        {
            _peerEntity ??= new PeerEntity(id);
            return _peerEntity;
        }

        public PeerEntity CreatePeer(NodeId id)
        {
            _peerEntity ??= new PeerEntity(id);
            return _peerEntity;
        }

        public void RemovePeer(NodeId id)
        {
            throw new System.NotImplementedException();
        }

        public bool IsConnectingOrConnected(NodeId id)
        {
            throw new System.NotImplementedException();
        }

        public void Dispose()
        {
            _peerEntity.Dispose();
        }
    }
}

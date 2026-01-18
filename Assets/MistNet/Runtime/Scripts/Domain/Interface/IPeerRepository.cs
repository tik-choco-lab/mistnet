using System;
using System.Collections.Generic;
using UnityEngine;

namespace MistNet
{
    public interface IPeerRepository : IDisposable
    {
        void Init(ITransportLayer transport, NodeId selfId = null);
        NodeId SelfId { get; }
        IReadOnlyDictionary<NodeId, MistPeerDataElement> PeerDict { get; }
        PeerEntity GetPeer(NodeId id);
        PeerEntity CreatePeer(NodeId id);
        void RemovePeer(NodeId id);
        void AddInputAudioSource(AudioSource audioSource);
    }
}

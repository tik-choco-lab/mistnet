using System;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

namespace MistNet
{
    public class PeerRepository : IPeerRepository
    {
        public NodeId SelfId { get; private set; }
        public IReadOnlyDictionary<NodeId, MistPeerDataElement> PeerDict => GetAllPeer;
        public Dictionary<NodeId, MistPeerDataElement> GetAllPeer { get; } = new();

        private AudioSource _selfAudioSource;
        private ITransportLayer _transport;

        public void Init(ITransportLayer transport, NodeId selfId = null)
        {
            _transport = transport;
            if (selfId != null)
            {
                SelfId = selfId;
                MistConfig.Data.NodeId = SelfId;
            }
            else InitSelfId();

            MistLogger.Info($"[Self ID] {SelfId}");
            GetAllPeer.Clear();
        }

        private void InitSelfId()
        {
            if (MistConfig.Data.RandomId || string.IsNullOrEmpty(MistConfig.Data.NodeId))
            {
                SelfId = new NodeId(Guid.NewGuid().ToString("N"));
                MistConfig.Data.NodeId = SelfId;
            }
            else SelfId = MistConfig.Data.NodeId;
        }

        private void AllForceClose()
        {
            foreach (var peerData in GetAllPeer.Values)
            {
                peerData.PeerEntity.Close();
            }
        }

        public PeerEntity CreatePeer(NodeId id)
        {
            if (GetAllPeer.TryGetValue(id, out var peerData))
            {
                if (peerData.PeerEntity.ActiveProtocol == PeerActiveProtocol.WebSocket) return peerData.PeerEntity;
                // WebSocketを優先する　WebRTCは以下の通り既存の物を一旦破棄する
                peerData.PeerEntity?.Dispose();
                GetAllPeer.Remove(id);
            }

            MistLogger.Debug($"[MistPeerData] Add {id}");
            GetAllPeer.Add(id, new MistPeerDataElement(id, _transport));
            GetAllPeer[id].PeerEntity.AddInputAudioSource(_selfAudioSource);

            return GetAllPeer[id].PeerEntity;
        }

        public PeerEntity GetPeer(NodeId id)
        {
            return GetAllPeer.TryGetValue(id, out var peerData) ? peerData.PeerEntity : null;
        }

        public MistPeerDataElement GetPeerData(NodeId id)
        {
            if (string.IsNullOrEmpty(id))
            {
                MistLogger.Error("GetPeerData id is null");
            }

            MistLogger.Debug($"[GetPeerData] {id}");
            return GetAllPeer.GetValueOrDefault(id);
        }

        public void RemovePeer(NodeId id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!GetAllPeer.TryGetValue(id, out var peerData)) return;
            MistLogger.Debug($"[MistPeerData] Delete {id}");
            peerData.PeerEntity?.Dispose();
            peerData.PeerEntity = null;
            GetAllPeer.Remove(id);
        }

        public void AddInputAudioSource(AudioSource audioSource)
        {
            _selfAudioSource = audioSource;
        }

        public void Dispose()
        {
            MistLogger.Debug("[MistPeerData] Dispose");
            AllForceClose();
            foreach (var peerData in GetAllPeer.Values)
            {
                peerData.PeerEntity?.Dispose();
            }

            SelfId = null;
            _selfAudioSource = null;
        }
    }

    public class MistPeerDataElement
    {
        public PeerEntity PeerEntity;

        public MistPeerDataElement(NodeId id, ITransportLayer transport)
        {
            PeerEntity = new(id, transport);
            MistLogger.Debug($"[MistPeerData] Create Peer {id}");
        }

        public bool IsConnected
        {
            get
            {
                if (PeerEntity == null) return false;
                if (PeerEntity.RtcPeer == null) return false;
                return PeerEntity.RtcPeer.ConnectionState == RTCPeerConnectionState.Connected;
            }
        }
    }
}

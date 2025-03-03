using System;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

namespace MistNet
{
    public class MistPeerData
    {
        public static MistPeerData I { get; private set; } = new();
        public NodeId SelfId { get; private set; }
        public Dictionary<NodeId, MistPeerDataElement> GetAllPeer { get; } = new();

        private AudioSource _selfAudioSource;

        public void Init()
        {
            I = this;
            InitSelfId();

            MistDebug.Log($"[Self ID] {SelfId}");
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

        public void AllForceClose()
        {
            foreach (var peerData in GetAllPeer.Values)
            {
                peerData.Peer.Close();
            }
        }

        /// <summary>
        /// TODO: IsConnectedが正しく機能していないかも
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool IsConnected(NodeId id)
        {
            return GetAllPeer.TryGetValue(id, out var data) && data.IsConnected;
        }

        public bool IsConnectingOrConnected(NodeId id)
        {
            if (!GetAllPeer.TryGetValue(id, out var data)) return false;
            return data.Peer.Connection.ConnectionState is RTCPeerConnectionState.Connected or RTCPeerConnectionState.Connecting;
        }

        public MistPeer GetPeer(NodeId id)
        {
            if (GetAllPeer.TryGetValue(id, out var peerData))
            {
                if (peerData.Peer == null)
                {
                    peerData.Peer = new MistPeer(id);
                    peerData.Peer.AddInputAudioSource(_selfAudioSource);
                }
                else peerData.Peer.Id = id;

                return peerData.Peer;
            }

            GetAllPeer.Add(id, new MistPeerDataElement(id));
            GetAllPeer[id].Peer.AddInputAudioSource(_selfAudioSource);

            return GetAllPeer[id].Peer;
        }

        public MistPeerDataElement GetPeerData(NodeId id)
        {
            if (string.IsNullOrEmpty(id))
            {
                MistDebug.LogError("GetPeerData id is null");
            }

            MistDebug.Log($"[GetPeerData] {id}");
            return GetAllPeer.GetValueOrDefault(id);
        }

        public void OnDisconnected(NodeId id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!GetAllPeer.TryGetValue(id, out var peerData)) return;
            MistDebug.Log($"[MistPeerData] Delete {id}");
            peerData.Peer?.Dispose();
            peerData.Peer = null;
            GetAllPeer.Remove(id);
        }

        public void AddInputAudioSource(AudioSource audioSource)
        {
            _selfAudioSource = audioSource;
        }
    }

    public class MistPeerDataElement
    {
        public MistPeer Peer;

        public MistPeerDataElement(NodeId id)
        {
            Peer = new(id);
        }

        public bool IsConnected => Peer.Connection.ConnectionState == RTCPeerConnectionState.Connected;
    }
}

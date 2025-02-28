using System;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

namespace MistNet
{
    public class MistPeerData
    {
        public static MistPeerData I { get; private set; } = new();
        public string SelfId { get; private set; }
        public Dictionary<string, MistPeerDataElement> GetAllPeer { get; } = new();

        private AudioSource _selfAudioSource;

        public void Init()
        {
            I = this;
            SelfId = Guid.NewGuid().ToString("N");
            MistDebug.Log($"[Self ID] {SelfId}");
            GetAllPeer.Clear();
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
        public bool IsConnected(string id)
        {
            return GetAllPeer.TryGetValue(id, out var data) && data.IsConnected;
        }

        public MistPeer GetPeer(string id)
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

        public MistPeerDataElement GetPeerData(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                MistDebug.LogError("GetPeerData id is null");
            }

            MistDebug.Log($"[GetPeerData] {id}");
            return GetAllPeer.GetValueOrDefault(id);
        }

        public void SetState(string id, MistPeerState state)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!GetAllPeer.TryGetValue(id, out var peerData)) return;
            peerData.State = state;
            if (state == MistPeerState.Disconnected && peerData.Peer != null)
            {
                peerData.Peer.Dispose();
                peerData.Peer = null;
            }
        }

        public void OnDisconnected(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!GetAllPeer.TryGetValue(id, out var peerData)) return;
            peerData.State = MistPeerState.Disconnected;
            peerData.Peer?.Dispose();
            peerData.Peer = null;
            GetAllPeer.Remove(id); // これを書くかどうかはCacheに関わりそう　Cacheは別で用意した方がいいかも
        }

        public void AddInputAudioSource(AudioSource audioSource)
        {
            _selfAudioSource = audioSource;
        }
    }

    public class MistPeerDataElement
    {
        public MistPeer Peer;
        /// <summary>
        /// TODO: ↓ 正しい値になっていないことがある
        /// </summary>
        public MistPeerState State = MistPeerState.Disconnected;

        public MistPeerDataElement(string id)
        {
            Peer = new(id);
        }

        public bool IsConnected => Peer.Connection.ConnectionState == RTCPeerConnectionState.Connected;
    }
}

using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Unity.WebRTC;
using UnityEngine;

namespace MistNet
{
    public class MistSignalingHandler
    {
        public Action<SignalingData, string> Send;
        private readonly HashSet<string> _candidateData = new();

        /// <summary>
        /// ★send offer → receive answer
        /// </summary>
        /// <returns></returns>
        public async UniTask SendOffer(string receiverId)
        {
            MistDebug.Log($"[MistSignaling] SendOffer: {receiverId}");
            var peer = MistManager.I.MistPeerData.GetPeer(receiverId);
            peer.OnCandidate = ice => SendCandidate(ice, receiverId);

            var desc = await peer.CreateOffer();
            var sendData = CreateSendData();
            sendData.Type = SignalingType.Offer;
            sendData.Data = JsonConvert.SerializeObject(desc);
            sendData.ReceiverId = receiverId;

            Send(sendData, receiverId);
        }

        /// <summary>
        /// send offer → ★receive answer
        /// </summary>
        /// <param name="response"></param>
        public void ReceiveAnswer(SignalingData response)
        {
            var targetId = response.SenderId;
            var peer = MistManager.I.MistPeerData.GetPeer(targetId);

            var sdpJson = response.Data;
            if (string.IsNullOrEmpty(sdpJson))
            {
                MistDebug.LogError("sdp is null or empty");
                return;
            }
            MistDebug.Log($"[MistSignaling][SignalingState] {peer.SignalingState}");
            if (peer.SignalingState == MistSignalingState.NegotiationCompleted) return;
            if (peer.SignalingState == MistSignalingState.InitialStable) return;

            var sdp = JsonConvert.DeserializeObject<RTCSessionDescription>(sdpJson);
            peer.SetRemoteDescription(sdp).Forget();
        }

        /// <summary>
        /// ★receive offer → send answer
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        public void ReceiveOffer(SignalingData response)
        {
            var targetId = response.SenderId;

            var peer = MistPeerData.I.GetPeer(targetId);
            if (peer.SignalingState == MistSignalingState.NegotiationCompleted) return;
            
            peer.OnCandidate = (ice) => SendCandidate(ice, targetId);

            MistDebug.Log($"[MistSignaling][SignalingState] {peer.Connection.SignalingState}");
            var sdpJson = response.Data;
            var sdp = JsonConvert.DeserializeObject<RTCSessionDescription>(sdpJson);
            SendAnswer(peer, sdp, targetId).Forget();
        }

        /// <summary>
        /// receive offer → ★send answer
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="sdp"></param>
        /// <returns></returns>
        private async UniTask SendAnswer(MistPeer peer, RTCSessionDescription sdp, string targetId)
        {
            var desc = await peer.CreateAnswer(sdp);

            var sendData = CreateSendData();
            sendData.Type = SignalingType.Answer;
            sendData.Data = JsonConvert.SerializeObject(desc);
            sendData.ReceiverId = targetId;
            Send(sendData, targetId);

            if (_candidateData.Count == 0) return;
            foreach (var candidate in _candidateData)
            {
                var value = JsonUtility.FromJson<Ice>(candidate);
                peer.AddIceCandidate(value);
            }
        }

        private void SendCandidate(Ice candidate, string targetId = "")
        {
            var candidateString = JsonUtility.ToJson(candidate);
            if (_candidateData.Contains(candidateString))
            {
                MistDebug.Log($"[MistSignaling] Candidate already sent: {candidateString}");
                return;
            }

            var sendData = CreateSendData();
            sendData.Type = SignalingType.Candidate;
            sendData.ReceiverId = targetId;
            sendData.Data = candidateString;
            Send(sendData, targetId);
            _candidateData.Add(candidateString);
            
            // 接続が完了したら、関連するICE候補を削除
            var peer = MistManager.I.MistPeerData.GetPeer(targetId).Connection;
            RegisterIceConnectionChangeHandler(targetId, peer);
        }

        public async void ReceiveCandidate(SignalingData response)
        {
            var targetId = response.SenderId;
            var dataStr = response.Data;

            MistPeer peer;
            while (true)
            {
                peer = MistManager.I.MistPeerData.GetPeer(targetId);
                if (peer != null) break;
                await UniTask.Yield();
            }

            var candidates = dataStr.Split("|");

            foreach (var candidate in candidates)
            {
                if (_candidateData.Contains(candidate))
                {
                    MistDebug.Log($"[MistSignaling] Candidate already processed: {candidate}");
                    continue;
                }

                var value = JsonUtility.FromJson<Ice>(candidate);
                _candidateData.Add(candidate);

                peer.AddIceCandidate(value);
            }
        }

        /// <summary>
        /// 送信用データを作成する
        /// </summary>
        /// <returns></returns>
        private static SignalingData CreateSendData()
        {
            var sendData = new SignalingData
            {
                SenderId = MistManager.I.MistPeerData.SelfId,
                RoomId = MistConfig.Data.RoomId
            };

            return sendData;
        }
        
        private void RegisterIceConnectionChangeHandler(string targetId, RTCPeerConnection peer)
        {
            void PeerOnIceConnectionChange(RTCIceConnectionState state)
            {
                if (state == RTCIceConnectionState.Connected || state == RTCIceConnectionState.Completed)
                {
                    _candidateData.RemoveWhere(c => c.Contains($"\"receiverId\":\"{targetId}\""));
                }

                if (state == RTCIceConnectionState.Closed || state == RTCIceConnectionState.Failed || state == RTCIceConnectionState.Disconnected)
                {
                    // 接続が切断された場合の処理を追加
                    peer.OnIceConnectionChange -= PeerOnIceConnectionChange;
                }
            }

            peer.OnIceConnectionChange += PeerOnIceConnectionChange;
        }
    }
}

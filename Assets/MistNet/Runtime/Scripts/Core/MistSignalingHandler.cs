using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Newtonsoft.Json;
using Unity.WebRTC;
using UnityEngine;

namespace MistNet
{
    public class MistSignalingHandler : IDisposable
    {
        public Action<SignalingData, NodeId> Send;
        private readonly CancellationTokenSource _cts = new();

        public void RequestOffer(SignalingData response)
        {
            var senderId = response.SenderId;
            if(!MistManager.I.CompareId(senderId)) return;
            SendOffer(new NodeId(response.SenderId)).Forget();
        }

        /// <summary>
        /// ★send offer → receive answer
        /// </summary>
        /// <returns></returns>
        public async UniTask SendOffer(NodeId receiverId)
        {
            var peer = MistManager.I.MistPeerData.CreatePeer(receiverId);
            if (peer.RtcPeer.ConnectionState == RTCPeerConnectionState.Connecting)
            {
                MistDebug.LogWarning($"[Warning][MistSignaling] Peer is connecting: {receiverId}");
                return;
            }
            if (peer.RtcPeer.SignalingState != RTCSignalingState.Stable)
            {
                MistDebug.LogWarning($"[Warning][MistSignaling] SignalingState is not stable: {peer.RtcPeer.SignalingState}");
                return;
            }

            peer.OnCandidate = ice => SendCandidate(ice, receiverId);

            var desc = await peer.CreateOffer();
            var sendData = CreateSendData();
            sendData.Type = SignalingType.Offer;
            sendData.Data = JsonConvert.SerializeObject(desc);
            sendData.ReceiverId = receiverId;

            Send(sendData, receiverId);
            MistDebug.Log($"[MistSignaling] SendOffer: {receiverId}");
        }

        /// <summary>
        /// send offer → ★receive answer
        /// </summary>
        /// <param name="response"></param>
        public void ReceiveAnswer(SignalingData response)
        {
            MistDebug.Log($"[MistSignaling] ReceiveAnswer: {response.SenderId}");
            var targetId = response.SenderId;
            var peer = MistManager.I.MistPeerData.GetPeer(targetId);

            // if (peer.Connection.SignalingState != RTCSignalingState.HaveLocalOffer)
            // {
            //     MistDebug.LogError($"[Error][MistSignaling] SignalingState is not have local offer: {peer.Connection.SignalingState}");
            //     return;
            // }

            var sdpJson = response.Data;
            if (string.IsNullOrEmpty(sdpJson))
            {
                MistDebug.LogError("sdp is null or empty");
                return;
            }
            // MistDebug.Log($"[MistSignaling][SignalingState] {peer.SignalingState}");
            // if (peer.SignalingState == MistSignalingState.NegotiationCompleted) return;
            // if (peer.SignalingState == MistSignalingState.InitialStable) return;

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
            MistDebug.Log($"[MistSignaling] ReceiveOffer: {response.SenderId}");
            var targetId = response.SenderId;

            var peer = MistPeerData.I.CreatePeer(targetId);

            if (peer.RtcPeer.SignalingState != RTCSignalingState.Stable)
            {
                MistDebug.LogWarning($"[Error][MistSignaling] SignalingState is not stable: {peer.RtcPeer.SignalingState}");
                return;
            }
            peer.OnCandidate = (ice) => SendCandidate(ice, targetId);

            var sdpJson = response.Data;
            var sdp = JsonConvert.DeserializeObject<RTCSessionDescription>(sdpJson);
            SendAnswer(peer, sdp, targetId).Forget();
        }

        /// <summary>
        /// receive offer → ★send answer
        /// </summary>
        /// <param name="peerEntity"></param>
        /// <param name="sdp"></param>
        /// <param name="targetId"></param>
        /// <returns></returns>
        private async UniTask SendAnswer(PeerEntity peerEntity, RTCSessionDescription sdp, NodeId targetId)
        {
            var desc = await peerEntity.CreateAnswer(sdp);

            var sendData = CreateSendData();
            sendData.Type = SignalingType.Answer;
            sendData.Data = JsonConvert.SerializeObject(desc);
            sendData.ReceiverId = targetId;
            Send(sendData, targetId);
            MistDebug.Log($"[MistSignaling] SendAnswer: {targetId}");
        }

        private void SendCandidate(Ice candidate, NodeId targetId)
        {
            var candidateString = JsonUtility.ToJson(candidate);

            var sendData = CreateSendData();
            sendData.Type = SignalingType.Candidate;
            sendData.ReceiverId = targetId;
            sendData.Data = candidateString;
            Send(sendData, targetId);

            // 接続が完了したら、関連するICE候補を削除
            var peer = MistManager.I.MistPeerData.GetPeer(targetId).RtcPeer;
            RegisterIceConnectionChangeHandler(targetId, peer);
            MistDebug.Log($"[MistSignaling] SendCandidate: {targetId}");
        }

        public async void ReceiveCandidates(SignalingData response)
        {
            MistDebug.Log($"[MistSignaling] ReceiveCandidates: {response.SenderId}");
            var senderId = response.SenderId;
            var candidatesStr = response.Data;
            var candidatesArray = JsonConvert.DeserializeObject<string[]>(candidatesStr);
            var peer = await GetPeer(senderId, _cts.Token);
            // setRemoteDescriptionが完了するまで待つ
            if (!await WaitForRemoteOfferOrPrAnswerWithTimeout(peer)) return;
            foreach (var candidateStr in candidatesArray)
            {
                MistDebug.Log($"[MistSignaling] ReceiveCandidates: {candidateStr}");
                var candidate = JsonUtility.FromJson<Ice>(candidateStr);
                peer.AddIceCandidate(candidate);
            }
        }

        public async void ReceiveCandidate(SignalingData response)
        {
            MistDebug.Log($"[MistSignaling] ReceiveCandidate: {response.SenderId}");
            var senderId = response.SenderId;
            var peer = await GetPeer(senderId, _cts.Token);

            var candidateStr = response.Data;
            var candidate = JsonUtility.FromJson<Ice>(candidateStr);
            peer.AddIceCandidate(candidate);
        }

        private async UniTask<bool> WaitForRemoteOfferOrPrAnswerWithTimeout(PeerEntity peerEntity)
        {
            if (peerEntity.RtcPeer.SignalingState is RTCSignalingState.HaveRemoteOffer or RTCSignalingState.HaveRemotePrAnswer) return true;

            const int timeoutMilliseconds = 5000;
            var cts = new CancellationTokenSource(timeoutMilliseconds);

            try
            {
                // UniTask.WhenAnyで待機。SignalingStateが条件を満たすか、タイムアウトを待つ
                await UniTask.WhenAny(
                    UniTask.WaitUntil(() => peerEntity.RtcPeer?.SignalingState is RTCSignalingState.HaveRemoteOffer
                        or RTCSignalingState.HaveRemotePrAnswer, cancellationToken: cts.Token),
                    UniTask.WaitUntil(() => peerEntity.RtcPeer == null, cancellationToken: cts.Token),
                    UniTask.WaitUntil(()=> peerEntity.RtcPeer?.IceConnectionState is RTCIceConnectionState.Closed or RTCIceConnectionState.Connected, cancellationToken: cts.Token)
                );

                if (peerEntity.RtcPeer == null) return false;
                if (peerEntity.RtcPeer.IceConnectionState is RTCIceConnectionState.Closed or RTCIceConnectionState.Connected) return false;

                return peerEntity.RtcPeer.SignalingState is RTCSignalingState.HaveRemoteOffer or RTCSignalingState.HaveRemotePrAnswer;
            }
            catch (OperationCanceledException)
            {
                // 既に接続が完了している場合は、タイムアウトしても問題ない
                if (peerEntity.RtcPeer.SignalingState is RTCSignalingState.Stable or RTCSignalingState.Closed) return false;

                MistDebug.LogError("[MistSignaling][Candidate] Timeout");
                return false;
            }
        }

        private static async UniTask<PeerEntity> GetPeer(NodeId targetId, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var peer = MistManager.I.MistPeerData.GetPeer(targetId);
                if (peer != null) return peer;
                await UniTask.Yield();
            }

            return null;
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
                    // _candidateData.RemoveWhere(c => c.Contains($"\"receiverId\":\"{targetId}\""));
                }

                if (state == RTCIceConnectionState.Closed || state == RTCIceConnectionState.Failed || state == RTCIceConnectionState.Disconnected)
                {
                    // 接続が切断された場合の処理を追加
                    peer.OnIceConnectionChange -= PeerOnIceConnectionChange;
                }
            }

            peer.OnIceConnectionChange += PeerOnIceConnectionChange;
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}

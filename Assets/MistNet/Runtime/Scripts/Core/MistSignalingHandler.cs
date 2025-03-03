using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
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
            MistDebug.Log($"[MistSignaling] SendOffer: {receiverId}");
            var peer = MistManager.I.MistPeerData.GetPeer(receiverId);
            if (peer.Connection.SignalingState != RTCSignalingState.Stable)
            {
                MistDebug.LogError($"[Error][MistSignaling] SignalingState is not stable: {peer.Connection.SignalingState}");
                return;
            }

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
            MistDebug.Log($"[MistSignaling] ReceiveAnswer: {response.SenderId}");
            var targetId = response.SenderId;
            var peer = MistManager.I.MistPeerData.GetPeer(targetId);

            if (peer.Connection.SignalingState != RTCSignalingState.HaveLocalOffer)
            {
                MistDebug.LogError($"[Error][MistSignaling] SignalingState is not have local offer: {peer.Connection.SignalingState}");
                return;
            }

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

            var peer = MistPeerData.I.GetPeer(targetId);

            if (peer.Connection.SignalingState != RTCSignalingState.Stable)
            {
                MistDebug.LogError($"[Error][MistSignaling] SignalingState is not stable: {peer.Connection.SignalingState}");
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
        /// <param name="peer"></param>
        /// <param name="sdp"></param>
        /// <param name="targetId"></param>
        /// <returns></returns>
        private async UniTask SendAnswer(MistPeer peer, RTCSessionDescription sdp, NodeId targetId)
        {
            MistDebug.Log($"[MistSignaling] SendAnswer: {targetId}");
            var desc = await peer.CreateAnswer(sdp);

            // if (peer.Connection.SignalingState != RTCSignalingState.HaveRemoteOffer)
            // {
            //     MistDebug.LogError($"[Error][MistSignaling] SignalingState is not have remote offer: {peer.Connection.SignalingState}");
            //     return;
            // }

            var sendData = CreateSendData();
            sendData.Type = SignalingType.Answer;
            sendData.Data = JsonConvert.SerializeObject(desc);
            sendData.ReceiverId = targetId;
            Send(sendData, targetId);
        }

        private void SendCandidate(Ice candidate, NodeId targetId)
        {
            MistDebug.Log($"[MistSignaling] SendCandidate: {targetId}");
            var candidateString = JsonUtility.ToJson(candidate);

            var sendData = CreateSendData();
            sendData.Type = SignalingType.Candidate;
            sendData.ReceiverId = targetId;
            sendData.Data = candidateString;
            Send(sendData, targetId);

            // 接続が完了したら、関連するICE候補を削除
            var peer = MistManager.I.MistPeerData.GetPeer(targetId).Connection;
            RegisterIceConnectionChangeHandler(targetId, peer);
        }

        public async void ReceiveCandidates(SignalingData response)
        {
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
            var senderId = response.SenderId;
            var peer = await GetPeer(senderId, _cts.Token);
            // setRemoteDescriptionが完了するまで待つ
            if (!await WaitForRemoteOfferOrPrAnswerWithTimeout(peer)) return;
            MistDebug.Log($"[MistSignaling] ReceiveCandidate: {response.SenderId}");
            var candidateStr = response.Data;
            var candidate = JsonUtility.FromJson<Ice>(candidateStr);
            peer.AddIceCandidate(candidate);
        }

        private async UniTask<bool> WaitForRemoteOfferOrPrAnswerWithTimeout(MistPeer peer)
        {
            if (peer.Connection.SignalingState is RTCSignalingState.HaveRemoteOffer or RTCSignalingState.HaveRemotePrAnswer) return true;

            const int timeoutMilliseconds = 5000;
            var cts = new CancellationTokenSource(timeoutMilliseconds);

            try
            {
                // UniTask.WhenAnyで待機。SignalingStateが条件を満たすか、タイムアウトを待つ

                await UniTask.WhenAny(
                    UniTask.WaitUntil(() => peer?.Connection?.SignalingState is RTCSignalingState.HaveRemoteOffer
                        or RTCSignalingState.HaveRemotePrAnswer, cancellationToken: cts.Token),
                    UniTask.WaitUntil(() => peer.Connection == null, cancellationToken: cts.Token)
                );

                if (peer.Connection == null) return false;

                return peer.Connection.SignalingState is RTCSignalingState.HaveRemoteOffer or RTCSignalingState.HaveRemotePrAnswer;
            }
            catch (OperationCanceledException)
            {
                // 既に接続が完了している場合は、タイムアウトしても問題ない
                if (peer.Connection.SignalingState is RTCSignalingState.Stable or RTCSignalingState.Closed) return false;

                MistDebug.LogError("[MistSignaling][Candidate] Timeout");
                return false;
            }
        }


        private static async UniTask<MistPeer> GetPeer(NodeId targetId, CancellationToken token)
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

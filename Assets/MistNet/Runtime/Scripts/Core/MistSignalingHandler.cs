using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using MistNet.Utils;
using Newtonsoft.Json;
using Unity.WebRTC;
using UnityEngine;

namespace MistNet
{
    public class MistSignalingHandler : IDisposable
    {
        private const float TimeoutSeconds = 5f;
        public Action<SignalingData, NodeId> Send;
        private readonly CancellationTokenSource _cts = new();
        private readonly PeerActiveProtocol _activeProtocol;
        private readonly IPeerRepository _peerRepository;

        public MistSignalingHandler(PeerActiveProtocol activeProtocol, IPeerRepository peerRepository)
        {
            _activeProtocol = activeProtocol;
            _peerRepository = peerRepository;
        }

        public void RequestOffer(SignalingData response)
        {
            var senderId = response.SenderId;
            if(!IdUtil.CompareId(_peerRepository.SelfId, senderId)) return;
            SendOffer(new NodeId(response.SenderId)).Forget();
        }

        /// <summary>
        /// ★send offer → receive answer
        /// </summary>
        /// <returns></returns>
        public async UniTask SendOffer(NodeId receiverId)
        {
            var peer = _peerRepository.CreatePeer(receiverId);
            if (peer.RtcPeer.ConnectionState == RTCPeerConnectionState.Connecting)
            {
                MistLogger.Warning($"[Warning][Signaling] Peer is connecting: {receiverId}");
                return;
            }
            if (peer.RtcPeer.SignalingState != RTCSignalingState.Stable)
            {
                MistLogger.Warning($"[Warning][Signaling] SignalingState is not stable: {peer.RtcPeer.SignalingState}");
                return;
            }
            peer.ActiveProtocol = _activeProtocol;
            peer.OnCandidate = ice => SendCandidate(ice, receiverId);

            RTCSessionDescription? desc;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            try
            {
                desc = await peer.CreateOffer(cts.Token);
            }
            catch (OperationCanceledException)
            {
                MistLogger.Warning($"[Signaling][Offer] Timeout operation cancelled: {receiverId}");
                _peerRepository.RemovePeer(receiverId);
                return;
            }

            var sendData = CreateSendData();
            sendData.Type = SignalingType.Offer;
            sendData.Data = JsonConvert.SerializeObject(desc);
            sendData.ReceiverId = receiverId;

            Send(sendData, receiverId);
            MistLogger.Debug($"[Signaling] SendOffer: {receiverId}");
        }

        /// <summary>
        /// send offer → ★receive answer
        /// </summary>
        /// <param name="response"></param>
        public void ReceiveAnswer(SignalingData response)
        {
            MistLogger.Debug($"[Signaling] ReceiveAnswer: {response.SenderId}");
            var targetId = response.SenderId;
            var peer = _peerRepository.GetPeer(targetId);
            if (peer == null) return;
            if (peer.RtcPeer.SignalingState != RTCSignalingState.HaveLocalOffer)
            {
                MistLogger.Warning($"[Error][Signaling] SignalingState is not HaveLocalOffer: {peer.RtcPeer.SignalingState}");
                return;
            }

            var sdpJson = response.Data;
            if (string.IsNullOrEmpty(sdpJson))
            {
                MistLogger.Error("sdp is null or empty");
                return;
            }

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
            MistLogger.Debug($"[Signaling] ReceiveOffer: {response.SenderId}");
            var targetId = response.SenderId;

            var peer = _peerRepository.CreatePeer(targetId);

            if (peer.RtcPeer.SignalingState != RTCSignalingState.Stable)
            {
                MistLogger.Warning($"[Error][Signaling] SignalingState is not stable: {peer.RtcPeer.SignalingState}");
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
            if (peerEntity == null)
            {
                MistLogger.Warning("[Signaling][Answer] PeerEntity is null");
                return;
            }
            RTCSessionDescription? desc;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            try
            {
                desc = await peerEntity.CreateAnswer(sdp, cts.Token);
            }
            catch (OperationCanceledException)
            {
                MistLogger.Warning("[Signaling] Timeout, operation cancelled");
                _peerRepository.RemovePeer(targetId);
                return;
            }

            var sendData = CreateSendData();
            sendData.Type = SignalingType.Answer;
            sendData.Data = JsonConvert.SerializeObject(desc);
            sendData.ReceiverId = targetId;
            Send(sendData, targetId);
            MistLogger.Debug($"[Signaling] SendAnswer: {targetId}");
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
            var peer = _peerRepository.GetPeer(targetId).RtcPeer;
            RegisterIceConnectionChangeHandler(peer);
            MistLogger.Debug($"[Signaling] SendCandidate: {targetId}");
        }

        public async void ReceiveCandidates(SignalingData response)
        {
            MistLogger.Debug($"[Signaling] ReceiveCandidates: {response.SenderId}");
            var senderId = response.SenderId;
            var candidatesStr = response.Data;
            var candidatesArray = JsonConvert.DeserializeObject<string[]>(candidatesStr);
            var peer = await GetPeer(senderId, _cts.Token);
            // setRemoteDescriptionが完了するまで待つ
            if (!await WaitForRemoteOfferOrPrAnswerWithTimeout(peer)) return;
            foreach (var candidateStr in candidatesArray)
            {
                MistLogger.Debug($"[Signaling] ReceiveCandidates: {candidateStr}");
                var candidate = JsonUtility.FromJson<Ice>(candidateStr);
                peer.AddIceCandidate(candidate);
            }
        }

        public async void ReceiveCandidate(SignalingData response)
        {
            MistLogger.Debug($"[Signaling] ReceiveCandidate: {response.SenderId}");
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

                MistLogger.Error("[Signaling][Candidate] Timeout");
                return false;
            }
        }

        private async UniTask<PeerEntity> GetPeer(NodeId targetId, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var peer = _peerRepository.GetPeer(targetId);
                if (peer != null) return peer;
                await UniTask.Yield();
            }

            return null;
        }

        /// <summary>
        /// 送信用データを作成する
        /// </summary>
        /// <returns></returns>
        private SignalingData CreateSendData()
        {
            var sendData = new SignalingData
            {
                SenderId = _peerRepository.SelfId,
                RoomId = MistConfig.Data.RoomId
            };

            return sendData;
        }
        
        private static void RegisterIceConnectionChangeHandler(RTCPeerConnection peer)
        {
            peer.OnIceConnectionChange += PeerOnIceConnectionChange;
            return;

            void PeerOnIceConnectionChange(RTCIceConnectionState state)
            {
                if (state == RTCIceConnectionState.Closed || state == RTCIceConnectionState.Failed || state == RTCIceConnectionState.Disconnected)
                {
                    // 接続が切断された場合の処理を追加
                    peer.OnIceConnectionChange -= PeerOnIceConnectionChange;
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}

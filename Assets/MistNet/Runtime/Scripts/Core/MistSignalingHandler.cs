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
        private const float CreateSdpTimeoutSeconds = 5f; // SDP生成用
        private const float ConnectionTimeoutSeconds = 10f; // 接続確立待ち用

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
            // SDP生成のタイムアウト
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CreateSdpTimeoutSeconds));
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
            MistLogger.Debug($"[Signaling] SendOffer {_peerRepository.SelfId}: {receiverId}");

            // 接続監視を開始
            WaitForIceConnection(receiverId, ConnectionTimeoutSeconds).Forget();
        }

        /// <summary>
        /// send offer → ★receive answer
        /// </summary>
        /// <param name="response"></param>
        public void ReceiveAnswer(SignalingData response)
        {
            MistLogger.Debug($"[Signaling] ReceiveAnswer {_peerRepository.SelfId}: {response.SenderId}");
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
            MistLogger.Debug($"[Signaling] ReceiveOffer {_peerRepository.SelfId}: {response.SenderId}");
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
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CreateSdpTimeoutSeconds));
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
            MistLogger.Debug($"[Signaling] SendAnswer {_peerRepository.SelfId}: {targetId}");

            // 接続監視を開始
            WaitForIceConnection(targetId, ConnectionTimeoutSeconds).Forget();
        }

        /// <summary>
        /// 指定時間内にIceConnectionStateがConnectedにならなければPeerを削除する
        /// </summary>
        private async UniTaskVoid WaitForIceConnection(NodeId targetId, float timeoutSec)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
            try
            {
                // Peerを取得
                var peer = _peerRepository.GetPeer(targetId);
                if (peer == null) return;

                // 接続完了(Connected) または 切断(Closed/Disconnected)になるまで待機
                await UniTask.WaitUntil(() =>
                {
                    // 途中でPeerが消された場合は終了
                    if (_peerRepository.GetPeer(targetId) == null) return true;

                    var state = peer.RtcPeer.IceConnectionState;
                    return state == RTCIceConnectionState.Connected ||
                           state == RTCIceConnectionState.Completed ||
                           state == RTCIceConnectionState.Closed ||
                           state == RTCIceConnectionState.Disconnected ||
                           state == RTCIceConnectionState.Failed;
                }, cancellationToken: cts.Token);

                // タイムアウトせずに抜けた場合のチェック
                if (peer.RtcPeer.IceConnectionState == RTCIceConnectionState.Connected)
                {
                    MistLogger.Debug($"[Signaling] Connection Established: {targetId}");
                }
                else
                {
                    // 明示的にClosedなどで終了した場合
                    MistLogger.Debug($"[Signaling] Connection Closed or Disconnected during wait: {targetId}");
                    _peerRepository.RemovePeer(targetId);
                }
            }
            catch (OperationCanceledException)
            {
                // タイムアウト発生時の処理
                MistLogger.Warning($"[Signaling] Connection Timeout (IceConnectionState did not reach Connected): {targetId}");
                _peerRepository.RemovePeer(targetId);
            }
            finally
            {
                cts.Dispose();
            }
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
            var peer = _peerRepository.GetPeer(targetId);
            if(peer != null) RegisterIceConnectionChangeHandler(peer.RtcPeer);

            MistLogger.Debug($"[Signaling] SendCandidate {_peerRepository.SelfId}: {targetId}");
        }

        public async void ReceiveCandidates(SignalingData response)
        {
            MistLogger.Debug($"[Signaling] ReceiveCandidates {_peerRepository.SelfId}: {response.SenderId}");
            var senderId = response.SenderId;
            var candidatesStr = response.Data;
            var candidatesArray = JsonConvert.DeserializeObject<string[]>(candidatesStr);
            var peer = await GetPeer(senderId, _cts.Token);
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
            peer.OnIceConnectionChange -= PeerOnIceConnectionChange; // 重複登録防止
            peer.OnIceConnectionChange += PeerOnIceConnectionChange;

            void PeerOnIceConnectionChange(RTCIceConnectionState state)
            {
                if (state == RTCIceConnectionState.Closed || state == RTCIceConnectionState.Failed || state == RTCIceConnectionState.Disconnected)
                {
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

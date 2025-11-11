using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;

namespace MistNet
{
    /// <summary>
    /// </summary>
    public class PeerEntity : IDisposable
    {
        private const float WaitReconnectTimeSec = 3f;
        private const string DataChannelLabel = "data";

        public RTCPeerConnection RtcPeer;

        public NodeId Id;
        private RTCDataChannel _dataChannel;

        public readonly Action<byte[], NodeId> OnMessage;
        public Action<Ice> OnCandidate;
        public readonly Action<NodeId> OnConnected;
        public readonly Action<NodeId> OnDisconnected;
        public PeerActiveProtocol ActiveProtocol = PeerActiveProtocol.None;

        private AudioSource _outputAudioSource;
        private RTCRtpSender _sender;

        private readonly CancellationTokenSource _cts = new();

        public PeerEntity(NodeId id, ITransportLayer transport)
        {
            Id = id;
            OnMessage += transport.OnMessage;
            OnConnected += transport.OnConnected;
            OnDisconnected += transport.OnDisconnected;

            // ----------------------------
            // Configuration
            var stunServer = new RTCIceServer
            {
                urls = MistConfig.Data.StunUrls,
            };

            var turnServer = MistConfig.Data.TurnServer;

            var configuration = default(RTCConfiguration);
            var iceServers = new List<RTCIceServer> { stunServer };
            if (turnServer.urls is { Length: > 0 }) iceServers.Add(turnServer);
            configuration.iceServers = iceServers.ToArray();

            RtcPeer = new RTCPeerConnection(ref configuration);

            // ----------------------------
            // Candidate
            RtcPeer.OnIceCandidate = OnIceCandidate;
            RtcPeer.OnIceConnectionChange += OnIceConnectionChange;
            RtcPeer.OnConnectionStateChange +=
                state => MistLogger.Debug($"[Signaling]<color=#feff57>[ConnectionState] {state} {id}</color>");
            RtcPeer.OnIceGatheringStateChange +=
                state => MistLogger.Debug($"[Signaling]<color=#56FF5B>[IceGatheringState] {state} {id}</color>");
            RtcPeer.OnNegotiationNeeded += () => MistLogger.Debug($"[Signaling]<color=#28FFEE>[OnNegotiationNeeded] {id}</color>");
            RtcPeer.OnTrack += OnTrack;
            // ----------------------------
            // DataChannels
            SetDataChannel();
        }

        public void Dispose()
        {
            RtcPeer?.Dispose();
            _dataChannel?.Dispose();
            _dataChannel = null;
            RtcPeer = null;
            _cts.Cancel();
        }

        public async UniTask<RTCSessionDescription> CreateOffer(CancellationToken ct)
        {
            MistLogger.Debug($"[Signaling][CreateOffer] {Id}");

            CreateDataChannel();

            var offerOp = RtcPeer.CreateOffer();
            while (!offerOp.IsDone)
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.Yield();
            }

            if (offerOp.IsError) return default;
            if (RtcPeer == null) return default;

            var desc = offerOp.Desc;
            var localOp = RtcPeer.SetLocalDescription(ref desc);
            if (localOp.IsError) return default;

            return desc;
        }

        public async UniTask<RTCSessionDescription> CreateAnswer(RTCSessionDescription remoteDescription, CancellationToken ct)
        {
            MistLogger.Debug($"[Signaling][CreateAnswer] {Id}");

            // RemoteDescription
            if (RtcPeer == null) return default;
            if (string.IsNullOrEmpty(remoteDescription.sdp)) return default;
            var remoteOp = RtcPeer.SetRemoteDescription(ref remoteDescription);
            while (!remoteOp.IsDone)
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.Yield();
            }
            if (remoteOp.IsError) return default;
            if (RtcPeer == null) return default;

            // CreateAnswer
            var answerOp = RtcPeer.CreateAnswer();
            while (!answerOp.IsDone)
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.Yield();
            }
            if (answerOp.IsError) return default;

            if (RtcPeer == null) return default;
            // LocalDescription
            var desc = answerOp.Desc;
            var localOp = RtcPeer.SetLocalDescription(ref desc);
            while (!localOp.IsDone)
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.Yield();
            }
            if (localOp.IsError) return default;

            return desc;
        }

        private void CreateDataChannel()
        {
            var config = new RTCDataChannelInit();
            _dataChannel = RtcPeer.CreateDataChannel(DataChannelLabel, config);
            _dataChannel.OnMessage = OnMessageDataChannel;
            _dataChannel.OnOpen = OnOpenDataChannel;
            _dataChannel.OnClose = OnCloseDataChannel;
        }

        private void SetDataChannel()
        {
            RtcPeer.OnDataChannel = channel =>
            {
                MistLogger.Debug("OnDataChannel");
                _dataChannel = channel;
                _dataChannel.OnOpen = OnOpenDataChannel;
                _dataChannel.OnClose = OnCloseDataChannel;
                _dataChannel.OnMessage = OnMessageDataChannel;
                OnOpenDataChannel();
            };
        }

        public async UniTaskVoid SetRemoteDescription(RTCSessionDescription remoteDescription)
        {
            if (RtcPeer == null) return;
            if (string.IsNullOrEmpty(remoteDescription.sdp)) return;
            var remoteDescriptionOperation = RtcPeer.SetRemoteDescription(ref remoteDescription);
            await remoteDescriptionOperation;
            if (remoteDescriptionOperation.IsError)
            {
                MistLogger.Error(
                    $"[Error][Signaling][SetRemoteDescription] 接続要求が同時に発生している可能性があります\n-> {Id} {remoteDescriptionOperation.Error.message}");
            }
        }

        public void AddIceCandidate(Ice candidate)
        {
            RtcPeer.AddIceCandidate(candidate.Get());
        }

        public void Send(byte[] data)
        {
            if (_dataChannel == null)
            {
                MistLogger.Warning($"[Send] DataChannel is null {Id}");
                return;
            }

            switch (_dataChannel)
            {
                case { ReadyState: RTCDataChannelState.Closed }:
                case { ReadyState: RTCDataChannelState.Closing }:
                case { ReadyState: RTCDataChannelState.Connecting }:
                    MistLogger.Warning($"[Send] DataChannel is not open {Id} {_dataChannel.ReadyState}");
                    if (_dataChannel.ReadyState == RTCDataChannelState.Closed) Close();
                    return;
            }

            // 評価用
            if (MistStats.I != null)
            {
                MistStats.I.TotalSendBytes += data.Length;
                MistStats.I.TotalMessageCount++;
            }

            _dataChannel.Send(data);
        }

        public void Close()
        {
            if (RtcPeer == null) return;
            if (_sender != null) RtcPeer.RemoveTrack(_sender);
            RtcPeer.Close();
            RtcPeer = null;
        }

        private void OnIceConnectionChange(RTCIceConnectionState state)
        {
            switch (state)
            {
                case RTCIceConnectionState.Connected:
                    break;
                case RTCIceConnectionState.Closed:
                case RTCIceConnectionState.Disconnected:
                case RTCIceConnectionState.Failed:
                case RTCIceConnectionState.Max:
                    OnDisconnected?.Invoke(Id);
                    break;
                case RTCIceConnectionState.New:
                case RTCIceConnectionState.Checking:
                case RTCIceConnectionState.Completed:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private async void OnOpenDataChannel()
        {
            await UniTask.Yield(); // DataChannelの状態が安定するまで待つ
            OnConnected?.Invoke(Id); // DataChannelが開いていないと、例えばInstantiateができないため、ここで呼ぶ
        }

        private void OnCloseDataChannel()
        {
        }

        private void OnMessageDataChannel(byte[] data)
        {
            if (MistStats.I != null) MistStats.I.TotalReceiveBytes += data.Length;
            OnMessage?.Invoke(data, Id);
        }

        private void OnIceCandidate(RTCIceCandidate candidate)
        {
            OnCandidate?.Invoke(new Ice(candidate));
        }

        private async void OnTrack(RTCTrackEvent e)
        {
            if (e.Track is not AudioStreamTrack track) return;
            await UniTask.WaitUntil(() => _outputAudioSource != null);
            MistLogger.Debug($"[MistPeer][OnTrack] {Id}");

            _outputAudioSource.SetTrack(track);
            _outputAudioSource.loop = true;
            _outputAudioSource.Play();
        }

        public void AddInputAudioSource(AudioSource audioSource)
        {
            if (audioSource == null) return;
            MistLogger.Debug($"[MistPeer][AddTrack] {Id}");
            var track = new AudioStreamTrack(audioSource);
            _sender = RtcPeer.AddTrack(track);
        }

        public void AddOutputAudioSource(AudioSource audioSource)
        {
            _outputAudioSource = audioSource;
        }

        private async UniTask Reconnect()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(WaitReconnectTimeSec));
            CreateOffer(_cts.Token).Forget();
        }
    }

    [Serializable]
    public class Ice
    {
        public string Candidate;
        public string SdpMid;
        public int SdpMLineIndex;

        public Ice(RTCIceCandidate candidate)
        {
            Candidate = candidate.Candidate;
            SdpMid = candidate.SdpMid;
            SdpMLineIndex = (int)candidate.SdpMLineIndex;
        }

        public RTCIceCandidate Get()
        {
            var data = new RTCIceCandidate(new RTCIceCandidateInit
            {
                candidate = Candidate,
                sdpMid = SdpMid,
                sdpMLineIndex = SdpMLineIndex
            });
            return data;
        }
    }
}

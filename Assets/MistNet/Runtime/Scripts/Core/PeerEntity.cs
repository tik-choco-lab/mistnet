using Cysharp.Threading.Tasks;
using System;
using Unity.WebRTC;
using UnityEngine;

namespace MistNet
{
    /// <summary>
    /// TODO: 相手のPeerでDataChannelが開いていない？
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

        private AudioSource _outputAudioSource;
        private RTCRtpSender _sender;

        public PeerEntity(NodeId id)
        {
            Id = id;
            OnMessage += MistManager.I.OnMessage;
            OnConnected += MistManager.I.OnConnected;
            OnDisconnected += MistManager.I.OnDisconnected;

            // ----------------------------
            // Configuration
            var configuration = default(RTCConfiguration);
            configuration.iceServers = new RTCIceServer[]
            {
                new() { urls = MistConfig.Data.StunUrls }
            };
            RtcPeer = new RTCPeerConnection(ref configuration);

            // ----------------------------
            // Candidate
            RtcPeer.OnIceCandidate = OnIceCandidate;
            RtcPeer.OnIceConnectionChange += OnIceConnectionChange;
            RtcPeer.OnConnectionStateChange +=
                state => MistDebug.Log($"[MistPeer][OnConnectionStateChange] {state}");
            RtcPeer.OnIceGatheringStateChange +=
                state => MistDebug.Log($"[MistPeer][OnIceGatheringStateChange] {state}");
            RtcPeer.OnNegotiationNeeded += () => MistDebug.Log($"[MistPeer][OnNegotiationNeeded] {Id}");
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
        }

        public async UniTask<RTCSessionDescription> CreateOffer()
        {
            MistDebug.Log($"[Signaling][CreateOffer] {Id}");

            CreateDataChannel(); // DataChannelを作成

            // ----------------------------
            // CreateOffer
            var offerOperation = RtcPeer.CreateOffer();
            await offerOperation;
            if (offerOperation.IsError)
            {
                MistDebug.LogError($"[Signaling][{Id}][Error][OfferOperation]");
                return default;
            }

            // ----------------------------
            // LocalDescription
            var desc = offerOperation.Desc;
            var localDescriptionOperation = RtcPeer.SetLocalDescription(ref desc);
            if (localDescriptionOperation.IsError)
            {
                MistDebug.LogError($"[Signaling][{Id}][Error][SetLocalDescription]");
                return default;
            }

            return desc;
        }

        public async UniTask<RTCSessionDescription> CreateAnswer(RTCSessionDescription remoteDescription)
        {
            MistDebug.Log($"[Signaling][CreateAnswer] {Id}");

            // ----------------------------
            // RemoteDescription
            var remoteDescriptionOperation = RtcPeer.SetRemoteDescription(ref remoteDescription);
            await remoteDescriptionOperation.ToUniTask();
            if (remoteDescriptionOperation.IsError)
            {
                Reconnect().Forget();
                MistDebug.LogError(
                    $"[Error][Signaling][SetRemoteDescription] {Id} {remoteDescriptionOperation.Error.message}");
                return default;
            }

            // ----------------------------
            // CreateAnswer
            var answerOperation = RtcPeer.CreateAnswer();
            await answerOperation.ToUniTask();
            ;
            if (answerOperation.IsError)
            {
                Reconnect().Forget();
                MistDebug.LogError(
                    $"[Error][Signaling][CreateAnswer] -> {Id} {answerOperation.Error.message} {RtcPeer.SignalingState}");
                return default;
            }

            // ----------------------------
            // LocalDescription
            var desc = answerOperation.Desc;
            var localDescriptionOperation = RtcPeer.SetLocalDescription(ref desc);
            if (localDescriptionOperation.IsError)
            {
                Reconnect().Forget();
                MistDebug.LogError(
                    $"[Error][Signaling][SetLocalDescription] -> {Id} {localDescriptionOperation.Error.message}");
                return default;
            }

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
                MistDebug.Log("OnDataChannel");
                _dataChannel = channel;
                _dataChannel.OnOpen = OnOpenDataChannel;
                _dataChannel.OnClose = OnCloseDataChannel;
                _dataChannel.OnMessage = OnMessageDataChannel;
                OnOpenDataChannel();
            };
        }

        public async UniTaskVoid SetRemoteDescription(RTCSessionDescription remoteDescription)
        {
            var remoteDescriptionOperation = RtcPeer.SetRemoteDescription(ref remoteDescription);
            await remoteDescriptionOperation;
            if (remoteDescriptionOperation.IsError)
            {
                MistDebug.LogError(
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
                MistDebug.LogWarning($"[Send] DataChannel is null {Id}");
                return;
            }

            switch (_dataChannel)
            {
                case { ReadyState: RTCDataChannelState.Closed }:
                case { ReadyState: RTCDataChannelState.Closing }:
                    return;
            }

            // 評価用
            if (MistStats.I != null)
            {
                MistStats.I.TotalSendBytes += data.Length;
                MistStats.I.TotalMessengeCount++;
            }

            _dataChannel.Send(data);
        }

        public void Close()
        {
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

        private void OnOpenDataChannel()
        {
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
            MistDebug.Log($"[MistPeer][OnTrack] {Id}");

            _outputAudioSource.SetTrack(track);
            _outputAudioSource.loop = true;
            _outputAudioSource.Play();
        }

        public void AddInputAudioSource(AudioSource audioSource)
        {
            if (audioSource == null) return;
            MistDebug.Log($"[MistPeer][AddTrack] {Id}");
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
            CreateOffer().Forget();
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

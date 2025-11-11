using System;
using MemoryPack;
using Unity.WebRTC;

namespace MistNet.Minimal
{
    public class TransportLayerTest : ITransportLayer
    {
        private readonly IPeerRepository _peerRepository;
        private MistSignalingWebRTC _mistSignalingWebRtc;

        private Action<NodeId> _onConnectedAction;
        private Action<NodeId> _onDisconnectedAction;
        private Action<MistMessage, NodeId> _onMessageAction;

        public TransportLayerTest(IPeerRepository peerRepository)
        {
            _peerRepository = peerRepository;
        }

        public void Dispose()
        {
            _mistSignalingWebRtc?.Dispose();
        }

        public void Init()
        {
            _mistSignalingWebRtc = new MistSignalingWebRTC(_peerRepository);
        }

        public void Connect(NodeId id)
        {
            if (id == _peerRepository.SelfId) return;
            _mistSignalingWebRtc.Connect(id);
        }

        public void Disconnect(NodeId id)
        {
            if (id == _peerRepository.SelfId) return;
            OnDisconnected(id);
        }

        public void DisconnectAll()
        {
            var nodeDict = _peerRepository.PeerDict;
            foreach (var nodeId in nodeDict.Keys)
            {
                if (!IsConnectingOrConnected(nodeId)) continue;
                Disconnect(nodeId);
            }
        }

        public void Send(NodeId targetId, MistMessage data, bool isForward = false)
        {
            if (!IsConnected(targetId)) return;

            if (!isForward)
            {
                data.HopCount = OptConfig.Data.HopCount;
            }

            var bytes = MemoryPackSerializer.Serialize(data);
            var peerData = _peerRepository.PeerDict[targetId];
            peerData.PeerEntity.Send(bytes);
        }

        public void AddConnectCallback(Delegate callback)
        {
            _onConnectedAction += (Action<NodeId>)callback;
        }

        public void AddDisconnectCallback(Delegate callback)
        {
            _onDisconnectedAction += (Action<NodeId>)callback;
        }

        public void RegisterReceive(Action<MistMessage, NodeId> callback)
        {
            _onMessageAction += callback;
        }

        public void OnConnected(NodeId id)
        {
            MistLogger.Info($"[Connected] {id}");
            _onConnectedAction?.Invoke(id);
        }

        public void OnDisconnected(NodeId id)
        {
            MistLogger.Info($"[Disconnected] {id}");
            _peerRepository.RemovePeer(id);
            _onDisconnectedAction?.Invoke(id);
        }

        public void OnMessage(byte[] data, NodeId senderId)
        {
            var message = MemoryPackSerializer.Deserialize<MistMessage>(data);
            message.HopCount--;
            MistLogger.Trace($"[RECV][{message.Type.ToString()}] {message.Id} -> {message.TargetId}");
            _onMessageAction?.Invoke(message, senderId);
        }

        public bool IsConnectingOrConnected(NodeId id)
        {
            if (!_peerRepository.PeerDict.TryGetValue(id, out var peerData)) return false;
            if (peerData.PeerEntity == null) return false;
            if (peerData.PeerEntity.RtcPeer == null) return false;

            return peerData.PeerEntity.RtcPeer.ConnectionState is RTCPeerConnectionState.Connected
                or RTCPeerConnectionState.Connecting;
        }

        public bool IsConnected(NodeId id)
        {
            return _peerRepository.PeerDict.TryGetValue(id, out var data) && data.IsConnected;
        }
    }
}

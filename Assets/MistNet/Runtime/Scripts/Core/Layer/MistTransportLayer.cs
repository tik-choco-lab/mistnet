using System;
using System.Linq;
using MemoryPack;
using Unity.WebRTC;

namespace MistNet
{
    public class MistTransportLayer : ITransportLayer
    {
        private Action<NodeId> _onConnectedAction;
        private Action<NodeId> _onDisconnectedAction;
        private Action<MistMessage, NodeId> _onMessageAction;
        private Action<byte[], NodeId> _onLocationMessageAction;
        private MistSignalingWebRTC _mistSignalingWebRtc;
        private readonly Selector _selector;
        private readonly IPeerRepository _peerRepository;
        private readonly ILayer _layer;

        public MistTransportLayer(Selector selector, IPeerRepository peerRepository, ILayer layer)
        {
            _selector = selector;
            _peerRepository = peerRepository;
            _layer = layer;
        }

        public void Dispose()
        {
            _mistSignalingWebRtc?.Dispose();
        }

        public void Init()
        {
            _mistSignalingWebRtc = new MistSignalingWebRTC
            (
                _peerRepository,
                _layer.World.RegisterReceive,
                _layer.World.Send
            );
        }

        public void Connect(NodeId id)
        {
            if (id == _peerRepository.SelfId) return;
            if (IsConnectingOrConnected(id)) return;
            _mistSignalingWebRtc.Connect(id);
        }

        public void Disconnect(NodeId id)
        {
            if (id == _peerRepository.SelfId) return;

            _selector.RoutingBase.RemoveMessageNode(id);
            _selector.RoutingBase.Remove(id);
            OnDisconnected(id);
        }

        public void DisconnectAll()
        {
            MistLogger.Info("[DisconnectAll] All peers will be disconnected.");
            var peerIds = _selector.RoutingBase.ConnectedNodes.ToArray();
            foreach (var peerId in peerIds)
            {
                Disconnect(peerId);
            }
        }

        public void Send(NodeId targetId, MistMessage data, bool isForward = false)
        {
            if (!IsConnected(targetId))
            {
                OnDisconnected(targetId);
                return;
            }

            if (!isForward)
            {
                data.HopCount = OptConfig.Data.HopCount;
            }

            var bytes = MemoryPackSerializer.Serialize(data);
            var peerData = _peerRepository.PeerDict[targetId];
            peerData.PeerEntity.Send(bytes);
        }
        
        /// <summary>
        /// 位置同期専用の高速チャンネルで送信（unreliable、順序保証なし）
        /// </summary>
        public void SendLocation(NodeId targetId, byte[] data)
        {
            if (!IsConnected(targetId))
            {
                return;
            }

            var peerData = _peerRepository.PeerDict[targetId];
            peerData.PeerEntity.SendLocation(data);
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
        
        public void RegisterLocationReceive(Action<byte[], NodeId> callback)
        {
            _onLocationMessageAction += callback;
        }

        public void OnConnected(NodeId id)
        {
            MistLogger.Info($"[Connected] {id}");
            _selector.SelectorBase.OnConnected(id);
            _onConnectedAction?.Invoke(id);
            _selector.RoutingBase.OnConnected(id);
        }

        public void OnDisconnected(NodeId id)
        {
            MistLogger.Info($"[Disconnected] {id}");
            MistSyncManager.I.RemoveObject(id);
            _selector.SelectorBase.OnDisconnected(id);
            _peerRepository.RemovePeer(id);
            _onDisconnectedAction?.Invoke(id);
            _selector.RoutingBase.OnDisconnected(id);
        }

        public void OnMessage(byte[] data, NodeId senderId)
        {
            var message = MemoryPackSerializer.Deserialize<MistMessage>(data);
            message.HopCount--;
            MistLogger.Trace($"[RECV][{message.Type.ToString()}] {message.Id} -> {message.TargetId}");
            _onMessageAction?.Invoke(message, senderId);
        }
        
        public void OnLocationMessage(byte[] data, NodeId senderId)
        {
            _onLocationMessageAction?.Invoke(data, senderId);
        }

        public bool IsConnectingOrConnected(NodeId id)
        {
            if (!_peerRepository.PeerDict.TryGetValue(id, out var peerData)) return false;
            if (peerData.PeerEntity == null) return false;
            if (peerData.PeerEntity.RtcPeer == null) return false;

            var state = peerData.PeerEntity.RtcPeer.ConnectionState;
            return state is RTCPeerConnectionState.Connected
                or RTCPeerConnectionState.Connecting
                or RTCPeerConnectionState.New;
        }

        public bool IsConnected(NodeId id)
        {
            return _peerRepository.PeerDict.TryGetValue(id, out var data) && data.IsConnected;
        }
    }
}

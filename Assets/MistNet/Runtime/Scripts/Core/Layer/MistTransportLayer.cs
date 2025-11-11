using System;
using System.Linq;
using MemoryPack;

namespace MistNet
{
    public class MistTransportLayer : ITransportLayer
    {
        private Action<NodeId> _onConnectedAction;
        private Action<NodeId> _onDisconnectedAction;
        private Action<MistMessage, NodeId> _onMessageAction;
        private MistSignalingWebRTC _mistSignalingWebRtc;
        private readonly Selector _selector;
        private readonly IPeerRepository _peerRepository;

        public MistTransportLayer(Selector selector, IPeerRepository peerRepository)
        {
            _selector = selector;
            _peerRepository = peerRepository;
        }

        public void Init()
        {
            _mistSignalingWebRtc = new MistSignalingWebRTC(_peerRepository);
        }

        public void Connect(NodeId id)
        {
            if (id == PeerRepository.I.SelfId) return;

            _mistSignalingWebRtc.Connect(id);
        }

        public void Disconnect(NodeId id)
        {
            if (id == PeerRepository.I.SelfId) return;

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
            PeerRepository.I.OnDisconnected(id);
            _onDisconnectedAction?.Invoke(id);
            _selector.RoutingBase.OnDisconnected(id);
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

        public void OnMessage(byte[] data, NodeId senderId)
        {
            var message = MemoryPackSerializer.Deserialize<MistMessage>(data);
            message.HopCount--;
            MistLogger.Trace($"[RECV][{message.Type.ToString()}] {message.Id} -> {message.TargetId}");
            _onMessageAction?.Invoke(message, senderId);
        }

        public void Send(NodeId targetId, MistMessage data, bool isForward = false)
        {
            if (!PeerRepository.I.IsConnected(targetId)) return;

            if (!isForward)
            {
                data.HopCount = OptConfig.Data.HopCount;
            }

            var bytes = MemoryPackSerializer.Serialize(data);
            var peerData = PeerRepository.I.GetAllPeer[targetId];
            peerData.PeerEntity.Send(bytes);
        }

        public void Dispose()
        {
            _mistSignalingWebRtc?.Dispose();
        }
    }
}

using System;
using System.Linq;

namespace MistNet
{
    public class MistTransportLayer : ITransportLayer
    {
        private Action<NodeId> _onConnectedAction;
        private Action<NodeId> _onDisconnectedAction;
        private readonly MistSignalingWebRTC _mistSignalingWebRtc;
        private readonly Selector _selector;

        public MistTransportLayer(Selector selector)
        {
            _selector = selector;
            _mistSignalingWebRtc = new MistSignalingWebRTC();
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

        public void Dispose()
        {
            _mistSignalingWebRtc?.Dispose();
        }
    }
}

using System;
using System.Collections.Generic;
using MemoryPack;
using Unity.WebRTC;

namespace MistNet
{
    public class MistWorldLayer : IWorldLayer
    {
        private readonly ITransportLayer _transport;
        private Action<NodeId> _sendFailed;
        private readonly Dictionary<MistNetMessageType, MessageReceivedHandler> _onMessageDict = new();
        private readonly Selector _selector;

        public MistWorldLayer(ITransportLayer transport, Selector selector)
        {
            _selector = selector;
            _transport = transport;
        }

        public void Send(MistNetMessageType type, byte[] data, NodeId targetId)
        {
            var message = new MistMessage
            {
                Id = PeerRepository.I.SelfId,
                Payload = data,
                TargetId = targetId,
                Type = type,
            };
            var sendData = MemoryPackSerializer.Serialize(message);

            if (!PeerRepository.I.IsConnected(targetId))
            {
                targetId = _selector.RoutingBase.Get(targetId);
                if (targetId == null)
                {
                    MistLogger.Warning($"[Error] No route to {message.TargetId}");
                    _sendFailed?.Invoke(new NodeId(message.TargetId));
                    return; // メッセージの破棄
                }

                MistLogger.Trace($"[FORWARD] {targetId} {type} {message.TargetId}");
            }

            if (PeerRepository.I.IsConnected(targetId))
            {
                MistLogger.Trace($"[SEND][{type.ToString()}] {type} {targetId}");
                var peerData = PeerRepository.I.GetAllPeer[targetId];
                peerData.PeerEntity.Send(sendData);
            }
        }

        public void SendAll(MistNetMessageType type, byte[] data)
        {
            var message = new MistMessage
            {
                Id = PeerRepository.I.SelfId,
                Payload = data,
                Type = type,
            };

            foreach (var peerId in _selector.RoutingBase.ConnectedNodes)
            {
                MistLogger.Trace($"[SEND][{peerId}] {type.ToString()}");
                message.TargetId = peerId;
                var sendData = MemoryPackSerializer.Serialize(message);
                var peerEntity = PeerRepository.I.GetPeer(peerId);
                peerEntity?.Send(sendData);
            }
        }

        public void OnMessage(byte[] data, NodeId senderId)
        {
            var message = MemoryPackSerializer.Deserialize<MistMessage>(data);
            MistLogger.Trace($"[RECV][{message.Type.ToString()}] {message.Id} -> {message.TargetId}");

            if (IsMessageForSelf(message))
            {
                // 自身宛のメッセージの場合
                ProcessMessageForSelf(message, senderId);
                return;
            }

            // 他のPeer宛のメッセージの場合
            var targetId = new NodeId(message.TargetId);
            if (!PeerRepository.I.IsConnected(targetId))
            {
                targetId = _selector.RoutingBase.Get(targetId);
            }

            if (!string.IsNullOrEmpty(targetId))
            {
                var peer = PeerRepository.I.GetPeer(targetId);
                if (peer == null
                    || peer.RtcPeer == null
                    || peer.RtcPeer.ConnectionState != RTCPeerConnectionState.Connected
                    || peer.Id == PeerRepository.I.SelfId
                    || peer.Id == senderId)
                {
                    MistLogger.Warning($"[Error] Peer is null {targetId}");
                    return;
                }

                peer.Send(data);
                MistLogger.Trace(
                    $"[RECV][SEND][FORWARD][{message.Type.ToString()}] {message.Id} -> {PeerRepository.I.SelfId} -> {peer.Id}");
            }
        }

        public void RegisterReceive(MistNetMessageType type, MessageReceivedHandler callback)
        {
            if (_onMessageDict.TryAdd(type, callback)) return;
            MistLogger.Warning($"[Warning] Duplicate OnMessage callback for type {type}");
        }

        public void AddSendFailedCallback(Delegate callback)
        {
            _sendFailed += (Action<NodeId>)callback;
        }

        private bool IsMessageForSelf(MistMessage message)
        {
            return message.TargetId == PeerRepository.I.SelfId;
        }

        private void ProcessMessageForSelf(MistMessage message, NodeId senderId)
        {
            _selector.RoutingBase.AddRouting(new NodeId(message.Id), senderId);
            _onMessageDict[message.Type](message.Payload, new NodeId(message.Id));
        }

        public void Dispose()
        {
            _onMessageDict.Clear();
            _sendFailed = null;
        }
    }
}

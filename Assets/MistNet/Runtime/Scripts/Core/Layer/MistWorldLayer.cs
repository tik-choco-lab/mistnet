using System;
using System.Collections.Generic;

namespace MistNet
{
    public class MistWorldLayer : IWorldLayer
    {
        private readonly ITransportLayer _transport;
        private Action<NodeId> _sendFailed;
        private readonly Dictionary<MistNetMessageType, MessageReceivedHandler> _onMessageDict = new();
        private readonly Selector _selector;
        private MistMessage _message;

        public MistWorldLayer(ITransportLayer transport, Selector selector)
        {
            _selector = selector;
            _transport = transport;
            _transport.RegisterReceive(OnMessage);
        }

        public void Send(MistNetMessageType type, byte[] data, NodeId targetId)
        {
            _message ??= new MistMessage();
            _message.Id = PeerRepository.I.SelfId;
            _message.Payload = data;
            _message.Type = type;

            if (!PeerRepository.I.IsConnected(targetId))
            {
                targetId = _selector.RoutingBase.Get(targetId);
                if (targetId == null)
                {
                    MistLogger.Warning($"[Error] No route to {_message.TargetId}");
                    _sendFailed?.Invoke(new NodeId(_message.TargetId));
                    return; // メッセージの破棄
                }

                MistLogger.Trace($"[FORWARD] {targetId} {type} {_message.TargetId}");
            }

            if (PeerRepository.I.IsConnected(targetId))
            {
                MistLogger.Trace($"[SEND][{type.ToString()}] {type} {targetId}");
                _transport.Send(targetId, _message);
            }
        }

        public void SendAll(MistNetMessageType type, byte[] data)
        {
            _message ??= new MistMessage();
            _message.Id = PeerRepository.I.SelfId;
            _message.Payload = data;
            _message.Type = type;

            foreach (var peerId in _selector.RoutingBase.ConnectedNodes)
            {
                MistLogger.Trace($"[SEND][{peerId}] {type.ToString()}");
                _message.TargetId = peerId;
                _transport.Send(peerId, _message);
            }
        }

        private void OnMessage(byte[] raw, MistMessage message, NodeId senderId)
        {
            if (IsMessageForSelf(message))
            {
                // 自身宛のメッセージの場合
                ProcessMessageForSelf(message, senderId);
                return;
            }

            // 他のPeer宛のメッセージの場合
            var targetId = new NodeId(message.TargetId);
            targetId = PeerRepository.I.IsConnected(targetId) ? targetId : _selector.RoutingBase.Get(targetId);
            if (string.IsNullOrEmpty(targetId)) return;
            _transport.Send(targetId, raw);
            MistLogger.Trace($"[FORWARD][{message.Type.ToString()}] {message.Id} -> {PeerRepository.I.SelfId} -> {targetId}");
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

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

        public MistWorldLayer(ITransportLayer transport, Selector selector)
        {
            _selector = selector;
            _transport = transport;
            _transport.RegisterReceive(OnMessage);
        }

        public void Send(MistNetMessageType type, byte[] data, NodeId targetId)
        {
            // NOTE: messageを共有すると予期しない問題が発生するので　毎回newしている
            var message = new MistMessage
            {
                Id = PeerRepository.I.SelfId,
                Payload = data,
                Type = type,
                TargetId = targetId,
            };

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
                _transport.Send(targetId, message);
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
                _transport.Send(peerId, message, isForward:false);
            }
        }

        private void OnMessage(MistMessage message, NodeId senderId)
        {
            if (message.Id == PeerRepository.I.SelfId)
            {
                // 自分からのメッセージは破棄 loopを防ぐ
                MistLogger.Warning($"[Warning] Loop message from self {message.Type}");
                return;
            }

            if (IsMessageForSelf(message))
            {
                // 自身宛のメッセージの場合
                ProcessMessageForSelf(message, senderId);
                return;
            }

            // 他のPeer宛のメッセージの場合
            if (message.HopCount <= 0) return;

            var targetId = new NodeId(message.TargetId);
            targetId = PeerRepository.I.IsConnected(targetId) ? targetId : _selector.RoutingBase.Get(targetId);
            if (string.IsNullOrEmpty(targetId)) return;
            if (targetId == message.Id) return; // 送り元に送り返すのは無限ループになるので破棄

            _transport.Send(targetId, message, isForward:true);
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

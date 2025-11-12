using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MemoryPack;
using MistNet.Utils;
using UnityEngine;

namespace MistNet.Minimal
{
    public class Node : MonoBehaviour
    {
        private MistSignalingWebSocket _mistSignalingWebSocket;
        private PeerRepository _peerRepository;
        private ITransportLayer Transport { get; set; }
        private readonly HashSet<NodeId> _connectingOrConnectedNodes = new();
        private readonly Dictionary<MistNetMessageType, MessageReceivedHandler> _onMessageDict = new();
        private readonly Dictionary<NodeId, NodeId> _routingTable = new();

        private void Start()
        {
            var selfId = Guid.NewGuid().ToString("N").Substring(0, 4);
            _peerRepository = new PeerRepository();
            Transport = new TransportLayerTest(_peerRepository, RegisterReceive, Send);
            Transport.AddConnectCallback((Action<NodeId>)OnConnected);
            _peerRepository.Init(Transport, new NodeId(selfId));
            _mistSignalingWebSocket = new MistSignalingWebSocket(_peerRepository);
            Transport.RegisterReceive(OnMessage);
            gameObject.name = $"Node_{selfId}";

            RegisterReceive(MistNetMessageType.ConnectionSelector, OnConnectionSelector);
        }

        private void OnDestroy()
        {
            Transport.Dispose();
            _mistSignalingWebSocket.Dispose();
        }

        private void RegisterReceive(MistNetMessageType type, MessageReceivedHandler callback)
        {
            _onMessageDict[type] = callback;
        }

        public void Signaling()
        {
            _mistSignalingWebSocket.Init().Forget();
        }

        private void SendAll(string data)
        {
            var allNode = _peerRepository.PeerDict.Keys;
            var payload = CreateData(data);
            foreach (var nodeId in allNode)
            {
                Send(MistNetMessageType.ConnectionSelector, payload, nodeId);
            }
        }

        private void Send(MistNetMessageType type, byte[] data, NodeId targetId)
        {
            var message = new MistMessage
            {
                Id = _peerRepository.SelfId,
                Payload = data,
                Type = type,
                TargetId = targetId,
            };

            var sendId = targetId;
            if (!_peerRepository.PeerDict.ContainsKey(targetId))
            {
                // 経路に基づいて転送
                if (!_routingTable.TryGetValue(targetId, out var nextHopId)) return;
                sendId = nextHopId;
            }

            Transport.Send(sendId, message);
        }

        private void OnConnected(NodeId id)
        {
            MistLogger.Debug($"[ConnectionSelector] {gameObject.name}: {string.Join(", ", _peerRepository.PeerDict.Keys)}");
            if (!_connectingOrConnectedNodes.Add(id)) return;

            // await UniTask.Yield();

            var dataStr = string.Join(",", _connectingOrConnectedNodes);
            SendAll(dataStr);
        }

        private void OnMessage(MistMessage message, NodeId senderId)
        {
            _routingTable[new NodeId(message.Id)] = senderId;

            if (message.TargetId != _peerRepository.SelfId)
            {
                if (message.HopCount <= 0)
                {
                    MistLogger.Error($"[ConnectionSelector] {gameObject.name} HopCount exceeded");
                    return;
                }
                // 経路に基づいて転送
                if (!_routingTable.TryGetValue(new NodeId(message.TargetId), out var nextHopId)) return;
                message.HopCount--;
                Transport.Send(nextHopId, message, isForward: true);
                return;
            }
            _onMessageDict[message.Type](message.Payload, senderId);
        }

        private void OnMessage(string data, NodeId senderId)
        {
            var nodes = data.Split(',');
            MistLogger.Debug($"[ConnectionSelector] ({nodes.Length}) Nodes: {data}");

            foreach (var nodeIdStr in nodes)
            {
                var nodeId = new NodeId(nodeIdStr);
                if (nodeId == _peerRepository.SelfId) continue;
                if (!_connectingOrConnectedNodes.Add(nodeId)) continue;
                _routingTable[nodeId] = senderId;

                // idの大きさを比較
                if (IdUtil.CompareId(_peerRepository.SelfId, nodeId))
                {
                    MistLogger.Debug($"[ConnectionSelector] {gameObject.name}: Connecting {nodeId}");
                    Transport.Connect(nodeId);
                }
            }
        }

        private void OnConnectionSelector(byte[] data, NodeId id)
        {
            if (MistStats.I != null)
            {
                MistStats.I.TotalEvalReceiveBytes += data.Length;
            }

            var message = MemoryPackSerializer.Deserialize<P_ConnectionSelector>(data);
            OnMessage(message.Data, id);
        }

        private static byte[] CreateData(string data)
        {
            var message = new P_ConnectionSelector
            {
                Data = data
            };

            return MemoryPackSerializer.Serialize(message);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
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
        private string _selfId;

        private void Start()
        {
            _selfId = Guid.NewGuid().ToString("N").Substring(0, 4);
            _peerRepository = new PeerRepository();
            Transport = new TransportLayerTest(_peerRepository, RegisterReceive, Send);
            Transport.AddConnectCallback((Action<NodeId>)OnConnected);
            _peerRepository.Init(Transport, new NodeId(_selfId));
            _mistSignalingWebSocket = new MistSignalingWebSocket(_peerRepository);
            Transport.RegisterReceive(OnMessage);
            gameObject.name = $"Node_{_selfId}";

            RegisterReceive(MistNetMessageType.ConnectionSelector, OnConnectionSelector);
        }

        private void OnDestroy()
        {
            Transport.Dispose();
            _mistSignalingWebSocket.Dispose();
        }

        private const float UpdateInterval = 5f;
        private float _timer;
        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < UpdateInterval) return;
            _timer = 0f;
            MistLogger.Trace($"[Test][Connection] {_selfId}: {string.Join(",", _peerRepository.PeerDict.Keys.Where(id => Transport.IsConnected(id)))}");
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
            if (!Transport.IsConnected(targetId))
            {
                // 経路に基づいて転送
                if (!_routingTable.TryGetValue(targetId, out var nextHopId)) return;
                sendId = nextHopId;
            }

            MistLogger.Trace($"[Test][Send] {_selfId}: {targetId} {sendId}");
            Transport.Send(sendId, message);
        }

        private async void OnConnected(NodeId id)
        {
            MistLogger.Debug($"[Test][Connected] {_selfId}: {id}");
            if (!_connectingOrConnectedNodes.Add(id)) return;

            await UniTask.DelayFrame(1);

            var dataStr = string.Join(",", _connectingOrConnectedNodes);
            SendAll(dataStr);
        }

        private void OnMessage(MistMessage message, NodeId senderId)
        {
            MistLogger.Debug($"[Test][Receive] {_selfId}: {senderId} {message.Id} {message.TargetId}");
            AddRouting(new NodeId(message.Id), senderId);

            if (message.TargetId != _peerRepository.SelfId)
            {
                if (message.HopCount <= 0)
                {
                    MistLogger.Warning($"[Test][HopCount] {gameObject.name} HopCount exceeded");
                    return;
                }
                // 経路に基づいて転送
                var targetId = new NodeId(message.TargetId);
                var nextHopId = targetId;
                if (!Transport.IsConnected(targetId))
                {
                    if (!_routingTable.TryGetValue(new NodeId(message.TargetId), out nextHopId)) return;
                }
                message.HopCount--;
                MistLogger.Trace($"[Test][Send][Forward] {_selfId}: {message.TargetId} {nextHopId}");
                Transport.Send(nextHopId, message, isForward: true);
                return;
            }
            _onMessageDict[message.Type](message.Payload, senderId);
        }

        private void OnMessage(string data, NodeId senderId)
        {
            var nodes = data.Split(',');
            MistLogger.Debug($"[Test][Receive] {_selfId}: {senderId} {data}");

            foreach (var nodeIdStr in nodes)
            {
                var nodeId = new NodeId(nodeIdStr);
                if (nodeId == _peerRepository.SelfId) continue;
                if (!_connectingOrConnectedNodes.Add(nodeId)) continue;
                AddRouting(nodeId, senderId);

                // idの大きさを比較
                if (IdUtil.CompareId(_peerRepository.SelfId, nodeId))
                {
                    Connect(nodeId).Forget();
                }
            }
        }

        private async UniTask Connect(NodeId nodeId)
        {
            await UniTask.Yield();
            MistLogger.Debug($"[ConnectionSelector] {gameObject.name}: Connecting {nodeId}");
            Transport.Connect(nodeId);
        }

        private void AddRouting(NodeId sourceId, NodeId viaId)
        {
            if (Transport.IsConnected(sourceId))
            {
                _routingTable.Remove(sourceId);
                return;
            }
            if (sourceId == _peerRepository.SelfId) return;
            if (sourceId == viaId) return;
            MistLogger.Trace($"[Test][Routing] {_selfId}: {sourceId} - {viaId}");
            _routingTable[sourceId] = viaId;
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

using System.Collections.Generic;
using MemoryPack;
using MistNet.Evaluation;
using Newtonsoft.Json;

namespace MistNet.Runtime.Evaluation
{
    public class NetworkPartitionCheck
    {
        private const int MaxQueueSize = 100; // 固定サイズ
        private P_Gossip _message;
        private readonly Queue<string> _receivedMessages = new();
        private EvalMessage _evalMessage;
        private readonly IEvalMessageSender _sender;
        private readonly IPeerRepository _peerRepository;

        public NetworkPartitionCheck(IEvalMessageSender sender, IPeerRepository peerRepository)
        {
            sender.RegisterReceive(EvalMessageType.NetworkPartitionCheck, OnNetworkPartitionCheck);
            _sender = sender;
            _peerRepository = peerRepository;
            MistManager.I.World.RegisterReceive(MistNetMessageType.Gossip, OnGossipReceived);
        }

        private void OnNetworkPartitionCheck(string payload)
        {
            MistLogger.Info("[Eval] Network partition check received.");

            var data = JsonConvert.DeserializeObject<NetworkPartitionCheckData>(payload);

            _message ??= new P_Gossip();
            _message.Payload = data.Id;
            var bytes = MemoryPackSerializer.Serialize(_message);

            _receivedMessages.Enqueue(_message.Payload);
            SendToEvalServer(_message.Payload);
            MistManager.I.World.SendAll(MistNetMessageType.Gossip, bytes);
        }

        private void OnGossipReceived(byte[] data, NodeId id)
        {
            var message = MemoryPackSerializer.Deserialize<P_Gossip>(data);
            if (message == null)
            {
                MistLogger.Error("[Eval] Failed to deserialize P_Gossip message.");
                return;
            }

            if (_receivedMessages.Contains(message.Payload))
            {
                MistLogger.Info("[Eval] Duplicate message received, ignoring.");
                return;
            }

            MistLogger.Info($"[Eval] Gossip message received: {message.Payload}");

            _receivedMessages.Enqueue(message.Payload);

            if (_receivedMessages.Count > MaxQueueSize)
                _receivedMessages.Dequeue(); // 古いものから削除

            MistManager.I.World.SendAll(MistNetMessageType.Gossip, data);
            SendToEvalServer(message.Payload);
        }

        private void SendToEvalServer(string payload)
        {
            var response = new NetworkPartitionCheckResponse
            {
                Id = payload,
                NodeId = _peerRepository.SelfId.ToString()
            };
            _sender.Send(EvalMessageType.NetworkPartitionCheckResponse, response);
        }

        public class NetworkPartitionCheckResponse
        {
            [JsonProperty("id")] public string Id;
            [JsonProperty("nodeId")] public string NodeId;
        }
    }
}

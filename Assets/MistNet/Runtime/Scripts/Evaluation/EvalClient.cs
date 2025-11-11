using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MistNet.Runtime.Evaluation;
using MistNet.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet.Evaluation
{
    public class EvalClient : MonoBehaviour, IEvalMessageSender
    {
        private WebSocketHandler _webSocketHandler;
        private NodeState _nodeStateData;
        private static MistEvalConfigData Data => EvalConfig.Data;
        private readonly Dictionary<EvalMessageType, EvalMessageReceivedHandler> _onMessageFunc = new();
        private MistEventLogger _logger;

        private async void Start()
        {
            EvalConfig.ReadConfig();
            _logger = new(Data.EnableEventLog);
            MistEventLogger.I.LogEvent(EventType.GameStarted);

            _webSocketHandler = new WebSocketHandler(url: Data.ServerUrl);
            _webSocketHandler.OnMessage += OnMessage;
            await _webSocketHandler.ConnectAsync();
            _ = new NetworkPartitionCheck(this, MistManager.I.PeerRepository);

            var nodeSettings = new EvalNodeSettings
            {
                NodeId = MistConfig.Data.NodeId,
                ConfigData = OptConfig.Data,
            };

            Send(EvalMessageType.NodeSettings, nodeSettings);
            UpdateSendNodeState(this.GetCancellationTokenOnDestroy()).Forget();
        }

        public void RegisterReceive(EvalMessageType type, EvalMessageReceivedHandler receiver)
        {
            if (_onMessageFunc.TryAdd(type, receiver)) return;
        }

        private NodeState GetNodeStateData()
        {
            return new NodeState
            {
                Node = NodeUtils.GetSelfNodeData(),
                Nodes = NodeUtils.GetOtherNodeData()
            };
        }

        public void Send(EvalMessageType type, object payload)
        {
            var sendData = new EvalMessage
            {
                Type = type,
                Payload = payload
            };

            _webSocketHandler.Send(JsonConvert.SerializeObject(sendData));
        }

        private async UniTask UpdateSendNodeState(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(EvalConfig.Data.SendStateIntervalSeconds), cancellationToken: token);
                SendNodeState();
            }
        }

        private void SendNodeState()
        {
            _nodeStateData ??= GetNodeStateData();
            _nodeStateData.Node = NodeUtils.GetSelfNodeData();
            _nodeStateData.Nodes = NodeUtils.GetOtherNodeData();
            _nodeStateData.Stats ??= MistStats.I.StatData;
            Send(EvalMessageType.NodeState, _nodeStateData);
        }

        private void OnDestroy()
        {
            _webSocketHandler.OnMessage -= OnMessage;
            _webSocketHandler?.Dispose();
            _logger?.Dispose();
        }

        private void OnMessage(string message)
        {
            Debug.Log($"Received message: {message}");
            var data = JsonConvert.DeserializeObject<EvalMessage>(message);
            _onMessageFunc.TryGetValue(data.Type, out var func);
            if (func == null)
            {
                MistLogger.Warning($"No handler for message type: {data.Type}");
                return;
            }

            func(data.Payload.ToString());
        }
    }
}

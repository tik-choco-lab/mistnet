using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MistNet.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet.Evaluation
{
    public class EvalClient : MonoBehaviour
    {
        private WebSocketHandler _webSocketHandler;
        private NodeState _nodeStateData;
        private static MistEvalConfigData Data => EvalConfig.Data;
        private readonly Dictionary<EvalMessageType, Action<string>> _onMessageFunc = new();

        private async void Start()
        {
            EvalConfig.ReadConfig();
            MistEventLogger logger = new(Data.EnableEventLog);
            MistEventLogger.I.LogEvent(EventType.GameStarted);

            _webSocketHandler = new WebSocketHandler(url: Data.ServerUrl);
            _webSocketHandler.OnMessage += OnMessage;
            await _webSocketHandler.ConnectAsync();

            var nodeSettings = new EvalNodeSettings
            {
                NodeId = MistConfig.Data.NodeId,
                ConfigData = OptConfig.Data,
            };

            Send(EvalMessageType.NodeSettings, nodeSettings);
            UpdateSendNodeState(this.GetCancellationTokenOnDestroy()).Forget();

        }

        public void RegisterMessageHandler(EvalMessageType type, Action<string> func)
        {
            if (_onMessageFunc.TryAdd(type, func)) return;
            MistLogger.Warning($"[EvalClient] Message handler already registered for type: {type}");
        }

        private NodeState GetNodeStateData()
        {
            return new NodeState
            {
                Node = NodeUtils.GetSelfNodeData(),
                Nodes = NodeUtils.GetOtherNodeData()
            };
        }

        private void Send(EvalMessageType type, object payload)
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
                await UniTask.Delay(TimeSpan.FromSeconds(3.0f), cancellationToken: token);
                SendNodeState();
            }
        }

        private void SendNodeState()
        {
            _nodeStateData ??= GetNodeStateData();
            _nodeStateData.Node = NodeUtils.GetSelfNodeData();
            _nodeStateData.Nodes = NodeUtils.GetOtherNodeData();
            Send(EvalMessageType.NodeState, _nodeStateData);
        }

        private void OnDestroy()
        {
            _webSocketHandler.OnMessage -= OnMessage;
            _webSocketHandler?.Dispose();
            EvalConfig.WriteConfig();
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

            func.Invoke(data.Payload == null ? string.Empty : data.Payload.ToString());
        }
    }
}

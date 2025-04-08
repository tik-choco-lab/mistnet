using System;
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
        private MistEvalConfigData Data => EvalConfig.Data;
        [SerializeField] private IRouting routing;

        private async void Start()
        {
            EvalConfig.ReadConfig();
            _webSocketHandler = new WebSocketHandler(url: Data.ServerUrl);
            _webSocketHandler.OnMessageReceived += OnMessage;
            await _webSocketHandler.ConnectAsync();

            var nodeSettings = new EvalNodeSettings
            {
                NodeId = MistConfig.Data.NodeId,
                Config = OptConfigLoader.Data,
            };
            Send(EvalMessageType.NodeSettings, nodeSettings);
            UpdateSendNodeState(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void Send(EvalMessageType type, object payload)
        {
            var sendData = new EvalMessage
            {
                Type = type,
                Payload = JsonConvert.SerializeObject(payload)
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
            var nodes = NodeUtils.GetAllNodeData();
            Send(EvalMessageType.AllNodeStates, nodes);
        }

        private void OnDestroy()
        {
            _webSocketHandler.OnMessageReceived -= OnMessage;
            _webSocketHandler?.Dispose();
        }

        private void OnMessage(string message)
        {
            Debug.Log($"Received message: {message}");
        }
    }
}

using Newtonsoft.Json;
using UnityEngine;

namespace MistNet.Evaluation
{
    public class EvalClient : MonoBehaviour
    {
        private WebSocketHandler _webSocketHandler;
        private MistEvalConfigData Data => EvalConfig.Data;

        private void Start()
        {
            EvalConfig.ReadConfig();
            _webSocketHandler.OnMessageReceived += OnMessage;
            _webSocketHandler = new WebSocketHandler(url: Data.ServerUrl);
        }

        private void OnDestroy()
        {
            _webSocketHandler.OnMessageReceived -= OnMessage;
            _webSocketHandler?.Dispose();
        }

        private void OnMessage(string message)
        {
            Debug.Log($"Received message: {message}");
            // Json
            var msg = JsonConvert.DeserializeObject<MessageInfo>(message);
        }
    }
}

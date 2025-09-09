using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using WebSocketSharp;

namespace MistNet.Evaluation
{
    public class WebSocketHandler : IDisposable
    {
        private readonly WebSocket _webSocket;
        private readonly SynchronizationContext _syncContext;
        public Action<string> OnMessageReceived;

        public WebSocketHandler(string url)
        {
            _syncContext = SynchronizationContext.Current;
            _webSocket = new WebSocket(url);
            _webSocket.OnMessage += OnMessage;
            _webSocket.OnError += OnError;
            _webSocket.OnOpen += OnOpen;
            _webSocket.OnClose += OnClose;
        }

        private void OnClose(object sender, CloseEventArgs e)
        {
            MistLogger.Info($"[WebSocket] Closed: {e.Reason}");
        }

        private void OnOpen(object sender, EventArgs e)
        {
            MistLogger.Info("[WebSocket] Opened");
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            MistLogger.Error($"[WebSocket] Error: {e.Message}");
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            MistLogger.Trace($"[WebSocket] Received: {e.Data}");
            _syncContext.Post(_ => OnMessageReceived?.Invoke(e.Data), null);
        }

        public async UniTask ConnectAsync()
        {
            _webSocket.ConnectAsync();
            await UniTask.WaitUntil(() => _webSocket.ReadyState == WebSocketState.Open);
        }

        public void Send(string message)
        {
            if (_webSocket.ReadyState == WebSocketState.Open)
            {
                MistLogger.Trace($"[WebSocket] Sending: {message}");
                _webSocket.Send(message);
            }
            else
            {
                MistLogger.Warning("WebSocket is not open.");
            }
        }

        public void Close()
        {
            if (_webSocket.ReadyState == WebSocketState.Open)
            {
                _webSocket.Close();
            }
        }

        public void Dispose()
        {
            if (_webSocket.ReadyState == WebSocketState.Open)
            {
                _webSocket.Close();
            }
            _webSocket.OnMessage -= OnMessage;
            _webSocket.OnError -= OnError;
            _webSocket.OnOpen -= OnOpen;
            _webSocket.OnClose -= OnClose;
            ((IDisposable)_webSocket)?.Dispose();
        }
    }
}

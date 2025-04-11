using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
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
            MistDebug.Log($"[WebSocket] Closed: {e.Reason}");
        }

        private void OnOpen(object sender, EventArgs e)
        {
            MistDebug.Log("[WebSocket] Opened");
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            MistDebug.LogError($"[WebSocket] Error: {e.Message}");
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            MistDebug.Log($"[WebSocket] Received: {e.Data}");
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
                MistDebug.Log($"[WebSocket] Sending: {message}");
                _webSocket.Send(message);
            }
            else
            {
                MistDebug.LogWarning("WebSocket is not open.");
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

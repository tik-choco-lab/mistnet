using System;
using System.Threading;
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
            Debug.Log($"[WebSocket] Closed: {e.Reason}");
        }

        private void OnOpen(object sender, EventArgs e)
        {
            Debug.Log("[WebSocket] Opened");
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            Debug.LogError($"[WebSocket] Error: {e.Message}");
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            Debug.Log($"[WebSocket] Received: {e.Data}");
            _syncContext.Post(_ => OnMessageReceived?.Invoke(e.Data), null);
        }

        public void Connect()
        {
            _webSocket.Connect();
        }

        public void Send(string message)
        {
            if (_webSocket.ReadyState == WebSocketState.Open)
            {
                Debug.Log($"[WebSocket] Sending: {message}");
                _webSocket.Send(message);
            }
            else
            {
                Debug.LogWarning("WebSocket is not open.");
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

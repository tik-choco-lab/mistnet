using System;
using System.Threading;
using WebSocketSharp;

namespace MistNet
{
    public class WebSocketHandler : IDisposable
    {
        private WebSocket _ws;
        private readonly SynchronizationContext _syncContext;

        public Action OnOpen { get; set; }
        public Action<string> OnMessage { get; set; }
        public Action<string> OnClose { get; set; }
        public Action<string> OnError { get; set; }

        public WebSocketHandler(string url)
        {
            _ws = new WebSocket(url);
            _syncContext = SynchronizationContext.Current;

            // Set up WebSocket event handlers
            _ws.OnOpen += HandleOnOpen;
            _ws.OnMessage += HandleOnMessage;
            _ws.OnClose += HandleOnClose;
            _ws.OnError += HandleOnError;
        }

        private void HandleOnOpen(object sender, EventArgs e)
        {
            if (_syncContext != null)
            {
                _syncContext.Post(state =>
                {
                    OnOpen?.Invoke();
                }, null);
            }
            else
            {
                OnOpen?.Invoke();
            }
        }

        private void HandleOnMessage(object sender, MessageEventArgs e)
        {
            if (_syncContext != null)
            {
                _syncContext.Post(state =>
                {
                    OnMessage?.Invoke(e.Data);
                }, null);
            }
            else
            {
                OnMessage?.Invoke(e.Data);
            }
        }

        private void HandleOnClose(object sender, CloseEventArgs e)
        {
            if (_syncContext != null)
            {
                _syncContext.Post(state =>
                {
                    OnClose?.Invoke(e.Reason);
                }, null);
            }
            else
            {
                OnClose?.Invoke(e.Reason);
            }
        }

        private void HandleOnError(object sender, ErrorEventArgs e)
        {
            if (_syncContext != null)
            {
                _syncContext.Post(state =>
                {
                    OnError?.Invoke(e.Message);
                }, null);
            }
            else
            {
                OnError?.Invoke(e.Message);
            }
        }

        public void Connect()
        {
            _ws.Connect();
        }

        public void Send(string message)
        {
            _ws.Send(message);
        }

        public void Close()
        {
            _ws.Close();
        }

        public void Dispose()
        {
            if (_ws == null) return;
            _ws.OnOpen -= HandleOnOpen;
            _ws.OnMessage -= HandleOnMessage;
            _ws.OnClose -= HandleOnClose;
            _ws.OnError -= HandleOnError;

            Close();
            _ws = null;
        }

        public bool IsConnected()
        {
            return _ws is { IsAlive: true };
        }
    }
}

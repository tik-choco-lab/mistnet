using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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
            _syncContext.Post(state => { OnOpen?.Invoke(); }, null);
        }

        private void HandleOnMessage(object sender, MessageEventArgs e)
        {
            _syncContext.Post(state => { OnMessage?.Invoke(e.Data); }, null);
        }

        private void HandleOnClose(object sender, CloseEventArgs e)
        {
            _syncContext.Post(state => { OnClose?.Invoke(e.Reason); }, null);
        }

        private void HandleOnError(object sender, ErrorEventArgs e)
        {
            _syncContext.Post(state => { OnError?.Invoke(e.Message); }, null);
        }

        public void Connect()
        {
            _ws.Connect();
        }

        public async UniTask ConnectAsync()
        {
            _ws.ConnectAsync();
            await UniTask.WaitUntil(() => _ws.ReadyState == WebSocketState.Open);
        }

        public void Send(string message)
        {
            _ws.Send(message);
        }

        public async UniTask CloseAsync()
        {
            _ws.CloseAsync();
            await UniTask.WaitUntil(() => _ws.ReadyState == WebSocketState.Closed);
        }

        public void Dispose()
        {
            if (_ws == null) return;
            _ws.OnOpen -= HandleOnOpen;
            _ws.OnMessage -= HandleOnMessage;
            _ws.OnClose -= HandleOnClose;
            _ws.OnError -= HandleOnError;

            CloseAsync().Forget();
            _ws = null;
        }

        public bool IsConnected()
        {
            return _ws is { IsAlive: true };
        }
    }
}

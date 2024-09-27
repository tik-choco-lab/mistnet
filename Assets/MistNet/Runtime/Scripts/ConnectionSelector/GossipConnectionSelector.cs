using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MistNet
{
    public class GossipConnectionSelector : IConnectionSelector
    {
        public override void OnConnected(string id)
        {
            MistDebug.Log($"[GossipConnectionSelector] OnConnected {id}");
        }

        public override void OnDisconnected(string id)
        {
            MistDebug.Log($"[GossipConnectionSelector] OnDisconnected {id}");
        }

        protected override void OnMessage(string data, string id)
        {
            MistDebug.Log($"[GossipConnectionSelector] OnMessage {id}");
        }

        protected override void Start()
        {
            base.Start();

        }

        private async UniTask UpdateSendMessage(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(1000, cancellationToken: token);
                
            }
        }

        private void CreateMessageData()
        {


        }

        [Serializable]
        internal class MessageData
        {
            public string type;
            public Dictionary<string, object> worlds;
            
        }
    }
}

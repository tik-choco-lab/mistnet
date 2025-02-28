using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace MistNet
{
    /// <summary>
    /// NOTE: エラー文を出してくれない　別スレッドを含むプログラムであるからだと考えられる
    /// </summary>
    public class MistSignalingServer : MonoBehaviour
    {
        private WebSocketServer _webSocketServer;

        private void Start()
        {
            if (!MistConfig.Data.GlobalNode.Enable) return;

            var port = MistConfig.Data.GlobalNode.Port;
            _webSocketServer = new WebSocketServer(port);
            _webSocketServer.AddWebSocketService<MistWebSocketBehavior>("/signaling");
            _webSocketServer.Start();
            MistDebug.Log($"[MistSignalingServer] Start {port}");
        }

        private void OnDestroy()
        {
            if (_webSocketServer == null) return;
            _webSocketServer.Stop();
            MistDebug.Log($"[MistSignalingServer] End");
        }

        private class MistWebSocketBehavior : WebSocketBehavior
        {
            private static readonly Dictionary<string, string> SessionIdToClientId = new();
            private static ConcurrentQueue<string> _signalingRequestIds = new();

            protected override void OnOpen()
            {
                MistDebug.Log($"[SERVER][OPEN] {ID}");
                _signalingRequestIds.Enqueue(ID);
            }

            protected override void OnClose(CloseEventArgs e)
            {
                MistDebug.Log($"[SERVER][CLOSE] {ID}");
                SessionIdToClientId.Remove(ID);

                var newList = _signalingRequestIds.Where(x => x != ID).ToList();
                _signalingRequestIds = new ConcurrentQueue<string>(newList);
            }

            protected override void OnMessage(MessageEventArgs e)
            {
                MistDebug.Log($"[SERVER][RECV] {e.Data}");

                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(e.Data);
                var messageType = data["type"].ToString();
                if (!SessionIdToClientId.ContainsKey(ID))
                {
                    var id = data["id"].ToString();
                    SessionIdToClientId.TryAdd(ID, id);
                }

                if (messageType == "signaling_request")
                {
                    HandleSignalingRequest();
                }
                else
                {
                    var targetId = data["target_id"].ToString();
                    var targetSessionId = SessionIdToClientId.FirstOrDefault(x => x.Value == targetId).Key;
                    if (!string.IsNullOrEmpty(targetSessionId))
                    {
                        Sessions.SendTo(e.Data, targetSessionId);
                    }
                }
            }

            private void HandleSignalingRequest()
            {
                var availableSessionIds = _signalingRequestIds.Where(id => id != ID).ToList();
                if (availableSessionIds.Count <= 0) return;
                var random = new System.Random();
                var targetSessionId = availableSessionIds[random.Next(availableSessionIds.Count)];

                if (!SessionIdToClientId.TryGetValue(targetSessionId, out var targetClientId)) return;
                var response = new
                {
                    type = "signaling_response",
                    target_id = targetClientId,
                    request = "offer"
                };
                var sendData = JsonConvert.SerializeObject(response);
                Sessions.SendTo(sendData, ID);
            }
        }
    }
}

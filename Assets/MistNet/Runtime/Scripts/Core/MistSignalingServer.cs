using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Collections.Generic;
using System.Net;
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
            _webSocketServer = new WebSocketServer(IPAddress.Any, port);
            _webSocketServer.AddWebSocketService<MistWebSocketBehavior>("/signaling");
            _webSocketServer.Start();

            MistLogger.Debug($"[SignalingServer] Start {port}");
        }

        private void OnDestroy()
        {
            if (_webSocketServer == null) return;
            _webSocketServer.Stop();
            MistLogger.Debug($"[SignalingServer] End");
        }

        private class MistWebSocketBehavior : WebSocketBehavior
        {
            private static readonly Dictionary<string, NodeId> NodeIdBySessionId = new();
            private static readonly Dictionary<NodeId, string> SessionIdByNodeId = new();

            private static readonly Queue<(NodeId, SignalingData)> RequestQueue = new();

            protected override void OnOpen()
            {
                MistLogger.Info($"[SERVER][OPEN] {ID}");
            }

            private void RequestOffer()
            {
                MistLogger.Info($"[Server] RequestOffer {ID}");
                var sendData = new SignalingData
                {
                    Type = SignalingType.Request
                };
                var message = JsonConvert.SerializeObject(sendData);
                Sessions.SendTo(message, ID);
            }

            protected override void OnMessage(MessageEventArgs e)
            {
                MistLogger.Trace($"[SERVER][RECV] {e.Data}");

                var data = JsonConvert.DeserializeObject<SignalingData>(e.Data);
                if (data.RoomId != MistConfig.Data.RoomId) return;

                SessionIdByNodeId[data.SenderId] = ID;
                NodeIdBySessionId[ID] = data.SenderId;

                if (data.Type == SignalingType.Request)
                {
                    RequestQueue.Enqueue((data.SenderId, data));
                    if (RequestQueue.Count < 2) return;
                    SendRequest();
                    return;
                }

                Send(data.ReceiverId, e.Data);
            }

            private void SendRequest()
            {
                var nodeA = RequestQueue.Dequeue();
                var nodeB = RequestQueue.Dequeue();

                var nodeAData = new SignalingData
                {
                    Type = SignalingType.Request,
                    ReceiverId = nodeA.Item1,
                    SenderId = nodeB.Item1,
                    RoomId = nodeB.Item2.RoomId
                };

                var nodeBData = new SignalingData
                {
                    Type = SignalingType.Request,
                    ReceiverId = nodeB.Item1,
                    SenderId = nodeA.Item1,
                    RoomId = nodeA.Item2.RoomId
                };

                Send(nodeA.Item1, JsonConvert.SerializeObject(nodeAData));
                Send(nodeB.Item1, JsonConvert.SerializeObject(nodeBData));
                RequestQueue.Enqueue(nodeA);
            }

            private void Send(NodeId receiverId, string data)
            {
                MistLogger.Trace($"[SERVER][SEND] {receiverId} {data}");
                var targetSessionId = SessionIdByNodeId.GetValueOrDefault(receiverId);
                if (string.IsNullOrEmpty(targetSessionId))
                {
                    MistLogger.Error($"[SERVER][ERROR] {receiverId} is not found.");
                    return;
                }

                Sessions.SendTo(data, targetSessionId);
            }

            protected override void OnClose(CloseEventArgs e)
            {
                MistLogger.Debug($"[SERVER][CLOSE] {ID}");

                SessionIdByNodeId.Remove(NodeIdBySessionId[ID]);
                NodeIdBySessionId.Remove(ID);
            }
        }
    }
}

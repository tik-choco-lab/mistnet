using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

namespace MistNet
{
    public class MistSignalingWebSocket : MonoBehaviour
    {
        private Dictionary<SignalingType, Action<SignalingData>> _functions;
        private WebSocketHandler _ws;
        private MistSignalingHandler _mistSignalingHandler;

        private async void Start()
        {
            _mistSignalingHandler = new MistSignalingHandler();
            _mistSignalingHandler.Send += Send;

            // Functionの登録
            _functions = new()
            {
                { SignalingType.Request , _mistSignalingHandler.RequestOffer },
                { SignalingType.Offer, _mistSignalingHandler.ReceiveOffer },
                { SignalingType.Answer, _mistSignalingHandler.ReceiveAnswer },
                { SignalingType.Candidate, _mistSignalingHandler.ReceiveCandidate },
                { SignalingType.Candidates, _mistSignalingHandler.ReceiveCandidates },
            };
            
            // 接続
            await UniTask.Yield(); // SignalingServerが立ち上がるのを待つ
            ConnectToSignalingServer();
            SendRequest();
        }

        private void SendRequest()
        {
            var sendData = new SignalingData
            {
                Type = SignalingType.Request,
                SenderId = PeerRepository.I.SelfId,
                RoomId = MistConfig.Data.RoomId,
            };

            Send(sendData, null);
        }

        private void OnDestroy()
        {
            _mistSignalingHandler.Dispose();
            _ws?.Dispose();
        }

        private int _currentAddressIndex;

        private void ConnectToSignalingServer()
        {
            if (_currentAddressIndex >= MistConfig.Data.Bootstraps.Length)
            {
                MistDebug.LogError("[Signaling][WebSocket] All signaling server addresses failed.");
                return;
            }

            var address = MistConfig.Data.Bootstraps[_currentAddressIndex];
            _ws = new WebSocketHandler(address);

            _ws.OnOpen += () => MistDebug.Log("[Signaling][WebSocket] Opened");
            _ws.OnClose += message =>
            {
                MistDebug.Log($"[Signaling][WebSocket] Closed {message}");
                // 接続が閉じた場合、再接続を試みる（失敗した場合のみ）
                TryNextAddress();
            };

            _ws.OnMessage += OnMessage;

            _ws.OnError += message =>
            {
                MistDebug.LogError($"[Signaling][WebSocket] Error {message}");
                // エラーが発生した場合も再接続を試みる
                TryNextAddress();
            };

            _ws.Connect();
        }

        private void TryNextAddress()
        {
            _currentAddressIndex++;
            ConnectToSignalingServer();
        }

        private void OnMessage(string message)
        {
            var response = JsonConvert.DeserializeObject<SignalingData>(message);
            var type = response.Type;
            _functions[type](response);
        }

        private void Send(SignalingData sendData, NodeId _)
        {
            var text = JsonConvert.SerializeObject(sendData);
            _ws.Send(text);
        }
    }
}

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

namespace MistNet
{
    public class MistSignalingWebSocket : MonoBehaviour
    {
        public static MistSignalingWebSocket I;

        private Dictionary<string, Action<Dictionary<string, object>>> _functions;
        private WebSocketHandler _ws;
        private MistSignalingHandler _mistSignalingHandler;

        private async void Start()
        {
            I = this;
            _mistSignalingHandler = new MistSignalingHandler();
            _mistSignalingHandler.Send += Send;

            // Functionの登録
            _functions = new()
            {
                { "signaling_response", _mistSignalingHandler.ReceiveSignalingResponse},
                { "offer", _mistSignalingHandler.ReceiveOffer },
                { "answer", _mistSignalingHandler.ReceiveAnswer },
                { "candidate_add", _mistSignalingHandler.ReceiveCandidate },
            };
            
            // 接続
            ConnectToSignalingServer();

            // try to connect to other nodes
            await UniTask.Yield(); // VCの初期化でAudioSourceをあらかじめMistPeerDataに登録する必要があるため
            _mistSignalingHandler.SendSignalingRequest();
        }

        private void OnDestroy()
        {
            _ws?.Dispose();
        }

        private int _currentAddressIndex;

        private void ConnectToSignalingServer()
        {
            if (_currentAddressIndex >= MistConfig.Data.Bootstraps.Length)
            {
                MistDebug.LogError("[WebSocket] All signaling server addresses failed.");
                return;
            }

            var address = MistConfig.Data.Bootstraps[_currentAddressIndex];
            _ws = new WebSocketHandler(address);

            _ws.OnOpen += () =>
            {
                MistDebug.Log("[WebSocket] Opened");
            };

            _ws.OnClose += message =>
            {
                MistDebug.Log($"[WebSocket] Closed {message}");
                // 接続が閉じた場合、再接続を試みる（失敗した場合のみ）
                TryNextAddress();
            };

            _ws.OnMessage += OnMessage;

            _ws.OnError += message =>
            {
                MistDebug.LogError($"[WebSocket] Error {message}");
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
            var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
            var type = response["type"].ToString();
            _functions[type](response);
        }

        private void Send(Dictionary<string, object> sendData, string _)
        {
            var text = JsonConvert.SerializeObject(sendData);
            _ws.Send(text);
        }
    }
}

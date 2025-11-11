using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace MistNet
{
    public class MistSignalingWebSocket : IDisposable
    {
        private Dictionary<SignalingType, Action<SignalingData>> _functions;
        private WebSocketHandler _ws;
        private MistSignalingHandler _mistSignalingHandler;
        private readonly IPeerRepository _peerRepository;

        public MistSignalingWebSocket(IPeerRepository peerRepository)
        {
            _peerRepository = peerRepository;
        }

        public async UniTask Init()
        {
            _mistSignalingHandler = new MistSignalingHandler(PeerActiveProtocol.WebSocket, _peerRepository);
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

        public async UniTask ReconnectToSignalingServer()
        {
            await _ws.CloseAsync();
            await _ws.ConnectAsync();
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


        private int _currentAddressIndex;

        private void ConnectToSignalingServer()
        {
            var address = MistConfig.Data.Bootstraps[_currentAddressIndex];
            _ws = new WebSocketHandler(address);

            _ws.OnOpen += () => MistLogger.Debug("[Signaling][WebSocket] Opened");
            _ws.OnClose += message =>
            {
                MistLogger.Info($"[Signaling][WebSocket] Closed {message}");
                // 接続が閉じた場合、再接続を試みる（失敗した場合のみ）
                TryNextAddress();
            };

            _ws.OnMessage += OnMessage;

            _ws.OnError += message =>
            {
                MistLogger.Error($"[Signaling][WebSocket] Error {message}");
                // エラーが発生した場合も再接続を試みる
                TryNextAddress();
            };

            _ws.Connect();
        }

        private void TryNextAddress()
        {
            _currentAddressIndex++;
            if (_currentAddressIndex >= MistConfig.Data.Bootstraps.Length)
            {
                MistLogger.Error("[Signaling][WebSocket] All signaling server addresses failed.");
                _currentAddressIndex = 0; // Reset for future attempts
                return;
            }

            ConnectToSignalingServer();
        }

        private void OnMessage(string message)
        {
            var response = JsonConvert.DeserializeObject<SignalingData>(message);
            MistLogger.Trace($"[Signaling][WebSocket] Received: {response.Type} {response.SenderId}");
            var type = response.Type;
            _functions[type](response);
        }

        private void Send(SignalingData sendData, NodeId _)
        {
            MistLogger.Trace($"[Signaling][WebSocket] Send: {sendData.Type} {sendData.ReceiverId}");
            var text = JsonConvert.SerializeObject(sendData);
            _ws.Send(text);
        }

        public void Dispose()
        {
            _ws?.Dispose();
            _mistSignalingHandler?.Dispose();
        }
    }
}

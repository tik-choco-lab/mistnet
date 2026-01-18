using MemoryPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace MistNet
{
    public class MistSignalingWebRTC : IDisposable
    {
        private readonly MistSignalingHandler _mistSignalingHandler;
        private readonly Dictionary<SignalingType, Action<SignalingData>> _functions;
        private readonly WLSend _wlSend;

        public MistSignalingWebRTC(IPeerRepository peerRepository, WLRegisterReceive wlRegisterReceive, WLSend wlSend)
        {
            _mistSignalingHandler = new MistSignalingHandler(PeerActiveProtocol.WebRTC, peerRepository);
            _mistSignalingHandler.Send += SendSignalingMessage;
            _wlSend = wlSend;
            // Functionの登録
            _functions = new Dictionary<SignalingType, Action<SignalingData>>
            {
                { SignalingType.Offer, _mistSignalingHandler.ReceiveOffer },
                { SignalingType.Answer, _mistSignalingHandler.ReceiveAnswer },
                { SignalingType.Candidate, _mistSignalingHandler.ReceiveCandidate },
            };
            
            // MistManager.I.World.RegisterReceive(MistNetMessageType.Signaling, ReceiveSignalingMessage);
            wlRegisterReceive(MistNetMessageType.Signaling, ReceiveSignalingMessage);
        }

        /// <summary>
        /// 送信
        /// NOTE: 切断した相手にすぐに接続を試みると、nullになることがある
        /// </summary>
        /// <param name="sendData"></param>
        /// <param name="targetId"></param>
        private void SendSignalingMessage(SignalingData sendData, NodeId targetId)
        {
            var message = new P_Signaling
            {
                Data = JsonConvert.SerializeObject(sendData)
            };
            var data = MemoryPackSerializer.Serialize(message);
            _wlSend(MistNetMessageType.Signaling, data, targetId);
        }

        /// <summary>
        /// 受信
        /// </summary>
        private void ReceiveSignalingMessage(byte[] bytes, NodeId senderId)
        {
            var receiveData = MemoryPackSerializer.Deserialize<P_Signaling>(bytes);
            var response = JsonConvert.DeserializeObject<SignalingData>(receiveData.Data);
            var type = response.Type;
            MistLogger.Trace($"[Signaling][WebRTC][{type}] {response.SenderId}");
            _functions[type](response);
        }

        public void Connect(NodeId id)
        {
            MistLogger.Trace($"[Signaling][WebRTC] Connecting: {id}");
            _mistSignalingHandler.SendOffer(id).Forget();
        }

        public void Dispose()
        {
            _mistSignalingHandler?.Dispose();
        }
    }
}

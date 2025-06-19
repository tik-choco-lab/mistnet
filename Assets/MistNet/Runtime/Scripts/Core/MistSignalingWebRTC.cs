using MemoryPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MistNet
{
    public class MistSignalingWebRTC : MonoBehaviour
    {
        private MistSignalingHandler _mistSignalingHandler;
        private Dictionary<SignalingType, Action<SignalingData>> _functions;
        
        private void Start()
        {
            _mistSignalingHandler = new MistSignalingHandler();
            _mistSignalingHandler.Send += SendSignalingMessage;
            // Functionの登録
            _functions = new Dictionary<SignalingType, Action<SignalingData>>
            {
                { SignalingType.Offer, _mistSignalingHandler.ReceiveOffer },
                { SignalingType.Answer, _mistSignalingHandler.ReceiveAnswer },
                { SignalingType.Candidate, _mistSignalingHandler.ReceiveCandidate },
            };
            
            MistManager.I.AddRPC(MistNetMessageType.Signaling, ReceiveSignalingMessage);
            MistManager.I.ConnectAction += Connect;
        }

        private void OnDestroy()
        {
            MistManager.I.ConnectAction -= Connect;
            _mistSignalingHandler.Dispose();
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
            MistManager.I.Send(MistNetMessageType.Signaling, data, targetId);
        }

        /// <summary>
        /// 受信
        /// </summary>
        private void ReceiveSignalingMessage(byte[] bytes, NodeId sourceId)
        {
            var receiveData = MemoryPackSerializer.Deserialize<P_Signaling>(bytes);
            var response = JsonConvert.DeserializeObject<SignalingData>(receiveData.Data);
            var type = response.Type;
            MistDebug.Log($"[Signaling][WebRTC][{type}] {sourceId}");
            _functions[type](response);
        }

        private void Connect(NodeId id)
        {
            MistDebug.Log($"[Signaling][WebRTC] Connecting: {id}");
            _mistSignalingHandler.SendOffer(id).Forget();
        }
    }
}

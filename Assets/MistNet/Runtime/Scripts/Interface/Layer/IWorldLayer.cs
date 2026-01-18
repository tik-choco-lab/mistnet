using System;

namespace MistNet
{
    public interface IWorldLayer : IDisposable
    {
        void Send(MistNetMessageType type, byte[] data, NodeId targetId);
        void SendAll(MistNetMessageType type, byte[] data);
        
        /// <summary>
        /// 位置同期専用の高速チャンネルで送信（unreliable、順序保証なし）
        /// </summary>
        void SendLocation(byte[] data, NodeId targetId);
        
        void AddSendFailedCallback(Delegate callback);
        void RegisterReceive(MistNetMessageType type, MessageReceivedHandler receiver);
        
        /// <summary>
        /// 位置同期受信コールバックを登録
        /// </summary>
        void RegisterLocationReceive(Action<byte[], NodeId> callback);
    }

    public delegate void MessageReceivedHandler(byte[] data, NodeId fromId);

    public delegate void WLRegisterReceive(MistNetMessageType type, MessageReceivedHandler receiver);
    public delegate void WLSend(MistNetMessageType type, byte[] data, NodeId targetId);
}

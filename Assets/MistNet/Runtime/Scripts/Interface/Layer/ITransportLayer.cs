using System;

namespace MistNet
{
    public interface ITransportLayer : IDisposable
    {
        void Init();

        void Connect(NodeId id);
        void Disconnect(NodeId id);
        void DisconnectAll();
        void Send(NodeId targetId, MistMessage data, bool isForward = false);
        
        /// <summary>
        /// 位置同期専用の高速チャンネルで送信（unreliable、順序保証なし）
        /// </summary>
        void SendLocation(NodeId targetId, byte[] data);

        void AddConnectCallback(Delegate callback);
        void AddDisconnectCallback(Delegate callback);
        void RegisterReceive(Action<MistMessage, NodeId> callback);
        void RegisterLocationReceive(Action<byte[], NodeId> callback);

        void OnConnected(NodeId id);
        void OnDisconnected(NodeId id);
        void OnMessage(byte[] data, NodeId senderId);
        void OnLocationMessage(byte[] data, NodeId senderId);

        bool IsConnectingOrConnected(NodeId id);
        bool IsConnected(NodeId id);
    }
}
